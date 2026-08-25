using FluentValidation.TestHelper;
using TechCurse.Application.Features.Payments.Commands.CreatePayment;
using Xunit;

namespace TechCurse.Application.UnitTests.Validators.Payments;

public class CreatePaymentCommandValidatorTests
{
    private readonly CreatePaymentCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        var command = new CreatePaymentCommand(1, 150.00m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_EnrollmentId_Is_Zero_Or_Negative(int invalidEnrollmentId)
    {
        var command = new CreatePaymentCommand(invalidEnrollmentId, 150.00m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EnrollmentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Amount_Is_Zero_Or_Negative(decimal invalidAmount)
    {
        var command = new CreatePaymentCommand(1, invalidAmount);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }
}
