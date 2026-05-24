using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace IntegrationTest.Helpers;

/// <summary>
/// Хелпер для публикации тестовых событий в RabbitMQ (имитация ответов других сервисов).
/// </summary>
public static class RabbitMqTestHelper
{
    private const string ExchangeName = "orders-exchange";
    private const string ExchangeType = "topic";

    /// <summary>
    /// Публикует событие в указанный routing key (имитируя ответ от ProductService/PaymentService).
    /// </summary>
    public static async Task PublishEventAsync<TEvent>(
        string rabbitMqUrl,
        string routingKey,
        TEvent @event,
        string correlationId,
        CancellationToken ct = default)
        where TEvent : class
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqUrl) };
        
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = correlationId
        };

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }
    
    /// <summary>
    /// Строит AMQP-URL из отдельных настроек RabbitMQ.
    /// </summary>
    public static string BuildAmqpUrl(IConfiguration config)
    {
        var host = config["RabbitMQ:HostName"] ?? "localhost";
        var port = config.GetValue("RabbitMQ:Port", 5672);
        var user = config["RabbitMQ:UserName"] ?? "guest";
        var pass = config["RabbitMQ:Password"] ?? "guest";
        var vhost = config["RabbitMQ:VirtualHost"] ?? "/";
    
        // 👇 Формируем URI в формате amqp://user:pass@host:port/vhost
        return $"amqp://{user}:{pass}@{host}:{port}{vhost}";
    }
}
