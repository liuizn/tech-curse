using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments.Commands.RefundPayment;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public class RefundPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IPaymentGatewayAdapter> _paymentGatewayMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<RefundPaymentCommandHandler>> _loggerMock = new();
    private readonly RefundPaymentCommandHandler _handler;

    public RefundPaymentCommandHandlerTests()
    {
        _handler = new RefundPaymentCommandHandler(
            _paymentRepositoryMock.Object,
            _paymentGatewayMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenPaymentNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Payment?)null);

        var command = new RefundPaymentCommand(1, "key-123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Pagamento não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenPaymentNotPaidOrNoExternalTransactionId_ShouldThrowNotAllowedException()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Pending, // Not paid
            ExternalTransactionId = null
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        var command = new RefundPaymentCommand(1, "key-123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Apenas pagamentos processados e com ID de transação podem ser estornados.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenGatewayFails_ShouldThrowBadRequestException()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Paid,
            ExternalTransactionId = "TX_123"
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _paymentGatewayMock.Setup(g => g.RefundTransactionAsync("TX_123", It.IsAny<CancellationToken>(), "key-123"))
            .ReturnsAsync(new GatewayResponse(false, null, null, "REFUND_FAILED", "Falha no estorno", DateTime.UtcNow));

        var command = new RefundPaymentCommand(1, "key-123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*REFUND_FAILED*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenGatewayTimesOut_ShouldThrowGatewayTimeoutException()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Paid,
            ExternalTransactionId = "TX_123"
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _paymentGatewayMock.Setup(g => g.RefundTransactionAsync("TX_123", It.IsAny<CancellationToken>(), "key-123"))
            .ThrowsAsync(new OperationCanceledException());

        var command = new RefundPaymentCommand(1, "key-123");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<GatewayTimeoutException>()
            .WithMessage("A comunicação com o provedor de pagamento excedeu o tempo limite durante o estorno.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValid_ShouldRefundPaymentAndInvalidateCache()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Paid,
            ExternalTransactionId = "TX_123",
            IsActive = true
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _paymentGatewayMock.Setup(g => g.RefundTransactionAsync("TX_123", It.IsAny<CancellationToken>(), "key-123"))
            .ReturnsAsync(new GatewayResponse(true, "TX_123", null, null, null, DateTime.UtcNow));

        var command = new RefundPaymentCommand(1, "key-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.IsActive.Should().BeFalse();
        payment.RefundedAt.Should().NotBeNull();

        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("payments:list:"), Times.Once);
    }
}
