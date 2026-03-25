using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWeb(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        // 👇 Валидаторы для Web-DTO
        services.AddValidatorsFromAssembly(assembly);
    
        // 👇 Контроллеры + глобальный фильтр валидации
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Order Service API", Version = "v1" }));

        return services;
    }
}
