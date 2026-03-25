using Application.Shared.DTOs.Order;
using Web.Responses;

namespace Web.Extensions;

/// <summary>
/// Extension-методы для формирования ответов API заказов.
/// </summary>
public static class OrderResponseExtensions
{
    /// <summary>
    /// Формирует стандартный ответ 201 Created для создания заказа.
    /// </summary>
    public static ApiResponse<OrderCreatedDto> ToOrderCreatedResponse(
        this OrderDto orderDto) => new ApiResponse<OrderCreatedDto>(
        Success: true,
        StatusCode: StatusCodes.Status201Created,
        Data: new OrderCreatedDto(
            Id: orderDto.Id,
            Status: orderDto.Status.ToString(),
            TotalAmount: orderDto.TotalAmountInKopecks
        )
    );

    /// <summary>
    /// Формирует стандартный ответ 200 OK для получения заказа.
    /// </summary>
    public static ApiResponse<T> ToOrderResponse<T>(
        this T orderDto,
        int statusCode = StatusCodes.Status200OK) => new ApiResponse<T>(
        Success: true,
        StatusCode: statusCode,
        Data: orderDto
    );
}
