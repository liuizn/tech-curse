using FluentAssertions;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Specifications;
using Xunit;

namespace TechCurse.Test.Unit.Specifications;

public class PaymentProcessableSpecificationTests
{
    private readonly PaymentProcessableSpecification _specification = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void IsSatisfiedBy_WhenPaymentIsActiveAndPending_ShouldReturnTrue()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            IsActive = true,
            Status = PaymentStatus.Pending,
            Amount = 100m
        };

        // Act
        var result = _specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsSatisfiedBy_WhenPaymentIsInactive_ShouldReturnFalse()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            IsActive = false,
            Status = PaymentStatus.Pending,
            Amount = 100m
        };

        // Act
        var result = _specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    [Trait("Category", "Unit")]
    public void IsSatisfiedBy_WhenPaymentStatusIsNotPending_ShouldReturnFalse(PaymentStatus status)
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            IsActive = true,
            Status = status,
            Amount = 100m
        };

        // Act
        var result = _specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ErrorMessage_ShouldReturnExpectedMessage()
    {
        // Act & Assert
        _specification.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
