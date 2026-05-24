namespace Application.Shared.Events;

/// <summary>
/// Событие: не удалось зарезервировать товар.
/// Запускает компенсацию в оркестраторе.
/// </summary>
public record StockReservationFailedEvent(
    Guid OrderId,
    Guid ProductId,
    int RequestedQuantity,
    string Reason,
    string CorrelationId);
