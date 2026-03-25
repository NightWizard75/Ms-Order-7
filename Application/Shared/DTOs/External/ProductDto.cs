namespace Application.Shared.DTOs.External;

/// <summary>
/// DTO для десериализации ответа от Product Service.
/// Не использовать для внутренней логики — только для маппинга внешних ответов.
/// </summary>
public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    int PriceInKopecks,
    string PriceAsMoney,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity
);
