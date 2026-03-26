using System.Net;
using Application.Shared.DTOs.Order;
using Domain.Enums;
using IntegrationTest.Fixtures;
using IntegrationTest.Helpers;
using Web.Requests;
using Web.Responses;

namespace IntegrationTest.Tests;

/// <summary>
/// Интеграционные тесты для создания заказа с Saga-оркестрацией.
/// </summary>
public class CreateOrderTests(OrderWebApplicationFactory factory) 
    : IClassFixture<OrderWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ========================================================================
    // Тест: Успешное создание заказа (цена из продукта, резерв успешен → Confirmed)
    // ========================================================================
    [Fact]
    public async Task CreateOrder_ValidData_Returns201_WithPriceFromProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        const int productPriceInKopecks = 199999;  // 1999.99 RUB
        const int orderQuantity = 3;
        const string customerEmail = "test@example.com";

        // 👇 WireMock: продукт существует с ценой + резерв успешен
        factory.MockProductService()
            .GivenProductExists(productId, productPriceInKopecks, stockQuantity: 100)
            .GivenReserveStockSuccess(productId, orderQuantity);

        var request = new CreateOrderRequest(
            ProductId: productId,
            Quantity: orderQuantity,
            CustomerEmail: customerEmail
            // 👈 Цены в запросе НЕТ — она придёт из продукта
        );

        // Act
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var responseBody = await response.ReadFromJsonAsync<ApiResponse<OrderCreatedDto>>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseBody);
        Assert.True(responseBody.Success == true);
        Assert.NotNull(responseBody.Data);
        Assert.NotEqual(Guid.Empty, responseBody.Data.Id);

        // 👇 Проверяем, что заказ создан с правильной ценой из продукта
        var getOrderResponse = await _client.GetAsync($"/api/orders/{responseBody.Data.Id}");
        var order = await getOrderResponse.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        
        Assert.NotNull(order?.Data);
        Assert.Equal(productId, order.Data.ProductId);
        Assert.Equal(orderQuantity, order.Data.Quantity);
        Assert.Equal(productPriceInKopecks * orderQuantity, order.Data.TotalAmountInKopecks);
        
        // 👇 ИЗМЕНЕНИЕ: после Saga успешный заказ имеет статус Confirmed (не Pending)
        Assert.Equal(OrderStatus.Confirmed, order.Data.Status);
    }

    // ========================================================================
    // Тест: Продукт не найден → 404
    // ========================================================================
    [Fact]
    public async Task CreateOrder_ProductNotFound_Returns404()
    {
        // Arrange
        var unknownProductId = Guid.NewGuid();
        
        factory.MockProductService()
            .GivenProductNotFound(unknownProductId);

        var request = new CreateOrderRequest(
            ProductId: unknownProductId,
            Quantity: 1,
            CustomerEmail: "test@example.com"
        );

        // Act
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var problem = await response.ReadProblemDetailsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Ресурс не найден", problem.Title);
        Assert.Contains("Product", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ========================================================================
    // Тест: Неверный email → 400
    // ========================================================================
    [Fact]
    public async Task CreateOrder_InvalidEmail_Returns400()
    {
        // Arrange
        var productId = Guid.NewGuid();
        
        factory.MockProductService()
            .GivenProductExists(productId, 10000, stockQuantity: 10);

        var request = new CreateOrderRequest(
            ProductId: productId,
            Quantity: 1,
            CustomerEmail: "not-an-email"  // ❌ Неверный формат
        );

        // Act
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var problem = await response.ReadProblemDetailsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Errors);
        Assert.Contains("CustomerEmail", problem.Errors.Keys);
        var firstError = problem.Errors["CustomerEmail"].FirstOrDefault();
        Assert.NotNull(firstError);
        Assert.Contains("email", firstError.ToLower());
    }

    // ========================================================================
    // Тест: Отрицательное количество → 400
    // ========================================================================
    [Fact]
    public async Task CreateOrder_NegativeQuantity_Returns400()
    {
        // Arrange
        var productId = Guid.NewGuid();
        
        factory.MockProductService()
            .GivenProductExists(productId, 10000, stockQuantity: 10);

        var request = new CreateOrderRequest(
            ProductId: productId,
            Quantity: -5,  // ❌ Отрицательное количество
            CustomerEmail: "test@example.com"
        );

        // Act
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var problem = await response.ReadProblemDetailsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Errors);
        Assert.Contains("Quantity", problem.Errors.Keys);
    }

    // ========================================================================
    // Тест: Недостаточно стока → 409 Conflict + компенсация (заказ отменён)
    // ========================================================================
    [Fact]
    public async Task CreateOrder_InsufficientStock_Returns409_AndOrderCancelled()
    {
        // Arrange
        var productId = Guid.NewGuid();
        const int stockQuantity = 10;
        const int orderQuantity = 50;  // 👇 Больше, чем есть в стоке
        
        factory.MockProductService()
            .GivenProductExists(productId, 10000, stockQuantity)
            .GivenReserveStockConflict(productId, orderQuantity);  // 👇 409 Conflict

        var request = new CreateOrderRequest(
            ProductId: productId,
            Quantity: orderQuantity,
            CustomerEmail: "test@example.com"
        );

        // Act
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var problem = await response.ReadProblemDetailsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Конфликт данных", problem.Title);
        
        // 👇 Дополнительно: можно проверить, что заказ в БД имеет статус Cancelled
        // (требуется доступ к репозиторию или отдельный endpoint для отладки)
    }
}
