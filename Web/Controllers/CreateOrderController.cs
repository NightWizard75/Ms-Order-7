using MediatR;
using Microsoft.AspNetCore.Mvc;
using Web.Extensions;
using Web.Requests;
using Web.Responses;

namespace Web.Controllers;

/// <summary>
/// Контроллер для создания новых заказов.
/// </summary>
[ApiController]
[Route("api/orders")]
public class CreateOrderController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// POST /api/orders
    /// Создает новый заказ в статусе Pending.
    /// </summary>
    /// <param name="request">Данные для создания заказа</param>
    /// <param name="ct">Токен отмены запроса</param>
    /// <returns>ID созданного заказа (201 Created)</returns>
    /// <response code="201">Заказ успешно создан</response>
    /// <response code="400">Ошибка валидации входных данных</response>
    /// <response code="500">Внутренняя ошибка сервера</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<OrderCreatedDto>>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;
        var orderDto = await mediator.Send(request.ToCommand(correlationId), ct);

        return CreatedAtAction(
            actionName: nameof(ShowOrderController.GetById), 
            controllerName: "ShowOrder", 
            routeValues: new { id = orderDto.Id }, orderDto.ToOrderCreatedResponse());
    }
}
