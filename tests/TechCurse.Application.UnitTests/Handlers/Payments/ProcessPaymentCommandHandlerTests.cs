using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Factory;
using TechCurse.Application.Features.Payments;
using TechCurse.Application.Features.Payments.Commands.ProcessPayment;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public class ProcessPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IPaymentGatewayAdapter> _paymentGatewayMock = new();
    private readonly Mock<IPaymentStrategy> _strategyMock = new();
    private readonly PaymentStrategyFactory _strategyFactory;
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<ProcessPaymentCommandHandler>> _loggerMock = new();
    private readonly ProcessPaymentCommandHandler _handler;

    public ProcessPaymentCommandHandlerTests()
    {
        _strategyMock.Setup(s => s.PaymentMethodType).Returns(PaymentMethodType.CreditCard);
        _strategyFactory = new PaymentStrategyFactory(new[] { _strategyMock.Object });

        _handler = new ProcessPaymentCommandHandler(
            _paymentRepositoryMock.Object,
            _paymentGatewayMock.Object,
            _strategyFactory,
            _cacheServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenPaymentNotFound_ShouldThrowNotFoundException()
    {
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Payment?)null);

        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, "key-123");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Pagamento não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenPaymentNotProcessable_ShouldThrowNotAllowedException()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Paid,
            IsActive = true
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, "key-123");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotAllowedException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenGatewayFails_ShouldThrowBadRequestException()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Pending,
            IsActive = true,
            Amount = 100m
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _strategyMock.Setup(s => s.ProcessAsync(payment, _paymentGatewayMock.Object, "key-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayResponse(false, null, null, "CARD_DECLINED", "Saldo insuficiente.", DateTime.UtcNow));

        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, "key-123");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*CARD_DECLINED*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenGatewayTimesOut_ShouldThrowGatewayTimeoutException()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Pending,
            IsActive = true,
            Amount = 100m
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _strategyMock.Setup(s => s.ProcessAsync(payment, _paymentGatewayMock.Object, "key-123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, "key-123");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<GatewayTimeoutException>()
            .WithMessage("A comunicação com o provedor de pagamento excedeu o tempo limite.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenSuccess_ShouldUpdatePaymentAndReturnSuccess()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            Status = PaymentStatus.Pending,
            IsActive = true,
            Amount = 100m
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        var processedAt = DateTime.UtcNow;
        _strategyMock.Setup(s => s.ProcessAsync(payment, _paymentGatewayMock.Object, "key-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayResponse(true, "TX_12345", "https://receipt.url", null, null, processedAt));

        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, "key-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExternalTransactionId.Should().Be("TX_12345");

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.PaidAt.Should().Be(processedAt);
        payment.ExternalTransactionId.Should().Be("TX_12345");
        payment.ReceiptUrl.Should().Be("https://receipt.url");

        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync(ChavesDeCachePagamento.Lista), Times.Once);
    }
}
