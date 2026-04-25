using FluentAssertions;
using Moq;
using Polly;
using Microsoft.Extensions.Logging;
using OrderProcessing.Infrastructure.Service;
using OrderProcessing.Application.EventHandlers;

namespace OrderProcessing.Tests.Unit;

public class ResilientPaymentServiceTests
{
    [Fact]
    public async Task ProcessPaymentAsync_RetriesOnFalseResult_CallsInnerExpectedTimes()
    {
        // Arrange
        var innerMock = new Mock<IPaymentService>(MockBehavior.Strict);

        // Simulate transient false results twice, then success
        innerMock.SetupSequence(x => x.ProcessPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        // Retry policy: 3 attempts (initial + up to 3 retries => policy configured to retry 3 times, but for this test we'll expect 3 total calls)
        var retryPolicy = Policy<bool>
            .HandleResult(r => r == false)
            .Or<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.Zero); // zero delay to run fast in unit test

        var loggerMock = new Mock<ILogger<ResilientPaymentService>>();

        var resilient = new ResilientPaymentService(innerMock.Object, retryPolicy, loggerMock.Object);

        // Act
        var result = await resilient.ProcessPaymentAsync(Guid.NewGuid(), 123.45m, "test@example.com", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        // Called three times: two false responses followed by a successful true
        innerMock.Verify(x => x.ProcessPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
