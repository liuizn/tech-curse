using FluentValidation.TestHelper;
using TechCurse.Application.Features.Students.Commands.CreateStudent;
using Xunit;

namespace TechCurse.Application.UnitTests.Validators.Students;

public class CreateStudentCommandValidatorTests
{
    private readonly CreateStudentCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        var command = new CreateStudentCommand("João Silva", "joao.silva@example.com");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Nome_Is_Empty(string? invalidName)
    {
        var command = new CreateStudentCommand(invalidName!, "joao.silva@example.com");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Nome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Nome_Exceeds_100_Characters()
    {
        var longName = new string('A', 101);
        var command = new CreateStudentCommand(longName, "joao.silva@example.com");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Nome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Email_Is_Empty(string? invalidEmail)
    {
        var command = new CreateStudentCommand("João Silva", invalidEmail!);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("joao@")]
    [InlineData("@example.com")]
    [InlineData("joao.example.com")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Email_Format_Is_Invalid(string? invalidEmail)
    {
        var command = new CreateStudentCommand("João Silva", invalidEmail!);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }
}
