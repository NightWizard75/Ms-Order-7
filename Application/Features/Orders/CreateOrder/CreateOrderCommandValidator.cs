using FluentValidation;

namespace Application.Features.Orders.CreateOrder;

/// <summary>
/// Валидатор команды создания заказа.
/// </summary>
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        // 👇 CorrelationId: обязательный для сквозной трассировки
        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId обязателен для трассировки");
        
        // 👇 Бизнес-правила
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ID продукта обязателен");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Количество должно быть больше нуля");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email обязателен")
            .EmailAddress().WithMessage("Некорректный формат email");
    }
}
