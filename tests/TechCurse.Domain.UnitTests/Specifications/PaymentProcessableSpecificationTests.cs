using FluentAssertions;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Specifications;
using Xunit;

namespace TechCurse.Domain.UnitTests.Specifications;

public class PaymentProcessableSpecificationTests
{
    private readonly PaymentProcessableSpecification _specification = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void IsSatisfiedBy_WhenPaymentIsActiveAndPending_ShouldReturnTrue()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            IsActive = true,
            Status = PaymentStatus.Pending,
            Amount = 100m
        };

        var result = _specification.IsSatisfiedBy(payment);

        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsSatisfiedBy_WhenPaymentIsInactive_ShouldReturnFalse()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            IsActive = false,
            Status = PaymentStatus.Pending,
            Amount = 100m
        };

        var result = _specification.IsSatisfiedBy(payment);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    [Trait("Category", "Unit")]
    public void IsSatisfiedBy_WhenPaymentStatusIsNotPending_ShouldReturnFalse(PaymentStatus status)
    {
        var payment = new Payment
        {
            PaymentId = 1,
            IsActive = true,
            Status = status,
            Amount = 100m
        };

        var result = _specification.IsSatisfiedBy(payment);

        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ErrorMessage_ShouldReturnExpectedMessage()
    {
        _specification.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
