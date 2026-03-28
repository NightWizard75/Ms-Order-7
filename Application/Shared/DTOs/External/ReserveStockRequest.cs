using System.Text.Json.Serialization;

namespace Application.Shared.DTOs.External;

public record ReserveStockRequest(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("correlationId")] string CorrelationId
);
