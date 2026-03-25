namespace Application.Shared.DTOs.External;

public record ShowProductResponseData(
    bool Success,
    int StatusCode,
    ProductDto Data,
    string? Message = null
);
