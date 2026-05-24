namespace Infrastructure.Options;

public record RabbitMQSettings
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string ExchangeName { get; init; } = "orders-exchange";
    public string ExchangeType { get; init; } = RabbitMQ.Client.ExchangeType.Topic;
}
