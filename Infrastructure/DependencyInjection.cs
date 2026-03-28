using Application.Shared.Interfaces;
using Infrastructure.Database.Context;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(
                        typeof(OrderDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                }));

        // 👇 Репозитории 
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ISagaRepository, SagaRepository>();
        
        
        // ==========================================
        // 👇 Регистрация ProductServiceClient (упрощённая)
        // ==========================================
        
        // 1. Привязка настроек из appsettings.json
        services.Configure<ProductServiceClientSettings>(
            configuration.GetSection("ProductService"));
        
        // 2. 👇 Регистрация ResiliencePipeline как SINGLETON (чтобы Circuit Breaker накапливал ошибки от разных запросов)
        services.AddSingleton<ResiliencePipeline<HttpResponseMessage>>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ProductServiceClientSettings>>().Value;
            
            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
                })
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .Handle<TaskCanceledException>(ex => 
                            // 👇 Перехватываем только таймауты HTTP, не пользовательские отмены
                            ex.InnerException is HttpRequestException || 
                            ex.InnerException is IOException)
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    MaxRetryAttempts = settings.MaxRetryAttempts,
                    DelayGenerator = context => 
                        new ValueTask<TimeSpan?>(TimeSpan.FromSeconds(Math.Pow(2, context.AttemptNumber)))
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerDurationSeconds),
                    MinimumThroughput = 5
                })
                .Build();
        });
        
        // 3. Регистрация HttpClient
        services.AddHttpClient<IProductServiceClient, ProductServiceClient>();
        // 👆 ProductServiceClient сам создаст пайплайн в конструкторе

        return services;
    }
}
