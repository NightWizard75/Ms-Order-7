using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Shared.Pipeline;

/// <summary>
/// Pipeline behavior для автоматической валидации команд/запросов через FluentValidation.
/// Выполняется ДО хендлера. Если валидация не проходит — выбрасывает ValidationException.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken ct)
    {
        // 👇 Если нет валидаторов для этого запроса — пропускаем
        if (!validators.Any())
            return await next(ct);

        logger.LogDebug("Валидация запроса: {RequestType}", typeof(TRequest).Name);

        // 👇 Создаём контекст валидации
        var context = new ValidationContext<TRequest>(request);
        
        // 👇 Выполняем валидацию асинхронно
        var validationResult = await validators
            // Считается, что валидатор один. 
            // Более точно использовать foreach вместо .First()
            .First()
            .ValidateAsync(context, ct);

        // 👇 Если есть ошибки — логируем и выбрасываем исключение
        if (!validationResult.IsValid)
        {
            logger.LogWarning(
                "Валидация не пройдена: {RequestType}, Errors: {@Errors}",
                typeof(TRequest).Name,
                validationResult.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            );

            throw new ValidationException(validationResult.Errors);
        }

        logger.LogDebug("Валидация успешна: {RequestType}", typeof(TRequest).Name);

        // 👇 Всё ок — передаём управление следующему поведению или хендлеру
        return await next(ct);
    }
}
