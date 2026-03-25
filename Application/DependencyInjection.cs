using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Application.Shared.Pipeline;

namespace Application;

/// <summary>
/// Расширения для регистрации зависимостей Application слоя.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 👇 Регистрация MediatR (CQRS handlers)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        
        // 👇 Регистрация валидаторов FluentValidation
        services.AddValidatorsFromAssembly(assembly);
        
        // 👇 Регистрация pipeline behavior для валидации
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
