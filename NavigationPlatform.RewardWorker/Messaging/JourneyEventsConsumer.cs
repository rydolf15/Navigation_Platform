using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.RewardWorker.Processing;
using NavigationPlatform.RewardWorker.Persistence;
using NavigationPlatform.RewardWorker.Persistence.Outbox;
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
        var hasMessageId = Guid.TryParse(messageId, out var messageGuid);

        try
        {
            using (LogContext.PushProperty("IncomingMessageId", messageId))
            using (LogContext.PushProperty("IncomingMessageType", type))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RewardDbContext>();

                await using var tx = await db.Database.BeginTransactionAsync();

                if (hasMessageId)
                {
                    var alreadyProcessed = await db.InboxMessages
                        .AsNoTracking()
                        .AnyAsync(x => x.Id == messageGuid);

                    if (alreadyProcessed)
                    {
                        await tx.CommitAsync();
                        channel.BasicAck(ea.DeliveryTag, multiple: false);
                        return;
                    }
                }

                switch (type)
                {
                    case nameof(JourneyCreated):
                        await ApplyUpsertAsync(
                            JsonSerializer.Deserialize<JourneyCreated>(json)!,
                            db,
                            hasMessageId ? messageGuid : null,
                            type);
                        break;

                    case nameof(JourneyUpdated):
                        await ApplyUpsertAsync(
                            JsonSerializer.Deserialize<JourneyUpdated>(json)!,
                            db,
                            hasMessageId ? messageGuid : null,
                            type);
                        break;

                    case nameof(JourneyDeleted):
                        await ApplyDeleteAsync(
                            JsonSerializer.Deserialize<JourneyDeleted>(json)!,
                            db,
                            hasMessageId ? messageGuid : null,
                            type);
                        break;

                    default:
                        _logger.LogWarning("Ignoring unsupported event type {Type}", type);
                        break;
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing message; will requeue");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static async Task ApplyUpsertAsync(
        IJourneyDistanceEvent evt,
        RewardDbContext db,
        Guid? messageId,
        string type)
    {
        var journeyId = evt.JourneyId;
        var userId = evt.UserId;
        var date = DateOnly.FromDateTime(evt.StartTime);
        var distanceKm = evt.DistanceKm;

        var existing = await db.Journeys.FindAsync(journeyId);

        if (existing == null)
        {
            existing = new JourneyProjection
            {
                JourneyId = journeyId,
                UserId = userId,
                Date = date,
                DistanceKm = distanceKm
            };
            db.Journeys.Add(existing);

            await ApplyDeltaAsync(db, userId, date, +distanceKm, triggeringJourneyId: journeyId);
        }
        else
        {
            var oldDate = existing.Date;
            var oldDistance = existing.DistanceKm;

            existing.UserId = userId;
            existing.Date = date;
            existing.DistanceKm = distanceKm;

            if (oldDate == date)
            {
                await ApplyDeltaAsync(db, userId, date, distanceKm - oldDistance, triggeringJourneyId: journeyId);
            }
            else
            {
                await ApplyDeltaAsync(db, userId, oldDate, -oldDistance, triggeringJourneyId: journeyId);
                await ApplyDeltaAsync(db, userId, date, +distanceKm, triggeringJourneyId: journeyId);
            }
        }

        if (messageId.HasValue)
        {
            db.InboxMessages.Add(new InboxMessage
            {
                Id = messageId.Value,
                Type = type,
                OccurredUtc = DateTime.UtcNow,
                ProcessedUtc = DateTime.UtcNow
            });
        }
    }

    private static async Task ApplyDeleteAsync(
        JourneyDeleted evt,
        RewardDbContext db,
        Guid? messageId,
        string type)
    {
        var existing = await db.Journeys.FindAsync(evt.JourneyId);
        if (existing != null)
        {
            db.Journeys.Remove(existing);
            await ApplyDeltaAsync(db, existing.UserId, existing.Date, -existing.DistanceKm, triggeringJourneyId: evt.JourneyId);
        }

        if (messageId.HasValue)
        {
            db.InboxMessages.Add(new InboxMessage
            {
                Id = messageId.Value,
                Type = type,
                OccurredUtc = DateTime.UtcNow,
                ProcessedUtc = DateTime.UtcNow
            });
        }
    }

    private static async Task ApplyDeltaAsync(
        RewardDbContext db,
        Guid userId,
        DateOnly date,
        decimal deltaKm,
        Guid triggeringJourneyId)
    {
        if (deltaKm == 0) return;

        var daily = await db.DailyDistances.FindAsync(userId, date);
        if (daily == null)
        {
            daily = new DailyDistanceProjection
            {
                UserId = userId,
                Date = date,
                TotalDistanceKm = 0,
                RewardGranted = false
            };
            db.DailyDistances.Add(daily);
        }

        daily.TotalDistanceKm += deltaKm;

        if (daily.TotalDistanceKm < 0)
            daily.TotalDistanceKm = 0;

        if (!daily.RewardGranted &&
            DailyRewardEvaluator.ShouldGrant(daily.TotalDistanceKm))
        {
            daily.RewardGranted = true;
            daily.GrantedByJourneyId = triggeringJourneyId;

            db.OutboxMessages.Add(
                OutboxMessage.From(
                    new JourneyDailyGoalAchieved(
                        triggeringJourneyId,
                        userId,
                        date,
                        daily.TotalDistanceKm)));
        }
    }
}

