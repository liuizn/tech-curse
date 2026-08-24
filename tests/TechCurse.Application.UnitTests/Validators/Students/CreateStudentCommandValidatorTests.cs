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
        // Arrange
        var command = new CreateStudentCommand("João Silva", "joao.silva@example.com");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Nome_Is_Empty(string invalidName)
    {
        // Arrange
        var command = new CreateStudentCommand(invalidName, "joao.silva@example.com");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Nome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Nome_Exceeds_100_Characters()
    {
        // Arrange
        var longName = new string('A', 101);
        var command = new CreateStudentCommand(longName, "joao.silva@example.com");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Nome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Email_Is_Empty(string invalidEmail)
    {
        // Arrange
        var command = new CreateStudentCommand("João Silva", invalidEmail);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("joao@")]
    [InlineData("@example.com")]
    [InlineData("joao.example.com")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Email_Format_Is_Invalid(string invalidEmail)
    {
        // Arrange
        var command = new CreateStudentCommand("João Silva", invalidEmail);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }
}
