using System.Text;
using System.Text.Json;
using Application.Shared.Events;
using Infrastructure.Database.Context;
using Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.BackgroundServices;

/// <summary>
/// Фоновый воркер: читает неопубликованные сообщения из Outbox и публикует их в RabbitMQ.
/// Запускается раз в 5 секунд (настраивается).
/// </summary>
public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly RabbitMQSettings _rabbitMqSettings;
    private readonly int _batchSize;
    private readonly TimeSpan _pollInterval;

    public OutboxPublisherWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxPublisherWorker> logger,
        IOptions<RabbitMQSettings> rabbitMqSettingsOptions,
        int batchSize = 50,
        int pollIntervalSeconds = 5)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rabbitMqSettings = rabbitMqSettingsOptions.Value;
        _batchSize = batchSize;
        _pollInterval = TimeSpan.FromSeconds(pollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker запущен. Опрос каждые {Interval} сек, пачка по {BatchSize}", 
            _pollInterval.TotalSeconds, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка при обработке пачки Outbox-сообщений");
                // Не прерываем цикл: следующая попытка через интервал
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
        }

        _logger.LogInformation("OutboxPublisherWorker остановлен");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // 👇 Создаём scope, чтобы получить DbContext (Scoped lifetime)
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        // 🔹 1. Читаем неопубликованные сообщения (FIFO, LIMIT)
        var messages = await context.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        _logger.LogDebug("Найдено {Count} неопубликованных сообщений", messages.Count);

        // 🔹 2. Настраиваем подключение к RabbitMQ
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqSettings.HostName,
            Port = _rabbitMqSettings.Port,
            UserName = _rabbitMqSettings.UserName,
            Password = _rabbitMqSettings.Password,
            VirtualHost = _rabbitMqSettings.VirtualHost
        };

        using var connection = await factory.CreateConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync();

        // 🔹 3. Декларируем обменник (идемпотентно)
        await channel.ExchangeDeclareAsync(
            exchange: _rabbitMqSettings.ExchangeName,
            type: ExchangeType.Topic, // Topic для гибкой маршрутизации
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // 🔹 4. Публикуем каждое сообщение
        foreach (var message in messages)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Payload);

                // 👇 Определяем routing key по типу события
                var routingKey = message.EventType switch
                {
                    nameof(OrderCreatedEvent) => "order.created",
                    nameof(PaymentRequestedEvent) => "payment.requested",
                    _ => "event.unknown"
                };

                // 👇 Публикуем с заголовками для трассировки
                var properties = new BasicProperties
                {
                    DeliveryMode = DeliveryModes.Persistent, // Сохранять на диск
                    CorrelationId = ExtractCorrelationId(message.Payload) // Сквозной ID
                };
                properties.Headers = new Dictionary<string, object?>
                {
                    ["event_type"] = message.EventType,
                    ["order_id"] = message.OrderId.ToString(),
                    ["correlation_id"] = properties.CorrelationId
                };

                await channel.BasicPublishAsync(
                    exchange: _rabbitMqSettings.ExchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: ct);

                // 👇 Только после успешной публикации помечаем как отправленное
                message.PublishedAt = DateTime.UtcNow;
                context.OutboxMessages.Update(message);
                
                _logger.LogDebug("Опубликовано: {EventType} → {RoutingKey}, OrderId={OrderId}", 
                    message.EventType, routingKey, message.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось опубликовать сообщение: Id={Id}, Type={Type}", 
                    message.Id, message.EventType);
                // Не помечаем как опубликованное → повторная попытка в следующем цикле
            }
        }

        // 🔹 5. Коммит: обновляем PublishedAt для успешно опубликованных
        await context.SaveChangesAsync(ct);
        _logger.LogInformation("Обработано пачку: {Count} сообщений", messages.Count);
    }

    /// <summary>
    /// Извлекает CorrelationId из JSON-пейлоада для заголовка.
    /// Если не удалось — генерирует новый.
    /// </summary>
    private static string ExtractCorrelationId(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            if (doc.RootElement.TryGetProperty("CorrelationId", out var prop))
                return prop.GetString() ?? Guid.NewGuid().ToString("N");
        }
        catch
        {
            // Игнорируем ошибки парсинга — сгенерируем новый ID
        }
        return Guid.NewGuid().ToString("N");
    }
}
