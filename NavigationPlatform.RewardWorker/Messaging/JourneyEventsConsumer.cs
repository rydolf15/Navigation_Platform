using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.RewardWorker.Processing;
using NavigationPlatform.RewardWorker.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using System.Text;
using System.Text.Json;

namespace NavigationPlatform.RewardWorker.Messaging;

internal sealed class JourneyEventsConsumer : BackgroundService
{
    private const string ExchangeName = "navigation.events";
    private const string DefaultQueueName = "reward-worker.journey-events";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _cfg;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JourneyEventsConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public JourneyEventsConsumer(
        IConfiguration cfg,
        IServiceScopeFactory scopeFactory,
        ILogger<JourneyEventsConsumer> logger)
    {
        _cfg = cfg;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = _cfg["RabbitMq:Queue"] ?? DefaultQueueName;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                StartConsuming(queue);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reward worker cannot connect to RabbitMQ yet; retrying in {Delay}s", RetryDelay.TotalSeconds);
                StopConsuming();
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }

        StopConsuming();
    }

    private void StartConsuming(string queue)
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
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Consume only journey distance-changing events.
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyCreated));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyUpdated));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyDeleted));

        // Keep processing simple and deterministic.
        _channel.BasicQos(0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleAsync;

        _channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
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

        var type = ea.RoutingKey;
        var json = Encoding.UTF8.GetString(ea.Body.ToArray());

        var messageId = ea.BasicProperties?.MessageId;
        var correlationId = ea.BasicProperties?.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = messageId;
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString();

        var hasMessageId = Guid.TryParse(messageId, out var messageGuid);

        try
        {
            using (LogContext.PushProperty("IncomingMessageId", messageId))
            using (LogContext.PushProperty("IncomingMessageType", type))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RewardDbContext>();
                var processor = new DailyDistanceRewardProcessor(db);

                // IMPORTANT:
                // RewardDbContext is configured with EnableRetryOnFailure, which uses a retrying execution strategy.
                // EF Core does not allow user-initiated transactions unless they're executed via CreateExecutionStrategy().
                var strategy = db.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync();

                    if (hasMessageId)
                    {
                        var alreadyProcessed = await db.InboxMessages
                            .AsNoTracking()
                            .AnyAsync(x => x.Id == messageGuid);

                        if (alreadyProcessed)
                        {
                            await tx.CommitAsync();
                            return;
                        }
                    }

                    var processed = false;

                    switch (type)
                    {
                        case nameof(JourneyCreated):
                            await processor.UpsertAsync(
                                JsonSerializer.Deserialize<JourneyCreated>(json)!);
                            processed = true;
                            break;

                        case nameof(JourneyUpdated):
                            await processor.UpsertAsync(
                                JsonSerializer.Deserialize<JourneyUpdated>(json)!);
                            processed = true;
                            break;

                        case nameof(JourneyDeleted):
                            await processor.DeleteAsync(
                                JsonSerializer.Deserialize<JourneyDeleted>(json)!);
                            processed = true;
                            break;

                        default:
                            _logger.LogWarning("Ignoring unsupported event type {Type}", type);
                            break;
                    }

                    if (processed && hasMessageId)
                    {
                        db.InboxMessages.Add(new InboxMessage
                        {
                            Id = messageGuid,
                            Type = type,
                            OccurredUtc = DateTime.UtcNow,
                            ProcessedUtc = DateTime.UtcNow
                        });
                    }

                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                });

                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing message; will requeue");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }
}

