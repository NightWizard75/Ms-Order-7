using FluentValidation;

namespace Application.Features.Orders.GetOrderById;

/// <summary>
/// Валидатор запроса получения заказа по ID.
/// </summary>
public class GetOrderByIdQueryValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("ID заказа обязателен");
    }
}
