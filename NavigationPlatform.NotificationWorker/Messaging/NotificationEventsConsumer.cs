using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.NotificationWorker.Processing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using System.Text;

namespace NavigationPlatform.NotificationWorker.Messaging;

internal sealed class NotificationEventsConsumer : BackgroundService
{
    private const string ExchangeName = "navigation.events";
    private const string DefaultQueueName = "notification-service.events";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _cfg;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEventsConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public NotificationEventsConsumer(
        IConfiguration cfg,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationEventsConsumer> logger)
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
                _logger.LogWarning(ex, "Notification service cannot connect to RabbitMQ yet; retrying in {Delay}s", RetryDelay.TotalSeconds);
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

        // Subscribe to events that affect notifications and the local favourites projection.
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyFavorited));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyUnfavorited));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyUpdated));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyDeleted));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyShared));
        _channel.QueueBind(queue, ExchangeName, nameof(JourneyDailyGoalAchieved));

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
        if (!Guid.TryParse(messageId, out var messageGuid))
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
                var processor = scope.ServiceProvider.GetRequiredService<NotificationEventProcessor>();

                await processor.ProcessAsync(messageGuid, type, json, CancellationToken.None);

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

