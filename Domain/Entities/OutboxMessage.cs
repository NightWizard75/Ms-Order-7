namespace Domain.Entities;

/// <summary>
/// Сущность Transactional Outbox.
/// Хранит события до их гарантированной публикации в шину сообщений.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrderId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty; // JSON-сериализованное событие
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}
