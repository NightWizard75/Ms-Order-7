using System.Net.Http.Headers;
using Application.Shared.Interfaces;
using Infrastructure.Database.Context;
using Infrastructure.Handlers;
using Infrastructure.Options;
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
        // ==========================================
        // 👇 БАЗОВАЯ ИНФРАСТРУКТУРА
        // ==========================================
        
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
        // 👇 ProductServiceClient (существующий, с Polly)
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
                            ex.InnerException is HttpRequestException or IOException)
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
        
        // 3. Typed HttpClient для ProductService
        services.AddTransient<AuthDelegatingHandler>();
        services.AddHttpClient<IProductServiceClient, ProductServiceClient>()
            .AddHttpMessageHandler<AuthDelegatingHandler>();
        
        // ==========================================
        // 👇 IdentityClient (НОВЫЙ: для получения токенов)
        // ==========================================
        
        // 1. Bind конфигурации к опциям
        services.Configure<IdentityClientOptions>(
            configuration.GetSection("IdentityClient"));
        
        // 2. Named HttpClient для IdentityServer (простой, без Polly)
        // Почему без Polly: /connect/token — быстрый локальный вызов, 
        // если он падает — лучше сразу получить ошибку, чем ждать ретраев
        services.AddHttpClient("IdentityServer", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IdentityClientOptions>>().Value;
            
            client.BaseAddress = new Uri(options.Authority);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds + 5);
        });
        
        // 3. Регистрация провайдера токенов
        services.AddSingleton<ITokenProvider, ClientCredentialsTokenService>();

        return services;
    }
}
