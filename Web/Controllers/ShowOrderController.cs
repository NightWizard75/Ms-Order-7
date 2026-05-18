using Application.Features.Orders.GetOrderById;
using Application.Features.Orders.GetOrderStatus;
using Application.Shared.DTOs.Order;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Extensions;
using Web.Responses;

namespace Web.Controllers;

/// <summary>
/// Контроллер для получения заказа по ID.
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ShowOrderController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// GET /api/orders/{id}
    /// Получает заказ по идентификатору.
    /// </summary>
    /// <param name="id">ID заказа</param>
    /// <param name="ct">Токен отмены запроса</param>
    /// <returns>Данные заказа (200 OK) или 404 если не найден</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetById(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var query = new GetOrderByIdQuery(id);
        var orderDto = await mediator.Send(query, ct);
        
        return Ok(new ApiResponse<OrderDto>(
            Success: true,
            StatusCode: StatusCodes.Status200OK,
            Data: orderDto
        ));
    }
    
   
    /// <summary>
    /// GET /api/orders/{id}/status
    /// Получает только статус заказа (оптимизированный легковесный эндпоинт)
    /// </summary> 
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<OrderStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderStatusDto>>> GetStatus(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        // 👇 Вызов оптимизированного хендлера (только Id + Status из БД)
        var statusDto = await mediator.Send(new GetOrderStatusQuery(id), ct);
        
        return Ok(statusDto.ToOrderResponse());
    }
}
