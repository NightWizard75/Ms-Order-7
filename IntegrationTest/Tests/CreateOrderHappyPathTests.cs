using System.Net;
using System.Net.Http.Json;
using Application.Shared.DTOs.Order;
using Application.Shared.Events;
using Domain.Enums;
using IntegrationTest.Fixtures;
using IntegrationTest.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Web.Requests;
using Web.Responses;
using Xunit;

namespace IntegrationTest.Tests;

/// <summary>
/// E2E-тест: полная цепочка создания заказа через RabbitMQ.
/// Медленный (~20 сек), поэтому помечен [Trait("Category", "E2E")].
/// Запуск: dotnet test --filter "Category=E2E"
/// </summary>
[Trait("Category", "E2E")]
public class CreateOrderHappyPathTests(OrderWebApplicationFactory factory) : IClassFixture<OrderWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly string _rabbitMqUrl = RabbitMqTestHelper.BuildAmqpUrl(
        factory.Services.GetRequiredService<IConfiguration>());

    // 👇 Получаем URL RabbitMQ из конфига фабрики

    [Fact]
    public async Task HappyPath_PostOrder_ThroughRabbitMQ_ResultsInConfirmed()
    {
        // ====================================================================
        // Arrange
        // ====================================================================
        var productId = Guid.NewGuid();
        const int productPrice = 199999;
        const int quantity = 2;
        const string email = "e2e-test@example.com";
        var correlationId = Guid.NewGuid().ToString("N");

        // 👇 Настраиваем мок ProductService (HTTP-часть для начального запроса)
        factory.MockProductService()
            .GivenProductExists(productId, productPrice, stockQuantity: 100);
            // 👇 НЕ вызываем GivenReserveStockSuccess — резерв будет через RabbitMQ!

        var request = new CreateOrderRequest(
            ProductId: productId,
            Quantity: quantity,
            CustomerEmail: email
        );

        // ====================================================================
        // Act Step 1: Создаём заказ (запускает цепочку)
        // ====================================================================
        var createResponse = await _client.PostAsJsonAsync("/api/orders", request);
        createResponse.EnsureSuccessStatusCode();
        
        var createResult = await createResponse.ReadFromJsonAsync<ApiResponse<OrderCreatedDto>>();
        var orderId = createResult!.Data!.Id;

        // ====================================================================
        // Act Step 2: Имитируем ответ ProductService через RabbitMQ
        // (в реальности ProductService получил бы OrderCreatedEvent и ответил)
        // ====================================================================
        var stockReservedEvent = new StockReservedEvent(
            OrderId: orderId,
            ProductId: productId,
            ReservedQuantity: quantity,
            CorrelationId: correlationId
        );

        await RabbitMqTestHelper.PublishEventAsync(
            _rabbitMqUrl,
            routingKey: "stock.reserved",  // 👇 Ключ, на который подписан OrderService
            stockReservedEvent,
            correlationId);

        // ====================================================================
        // Act Step 3: Имитируем ответ PaymentService через RabbitMQ
        // ====================================================================
        var paymentProcessedEvent = new PaymentProcessedEvent(
            OrderId: orderId,
            Success: true,
            ErrorMessage: null,
            CorrelationId: correlationId
        );

        await RabbitMqTestHelper.PublishEventAsync(
            _rabbitMqUrl,
            routingKey: "payment.processed",  // 👇 Ключ, на который подписан OrderService
            paymentProcessedEvent,
            correlationId);

        // ====================================================================
        // Assert: ждём и проверяем финальный статус
        // ====================================================================
        var completedOrder = await SagaWaiter.WaitForOrderCompletionAsync(_client, orderId);
        
        Assert.Equal(OrderStatus.Confirmed, completedOrder.Status);
        Assert.Equal(productPrice * quantity, completedOrder.TotalAmountInKopecks);
    }
}
