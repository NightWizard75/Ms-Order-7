namespace Application.Shared.Events;

/// <summary>
/// Событие: товар успешно зарезервирован в ProductService.
/// Публикуется после успешного уменьшения доступного остатка.
/// </summary>
public record StockReservedEvent(
    Guid OrderId,
    Guid ProductId,
    int ReservedQuantity,
    string CorrelationId);
