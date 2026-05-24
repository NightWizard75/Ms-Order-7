using System.Text;
using System.Text.Json;
using Application.Services;
using Application.Shared.Events;
using Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.BackgroundServices;

/// <summary>
/// Фоновый сервис: слушает очереди RabbitMQ и делегирует обработку событий оркестратору Saga.
/// </summary>
public class RabbitMqHostedService(
    IServiceProvider serviceProvider,
    ILogger<RabbitMqHostedService> logger,
    IOptions<RabbitMQSettings> settingsOptions)
    : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly RabbitMQSettings _settings = settingsOptions.Value;

    // Очереди, на которые подписываемся
    private readonly string[] _queueNames =
    [
        "order-service.stock-reserved",      // от ProductService
        "order-service.stock-reservation-failed",
        "order-service.payment-processed"     // от PaymentService
    ];

    // 👇 Подписываемся на события от других сервисов
    // от ProductService
    // от PaymentService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RabbitMqHostedService запущен. Очереди: {Queues}", 
            string.Join(", ", _queueNames));

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };

        var connection = await factory.CreateConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // 🔹 Декларируем обменник
        await channel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: _settings.ExchangeType,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // 🔹 Для каждой очереди: создаём, биндим, подписываемся
        foreach (var queueName in _queueNames)
        {
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: _settings.ExchangeName,
                routingKey: GetRoutingKeyForQueue(queueName),
                arguments: null,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) => await HandleMessageAsync(channel, ea, queueName, stoppingToken);

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false, // 👇 Ручное подтверждение: обрабатываем → ack, ошибка → nack/requeue
                consumer: consumer,
                cancellationToken: stoppingToken);

            logger.LogInformation("Подписан на очередь: {Queue}", queueName);
        }

        // Ждём остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static string GetRoutingKeyForQueue(string queueName) =>
        queueName switch
        {
            "order-service.stock-reserved" => "stock.reserved",
            "order-service.stock-reservation-failed" => "stock.reservation.failed",
            "order-service.payment-processed" => "payment.processed",
            _ => "#"
        };

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, string queueName, CancellationToken ct)
    {
        var correlationId = ea.BasicProperties.CorrelationId ?? Guid.NewGuid().ToString("N");
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            logger.LogDebug("Получено: Queue={Queue}, RoutingKey={Key}, CorrelationId={CorrId}", 
                queueName, ea.RoutingKey, correlationId);

            // 👇 Десериализация и делегирование оркестратору
            await ProcessEventAsync(queueName, body, correlationId, ct);

            // ✅ Успех: подтверждаем обработку
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            logger.LogDebug("Обработано: CorrelationId={CorrId}", correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки: Queue={Queue}, CorrelationId={CorrId}", queueName, correlationId);
            
            // ❌ Ошибка: возвращаем в очередь для повторной попытки
            await channel.BasicNackAsync(ea.DeliveryTag, false, true, ct);
        }
    }

    private async Task ProcessEventAsync(string queueName, string jsonPayload, string correlationId, CancellationToken ct)
    {
        // ✅ Создаём scope для каждого сообщения 
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<OrderSagaOrchestrator>();
        
        switch (queueName)
        {
            case "order-service.stock-reserved":
                var stockReserved = JsonSerializer.Deserialize<StockReservedEvent>(jsonPayload);
                if (stockReserved != null)
                    await orchestrator.HandleStockReservedAsync(stockReserved.OrderId, stockReserved, ct);
                break;

            case "order-service.stock-reservation-failed":
                var stockFailed = JsonSerializer.Deserialize<StockReservationFailedEvent>(jsonPayload);
                if (stockFailed != null)
                    await orchestrator.HandleStockReservationFailedAsync(stockFailed.OrderId, stockFailed, ct);
                break;

            case "order-service.payment-processed":
                var paymentProcessed = JsonSerializer.Deserialize<PaymentProcessedEvent>(jsonPayload);
                if (paymentProcessed != null)
                    await orchestrator.HandlePaymentProcessedAsync(paymentProcessed.OrderId, paymentProcessed, ct);
                break;

            default:
                logger.LogWarning("Неизвестная очередь: {Queue}", queueName);
                break;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("RabbitMqHostedService останавливается...");
        await base.StopAsync(cancellationToken);
    }
}
