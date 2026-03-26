namespace Domain.Exceptions;

/// <summary>
/// Исключение: не удалось зарезервировать сток.
/// </summary>
public class StockReservationException(
    Guid productId, 
    int requestedQuantity, 
    string? reason = null) 
    : DomainException(
        BuildMessage(productId, requestedQuantity, reason), 
        "STOCK_RESERVATION_FAILED",
        BuildContext(productId, requestedQuantity, reason))
{
    public Guid ProductId { get; } = productId;
    public int RequestedQuantity { get; } = requestedQuantity;

    private static string BuildMessage(Guid productId, int requestedQuantity, string? reason)
    {
        var baseMessage = $"Не удалось зарезервировать сток: ProductId={productId}, Quantity={requestedQuantity}";
        return string.IsNullOrEmpty(reason) ? baseMessage : $"{baseMessage}. Причина: {reason}";
    }

    private static Dictionary<string, object?> BuildContext(
        Guid productId, 
        int requestedQuantity, 
        string? reason)
    {
        return new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["requestedQuantity"] = requestedQuantity,
            ["reason"] = reason
        };
    }
}
