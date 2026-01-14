using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence.Analytics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using System.Text;
using System.Text.Json;

namespace NavigationPlatform.Api.Messaging;

internal sealed class MonthlyDistanceProjectionConsumer : BackgroundService
{
    private const string ExchangeName = "navigation.events";
    private const string DefaultQueueName = "journey-service.analytics.monthly-distance";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _cfg;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonthlyDistanceProjectionConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public MonthlyDistanceProjectionConsumer(
        IConfiguration cfg,
        IServiceScopeFactory scopeFactory,
        ILogger<MonthlyDistanceProjectionConsumer> logger)
    {
        _cfg = cfg;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = _cfg["RabbitMq:Queue:MonthlyDistance"] ?? DefaultQueueName;

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
                _logger.LogWarning(ex, "Monthly distance projection cannot connect to RabbitMQ yet; retrying in {Delay}s", RetryDelay.TotalSeconds);
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

        _channel.QueueBind(queue, ExchangeName, nameof(JourneyCreated));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyUpdated));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyDeleted));

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
        var hasMessageId = Guid.TryParse(messageId, out var messageGuid);
        if (!hasMessageId)
            messageGuid = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = messageId;
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = messageGuid.ToString();

        try
        {
            using (LogContext.PushProperty("IncomingMessageId", messageId))
            using (LogContext.PushProperty("IncomingMessageType", type))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

                await using var tx = await db.Database.BeginTransactionAsync();

                var alreadyProcessed = await db.InboxMessages
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == messageGuid);

                if (alreadyProcessed)
                {
                    await tx.CommitAsync();
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                switch (type)
                {
                    case nameof(JourneyCreated):
                        await ApplyUpsertAsync(
                            JsonSerializer.Deserialize<JourneyCreated>(json)!,
                            db);
                        break;

                    case nameof(JourneyUpdated):
                        await ApplyUpsertAsync(
                            JsonSerializer.Deserialize<JourneyUpdated>(json)!,
                            db);
                        break;

                    case nameof(JourneyDeleted):
                        await ApplyDeleteAsync(
                            JsonSerializer.Deserialize<JourneyDeleted>(json)!,
                            db);
                        break;

                    default:
                        _logger.LogWarning("Ignoring unsupported event type {Type}", type);
                        break;
                }

                db.InboxMessages.Add(new AnalyticsInboxMessage
                {
                    Id = messageGuid,
                    Type = type,
                    OccurredUtc = DateTime.UtcNow,
                    ProcessedUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing monthly distance projection message; will requeue");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static async Task ApplyUpsertAsync(IJourneyDistanceEvent evt, AnalyticsDbContext db)
    {
        var journeyId = evt.JourneyId;
        var userId = evt.UserId;
        var year = evt.StartTime.Year;
        var month = evt.StartTime.Month;
        var distanceKm = evt.DistanceKm;

        var existing = await db.JourneyDistances.FindAsync(journeyId);

        if (existing == null)
        {
            existing = new JourneyDistanceProjection
            {
                JourneyId = journeyId,
                UserId = userId,
                Year = year,
                Month = month,
                DistanceKm = distanceKm,
                StartTime = evt.StartTime
            };
            db.JourneyDistances.Add(existing);

            await ApplyDeltaAsync(db, userId, year, month, +distanceKm);
            return;
        }

        var oldUserId = existing.UserId;
        var oldYear = existing.Year;
        var oldMonth = existing.Month;
        var oldDistance = existing.DistanceKm;

        existing.UserId = userId;
        existing.Year = year;
        existing.Month = month;
        existing.DistanceKm = distanceKm;
        existing.StartTime = evt.StartTime;

        if (oldUserId == userId && oldYear == year && oldMonth == month)
        {
            await ApplyDeltaAsync(db, userId, year, month, distanceKm - oldDistance);
        }
        else
        {
            await ApplyDeltaAsync(db, oldUserId, oldYear, oldMonth, -oldDistance);
            await ApplyDeltaAsync(db, userId, year, month, +distanceKm);
        }
    }

    private static async Task ApplyDeleteAsync(JourneyDeleted evt, AnalyticsDbContext db)
    {
        var existing = await db.JourneyDistances.FindAsync(evt.JourneyId);

        if (existing != null)
        {
            db.JourneyDistances.Remove(existing);
            await ApplyDeltaAsync(db, existing.UserId, existing.Year, existing.Month, -existing.DistanceKm);
            return;
        }

        // Fallback: apply using event data.
        await ApplyDeltaAsync(db, evt.UserId, evt.StartTime.Year, evt.StartTime.Month, -evt.DistanceKm);
    }

    private static async Task ApplyDeltaAsync(AnalyticsDbContext db, Guid userId, int year, int month, decimal deltaKm)
    {
        if (deltaKm == 0) return;

        var monthly = await db.MonthlyDistances.FindAsync(userId, year, month);
        if (monthly == null)
        {
            monthly = new MonthlyDistanceProjection
            {
                UserId = userId,
                Year = year,
                Month = month,
                TotalDistanceKm = 0
            };
            db.MonthlyDistances.Add(monthly);
        }

        monthly.TotalDistanceKm += deltaKm;
        if (monthly.TotalDistanceKm < 0)
            monthly.TotalDistanceKm = 0;
    }
}

