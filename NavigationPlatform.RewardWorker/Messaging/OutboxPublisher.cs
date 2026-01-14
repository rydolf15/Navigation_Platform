using Microsoft.EntityFrameworkCore;
using NavigationPlatform.RewardWorker.Persistence;
using RabbitMQ.Client;
using Serilog.Context;
using System.Text;

namespace NavigationPlatform.RewardWorker.Messaging;

internal sealed class OutboxPublisher : BackgroundService
{
    private const string ExchangeName = "navigation.events";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IConfiguration cfg,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reward OutboxPublisher starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RewardDbContext>();

                var host = _cfg["RabbitMq:Host"] ?? "rabbitmq";
                var user = _cfg["RabbitMq:Username"] ?? "guest";
                var pass = _cfg["RabbitMq:Password"] ?? "guest";

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
                    using (LogContext.PushProperty("CorrelationId", msg.Id))
                    {
                        var body = Encoding.UTF8.GetBytes(msg.Payload);

                        var props = channel.CreateBasicProperties();
                        props.Persistent = true;
                        props.Type = msg.Type;
                        props.MessageId = msg.Id.ToString();
                        props.CorrelationId = msg.Id.ToString();

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
                _logger.LogError(ex, "Reward OutboxPublisher failed; retrying soon");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Reward OutboxPublisher stopped");
    }
}

