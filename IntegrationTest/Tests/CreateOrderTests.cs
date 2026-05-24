using System.Net;
using Application.Shared.DTOs.Order;
using Application.Shared.Events;
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

        // Act 1: создаём заказ
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var responseBody = await response.ReadFromJsonAsync<ApiResponse<OrderCreatedDto>>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseBody?.Data);
        var orderId = responseBody.Data.Id;

        // 👇 Act 2: эмулируем ответ от ProductService (минуя RabbitMQ)
        var stockReservedEvent = new StockReservedEvent(
            OrderId: orderId,
            ProductId: productId,
            ReservedQuantity: orderQuantity,
            CorrelationId: Guid.NewGuid().ToString("N")
        );
        await factory.TriggerSagaHandlerAsync(stockReservedEvent);

        // 👇 Act 3: эмулируем ответ от PaymentService
        var paymentProcessedEvent = new PaymentProcessedEvent(
            OrderId: orderId,
            Success: true,
            ErrorMessage: null,
            CorrelationId: stockReservedEvent.CorrelationId
        );
        await factory.TriggerSagaHandlerAsync(paymentProcessedEvent);
    
        // Assert: проверяем финальный статус
        var completedOrder = await SagaWaiter.WaitForOrderCompletionAsync(_client, orderId);
    
        Assert.Equal(OrderStatus.Confirmed, completedOrder.Status);
        Assert.Equal(productPriceInKopecks * orderQuantity, completedOrder.TotalAmountInKopecks);
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
    public async Task CreateOrder_InsufficientStock_Returns201_ThenCancelledViaSaga()
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

        // Act 1: заказ создаётся
        var response = await _client.PostJsonAsync<CreateOrderRequest>("/api/orders", request);
        var responseBody = await response.ReadFromJsonAsync<ApiResponse<OrderCreatedDto>>();
    
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(responseBody?.Data);
        var orderId = responseBody.Data.Id;

        // 👇 Act 2: эмулируем ошибку резерва (минуя RabbitMQ)
        var stockFailedEvent = new StockReservationFailedEvent(
            OrderId: orderId,
            ProductId: productId,
            RequestedQuantity: 10000,
            Reason: "Недостаточно стока",
            CorrelationId: Guid.NewGuid().ToString("N")
        );
        await factory.TriggerSagaHandlerAsync(stockFailedEvent);

        // Assert: заказ отменён
        var completedOrder = await SagaWaiter.WaitForOrderCompletionAsync(_client, orderId);
    
        Assert.Equal(OrderStatus.Cancelled, completedOrder.Status);
        
        // 👇 Дополнительно: можно проверить, что заказ в БД имеет статус Cancelled
        // (требуется доступ к репозиторию или отдельный endpoint для отладки)
    }
}
