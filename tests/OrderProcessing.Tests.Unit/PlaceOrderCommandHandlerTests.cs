using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Application.Commands;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Application.EventHandlers;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Interfaces;
using FluentAssertions;

namespace OrderProcessing.Tests.Unit;

public class PlaceOrderCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IIdempotencyRepository> _idemRepoMock = new();
    private readonly Mock<ILogger<PlaceOrderCommandHandler>> _loggerMock = new();

    // Real event handler wired with mocked services (prevents NRE inside event flow)
    private readonly OrderPlacedEventHandler _eventHandler;

    public PlaceOrderCommandHandlerTests()
    {
        _uowMock.SetupGet(u => u.Products).Returns(_productRepoMock.Object);
        _uowMock.SetupGet(u => u.Orders).Returns(_orderRepoMock.Object);
        _uowMock.SetupGet(u => u.Idempotency).Returns(_idemRepoMock.Object);

        // Payment & notification mocks for event handler
        var paymentMock = new Mock<IPaymentService>();
        paymentMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.SendOrderConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventLoggerMock = new Mock<ILogger<OrderPlacedEventHandler>>();

        // Use the same mocked IUnitOfWork for the event handler
        _eventHandler = new OrderPlacedEventHandler(_uowMock.Object, paymentMock.Object, notificationMock.Object, eventLoggerMock.Object);
    }

    [Fact]
    public async Task Handle_SuccessfulOrder_ReturnsCreatedOrder()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new Product("P", "desc", 10m, 10);
        // ensure Id matches requested id
        typeof(Product).GetProperty("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(product, productId);

        var request = new PlaceOrderRequest
        {
            CustomerEmail = "a@b.com",
            CustomerName = "A",
            Items = new List<OrderItemRequest> { new() { ProductId = productId, Quantity = 2 } }
            // no IdempotencyKey to keep path simple
        };

        _productRepoMock.Setup(p => p.GetByIdWithLockAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepoMock.Setup(o => o.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _orderRepoMock.Setup(o => o.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // When ExecuteInTransactionAsync is called with a Func<Task<Order>>, invoke the delegate,
        // capture the returned order and make GetByIdAsync return it for the event handler.
        _uowMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Order>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Order>>, CancellationToken>(async (func, ct) =>
            {
                var order = await func();
                // ensure subsequent reads by the event handler can find the order
                _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
                return order;
            });

        var handler = new PlaceOrderCommandHandler(_uowMock.Object, _loggerMock.Object, _eventHandler);

        // Act
        var result = await handler.Handle(new PlaceOrderCommand(request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("order should be created successfully");
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Quantity.Should().Be(2);
        result.Value.TotalAmount.Should().Be(20m);
    }
}