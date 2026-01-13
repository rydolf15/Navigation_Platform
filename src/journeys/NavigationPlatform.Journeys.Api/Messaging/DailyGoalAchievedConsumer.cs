using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Rewards;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using System.Text;
using System.Text.Json;

namespace NavigationPlatform.Api.Messaging;

internal sealed class DailyGoalAchievedConsumer : BackgroundService
{
    private const string ExchangeName = "navigation.events";
    private const string QueueName = "journey-service.daily-goal";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _cfg;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyGoalAchievedConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public DailyGoalAchievedConsumer(
        IConfiguration cfg,
        IServiceScopeFactory scopeFactory,
        ILogger<DailyGoalAchievedConsumer> logger)
    {
        _cfg = cfg;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                StartConsuming();
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Journey service cannot connect to RabbitMQ yet; retrying in {Delay}s", RetryDelay.TotalSeconds);
                StopConsuming();
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }

        StopConsuming();
    }

    private void StartConsuming()
    {
        StopConsuming();

        var host = _cfg["RabbitMq:Host"] ?? "rabbitmq";
        var user = _cfg["RabbitMq:Username"] ?? "guest";
        var pass = _cfg["RabbitMq:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(QueueName, ExchangeName, nameof(JourneyDailyGoalAchieved));
        _channel.BasicQos(0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleAsync;

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
    }

    private void StopConsuming()
    {
        try { _channel?.Close(); } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
        try { _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Dispose(); } catch { /* ignore */ }

        _channel = null;
        _connection = null;
    }

    private async Task HandleAsync(object sender, BasicDeliverEventArgs ea)
    {
        var channel = _channel;
        if (channel == null)
            return;

        var json = Encoding.UTF8.GetString(ea.Body.ToArray());
        var messageId = ea.BasicProperties?.MessageId;

        try
        {
            using (LogContext.PushProperty("IncomingMessageId", messageId))
            using (LogContext.PushProperty("IncomingMessageType", ea.RoutingKey))
            {
                var evt = JsonSerializer.Deserialize<JourneyDailyGoalAchieved>(json)!;

                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var rewardsDb = scope.ServiceProvider.GetRequiredService<RewardReadDbContext>();

                var journey = await db.Journeys.FindAsync([evt.JourneyId]);
                if (journey != null)
                {
                    journey.MarkDailyGoalAchieved();
                    await uow.CommitAsync(CancellationToken.None);
                }

                var row = await rewardsDb.DailyDistances.FindAsync([evt.UserId, evt.Date]);
                if (row == null)
                {
                    rewardsDb.DailyDistances.Add(new DailyDistanceProjection
                    {
                        UserId = evt.UserId,
                        Date = evt.Date,
                        TotalDistanceKm = evt.TotalDistanceKm,
                        RewardGranted = true
                    });
                }
                else
                {
                    row.TotalDistanceKm = evt.TotalDistanceKm;
                    row.RewardGranted = true;
                }

                await rewardsDb.SaveChangesAsync();

                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing JourneyDailyGoalAchieved; will requeue");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }
}

