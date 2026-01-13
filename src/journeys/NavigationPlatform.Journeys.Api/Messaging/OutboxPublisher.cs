using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Infrastructure.Persistence;
using RabbitMQ.Client;
using Serilog.Context;
using System.Text;

namespace NavigationPlatform.Api.Messaging;

internal sealed class OutboxPublisher : BackgroundService
{
    private const string ExchangeName = "navigation.events";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var host = cfg["RabbitMq:Host"] ?? "rabbitmq";
                var user = cfg["RabbitMq:Username"] ?? "guest";
                var pass = cfg["RabbitMq:Password"] ?? "guest";

                var messages = await db.OutboxMessages
                    .Where(x => !x.Processed)
                    .OrderBy(x => x.OccurredUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                if (messages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var factory = new ConnectionFactory
                {
                    HostName = host,
                    UserName = user,
                    Password = pass,
                    DispatchConsumersAsync = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.ExchangeDeclare(
                    exchange: ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                channel.ConfirmSelect();

                foreach (var msg in messages)
                {
                    using (LogContext.PushProperty("OutboxMessageId", msg.Id))
                    using (LogContext.PushProperty("OutboxMessageType", msg.Type))
                    {
                        var body = Encoding.UTF8.GetBytes(msg.Payload);

                        var props = channel.CreateBasicProperties();
                        props.Persistent = true;
                        props.Type = msg.Type;
                        props.MessageId = msg.Id.ToString();

                        // Route by event type name (e.g., JourneyCreated)
                        channel.BasicPublish(
                            exchange: ExchangeName,
                            routingKey: msg.Type,
                            basicProperties: props,
                            body: body);
                    }
                }

                channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

                foreach (var msg in messages)
                    msg.Processed = true;

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPublisher failed; retrying soon");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("OutboxPublisher stopped");
    }
}

