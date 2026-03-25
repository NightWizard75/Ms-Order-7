using Domain.Enums;

namespace Application.Shared.DTOs.Order;

/// <summary>
/// DTO для представления заказа в API ответах.
/// </summary>
public record OrderDto(
    Guid Id,
    Guid ProductId,
    int Quantity,
    int PriceInKopecks,
    int TotalAmountInKopecks,
    OrderStatus Status,
    string CustomerEmail,
    string CorrelationId,
    DateTime CreatedAt,
    string PriceAsMoney,
    string TotalAmountAsMoney
);
