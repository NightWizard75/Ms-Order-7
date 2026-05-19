using System.Reflection;
using FluentValidation;
using Infrastructure.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Web.Options;

namespace Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWeb(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 👇 Валидаторы для Web-DTO
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var auth = configuration.GetSection("Authentication").Get<AuthOptions>();
    
                // 🔹 1. Загружаем ключи вручную из 'JWKS' (синхронно, для простоты)
                using var http = new HttpClient();
    
                var jwksUrl = auth?.Authority + OidcEndpoints.Jwks;
                var jwksJson = http.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
                var jwks = new JsonWebKeySet(jwksJson);
                var signingKeys = jwks.GetSigningKeys();
    
                if (!signingKeys.Any())
                    throw new InvalidOperationException($"No signing keys found at {jwksUrl}");
    
                // 🔹 2. Настраиваем валидацию с явным ключом
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth?.Authority,
                    ValidateAudience = true,
                    ValidAudience = auth?.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeys,  // ← Ключи заданы явно
                    ClockSkew = TimeSpan.Zero
                };
    
                // 🔹 3. Отключаем Authority, чтобы не было конфликта с ручными ключами
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                
                // 🔹 4. Минимальное логирование событий
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"[Auth] Failed: {context.Exception.Message}");
                        return Task.CompletedTask;
                    }
                };
            });
        
        // Обязательно для [Authorize]
        services.AddAuthorization();
    
        // 👇 Контроллеры + глобальный фильтр валидации
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Order Service API", Version = "v1" }));

        return services;
    }
}
