namespace Application.Shared.Events;

/// <summary>
/// Событие интеграции: заказ создан и ожидает обработки.
/// Публикуется после сохранения заказа в статусе Pending.
/// </summary>
public record OrderCreatedEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string CorrelationId);
