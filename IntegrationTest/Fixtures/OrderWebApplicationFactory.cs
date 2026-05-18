using Application.Shared.DTOs.External;
using Infrastructure.Database.Context;
using Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Web.Responses;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace IntegrationTest.Fixtures;

/// <summary>
/// Фабрика приложения для интеграционных тестов Order Service.
/// </summary>
public class OrderWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // 👇 Тестовая БД для Order Service (как в Product)
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithDatabase("order_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithCleanUp(true)
        .Build();

    // 👇 WireMock для мокирования Product Service
    private WireMockServer? _productServiceMock;

    public string TestConnectionString => _dbContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 👇 1. Переопределяем строку подключения к БД
        builder.ConfigureServices(services =>
        {
            // Удаляем оригинальный DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<OrderDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Регистрируем тестовый DbContext
            services.AddDbContext<OrderDbContext>(options =>
                options.UseNpgsql(TestConnectionString,
                    b => b.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName)));

            // 👇 2. Переопределяем настройки ProductServiceClient (если мок создан)
            if (_productServiceMock != null)
            {
                services.Configure<ProductServiceClientSettings>(options =>
                {
                    options.BaseAddress = _productServiceMock.Urls[0];
                });
            }
        });

        // 👇 3. Отключаем авто-сидинг в тестах
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:AutoSeed"] = "false",
                ["Database:AutoMigrate"] = "true"
            });
        });
    }

    // ========================================================================
    // Методы для настройки мока Product Service
    // ========================================================================

    public OrderWebApplicationFactory MockProductService()
    {
        _productServiceMock ??= WireMockServer.Start();
        
        // 👇 Обновляем настройки (для случаев, когда мок создаётся после старта)
        // Это работает, потому что мы меняем значение в уже созданном экземпляре настроек
        var options = Services.GetRequiredService<IOptions<ProductServiceClientSettings>>();
        if (options.Value is ProductServiceClientSettings settings)
        {
            settings.BaseAddress = _productServiceMock.Urls[0];
        }
        
        return this;
    }

    public OrderWebApplicationFactory GivenProductExists(Guid productId, int priceInKopecks, int stockQuantity)
    {
        _productServiceMock ??= MockProductService()._productServiceMock;_productServiceMock ??= WireMockServer.Start(new WireMockServerSettings
               {
                   StartAdminInterface = true,  // 👈 Включает UI: http://localhost:<порт>/__admin__/
                   Logger = new WireMockConsoleLogger()  // 👈 Логи в консоль (опционально)
               });

        var response = new ApiResponse<ProductDto>(
            Success: true,
            StatusCode: 200,
            Data: new ProductDto(
                Id: productId,
                Name: "Test Product",
                Description: "",
                PriceInKopecks: priceInKopecks,
                PriceAsMoney: $"{priceInKopecks / 100.0m:F2} RUB",
                StockQuantity: stockQuantity,
                ReservedQuantity: 0,
                AvailableQuantity: stockQuantity
            ),
            Message: null
        );

        _productServiceMock!
            .Given(Request.Create()
                .WithPath($"/api/products/{productId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(response)
                .WithHeader("Content-Type", "application/json"));

        return this;
    }

    public OrderWebApplicationFactory GivenProductNotFound(Guid productId)
    {
        _productServiceMock ??= MockProductService()._productServiceMock;

        var response = new ApiResponse<ProductDto>(
            Success: false,
            StatusCode: 404,
            Data: null,
            Message: "Product not found"
        );

        _productServiceMock!
            .Given(Request.Create()
                .WithPath($"/api/products/{productId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBodyAsJson(response)
                .WithHeader("Content-Type", "application/json"));

        return this;
    }
    
    // ========================================================================
    // Методы для мокирования ReserveStockAsync (Saga)
    // ========================================================================

    public OrderWebApplicationFactory GivenReserveStockSuccess(Guid productId, int quantity)
    {
        MockProductService();
        var mock = _productServiceMock!;

        mock
            .Given(Request.Create()
                .WithPath($"/api/products/reserve")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new ApiResponse<bool>(true, 200, true, null))
                .WithHeader("Content-Type", "application/json"));

        return this;
    }

    public OrderWebApplicationFactory GivenReserveStockConflict(Guid productId, int quantity)
    {
        MockProductService();
        var mock = _productServiceMock!;
        var response = new ApiResponse<bool>(
            Success: false,
            StatusCode: 409,
            Data: false,
            Message: "Недостаточно стока"
        );

        mock
            .Given(Request.Create()
                .WithPath($"/api/products/reserve")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(409)
                .WithBodyAsJson(response)
                .WithHeader("Content-Type", "application/json"));

        return this;
    }

    // ========================================================================
    // IAsyncLifetime: управление контейнерами
    // ========================================================================

    public async Task InitializeAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        await _dbContainer.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        _productServiceMock?.Stop();
        await _dbContainer.StopAsync();
        await base.DisposeAsync();
    }

    // ========================================================================
    // Хелперы для тестов
    // ========================================================================

    public OrderDbContext GetDbContext() => Services.GetRequiredService<OrderDbContext>();
}
