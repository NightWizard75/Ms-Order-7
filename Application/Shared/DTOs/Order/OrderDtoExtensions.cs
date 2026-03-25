namespace Application.Shared.DTOs.Order;

/// <summary>
/// Extension методы для конвертации сущностей в DTO.
/// </summary>
public static class OrderDtoExtensions
{
    public static OrderDto ToDto(this Domain.Entities.Order order) => new OrderDto(
        Id: order.Id,
        ProductId: order.ProductId,
        Quantity: order.Quantity,
        PriceInKopecks: order.PriceInKopecks,
        TotalAmountInKopecks: order.TotalAmountInKopecks,
        Status: order.Status,
        CustomerEmail: order.CustomerEmail,
        CorrelationId: order.CorrelationId,
        CreatedAt: order.CreatedAt,
        PriceAsMoney: order.GetPriceAsMoney(),
        TotalAmountAsMoney: order.GetTotalAmountAsMoney()
    );
}
