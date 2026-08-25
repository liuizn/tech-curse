using FluentValidation.TestHelper;
using TechCurse.Application.Features.Students.Commands.UpdateStudent;
using Xunit;

namespace TechCurse.Application.UnitTests.Validators.Students;

public class UpdateStudentCommandValidatorTests
{
    private readonly UpdateStudentCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        var command = new UpdateStudentCommand(1, "João Silva Atualizado");

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
        var command = new UpdateStudentCommand(1, invalidName!);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Nome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Nome_Exceeds_100_Characters()
    {
        var longName = new string('A', 101);
        var command = new UpdateStudentCommand(1, longName);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Nome);
    }
}
