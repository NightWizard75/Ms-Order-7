using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace UnitTest.Domain.Entities;

/// <summary>
/// Тесты бизнес-логики смены статусов заказа (Domain Layer).
/// Используют только публичный API сущности.
/// </summary>
public class OrderTests
{
    // ========================================================================
    // Тесты перехода: Pending -> Processing
    // ========================================================================

    [Fact]
    public void MarkAsProcessing_FromPending_Succeeds()
    {
        // Arrange: новый заказ всегда в статусе Pending
        var order = CreatePendingOrder();

        // Act
        order.MarkAsProcessing();

        // Assert
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public void MarkAsProcessing_FromConfirmed_Throws()
    {
        // Arrange: переводим заказ в финальный статус через публичный API
        var order = CreatePendingOrder();
        order.MarkAsProcessing();
        order.Confirm(); // Теперь статус = Confirmed

        // Act
        var act = () => order.MarkAsProcessing();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Processing*");
    }

    // ========================================================================
    // Тесты перехода: Processing -> Confirmed
    // ========================================================================

    [Fact]
    public void Confirm_FromProcessing_Succeeds()
    {
        // Arrange
        var order = CreatePendingOrder();
        order.MarkAsProcessing(); // Переводим в нужное состояние

        // Act
        order.Confirm();

        // Assert
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_FromPending_Throws()
    {
        // Arrange: заказ всё ещё в начальном статусе
        var order = CreatePendingOrder();

        // Act
        var act = () => order.Confirm();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Processing*");
    }

    // ========================================================================
    // Тесты перехода: -> Cancelled (компенсация)
    // ========================================================================

    [Fact]
    public void Cancel_FromPending_Succeeds()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        order.Cancel("Customer request");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromProcessing_Succeeds()
    {
        // Arrange
        var order = CreatePendingOrder();
        order.MarkAsProcessing();

        // Act
        order.Cancel("Stock unavailable");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromConfirmed_Throws()
    {
        // Arrange: финальный статус
        var order = CreatePendingOrder();
        order.MarkAsProcessing();
        order.Confirm();

        // Act
        var act = () => order.Cancel("Test");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Confirmed*");
    }

    // ========================================================================
    // Тесты перехода: -> Failed
    // ========================================================================

    [Fact]
    public void Fail_FromProcessing_Succeeds()
    {
        // Arrange
        var order = CreatePendingOrder();
        order.MarkAsProcessing();

        // Act
        order.Fail("System error");

        // Assert
        order.Status.Should().Be(OrderStatus.Failed);
    }

    [Fact]
    public void Fail_FromConfirmed_Throws()
    {
        // Arrange
        var order = CreatePendingOrder();
        order.MarkAsProcessing();
        order.Confirm();

        // Act
        var act = () => order.Fail("Test");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Confirmed*"); // или "*Cancelled*", зависит от сообщения в домене
    }

    // ========================================================================
    // Хелперы
    // ========================================================================

    /// <summary>
    /// Создаёт валидный заказ в начальном статусе (Pending).
    /// </summary>
    private static Order CreatePendingOrder() => new(
        id: Guid.NewGuid(),
        productId: Guid.NewGuid(),
        quantity: 1,
        priceInKopecks: 10_000,
        customerEmail: "test@example.com",
        correlationId: Guid.NewGuid().ToString("N")
    );
}
