using FluentValidation.TestHelper;
using TechCurse.Application.Features.Payments.Commands.RefundPayment;
using Xunit;

namespace TechCurse.Application.UnitTests.Validators.Payments;

public class RefundPaymentCommandValidatorTests
{
    private readonly RefundPaymentCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        // Arrange
        var command = new RefundPaymentCommand(1, "idemp-key-456");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_PaymentId_Is_Zero_Or_Negative(int invalidPaymentId)
    {
        // Arrange
        var command = new RefundPaymentCommand(invalidPaymentId, "idemp-key-456");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PaymentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_IdempotencyKey_Is_Empty(string invalidKey)
    {
        // Arrange
        var command = new RefundPaymentCommand(1, invalidKey);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
