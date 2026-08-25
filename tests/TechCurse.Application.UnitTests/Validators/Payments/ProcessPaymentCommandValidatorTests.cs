using FluentValidation.TestHelper;
using TechCurse.Application.Features.Payments.Commands.ProcessPayment;
using TechCurse.Domain.Enums;
using Xunit;

namespace TechCurse.Application.UnitTests.Validators.Payments;

public class ProcessPaymentCommandValidatorTests
{
    private readonly ProcessPaymentCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, "idemp-key-123");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_PaymentId_Is_Zero_Or_Negative(int invalidPaymentId)
    {
        var command = new ProcessPaymentCommand(invalidPaymentId, PaymentMethodType.CreditCard, "idemp-key-123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PaymentId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Type_Is_Invalid_Enum()
    {
        var command = new ProcessPaymentCommand(1, (PaymentMethodType)999, "idemp-key-123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_IdempotencyKey_Is_Empty(string? invalidKey)
    {
        var command = new ProcessPaymentCommand(1, PaymentMethodType.CreditCard, invalidKey!);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
