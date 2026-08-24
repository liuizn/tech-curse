using FluentValidation.TestHelper;
using TechCurse.src.Application.Features.Courses.Commands.CreateCourse;
using Xunit;

namespace TechCurse.Test.Unit.Validators.Courses;

public class CreateCourseCommandValidatorTests
{
    private readonly CreateCourseCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        // Arrange
        var command = new CreateCourseCommand("Curso C#", "Aprenda C# do zero ao avançado", "Tecnologia", 40);

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
    public void Should_Have_Error_When_Titulo_Is_Empty_Or_Null(string invalidTitle)
    {
        // Arrange
        var command = new CreateCourseCommand(invalidTitle, "Descrição válida", "Tecnologia", 40);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Titulo);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Titulo_Exceeds_100_Characters()
    {
        // Arrange
        var longTitle = new string('A', 101);
        var command = new CreateCourseCommand(longTitle, "Descrição válida", "Tecnologia", 40);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Titulo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Descricao_Is_Empty_Or_Null(string invalidDesc)
    {
        // Arrange
        var command = new CreateCourseCommand("Curso C#", invalidDesc, "Tecnologia", 40);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Descricao);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_Categoria_Is_Empty_Or_Null(string invalidCat)
    {
        // Arrange
        var command = new CreateCourseCommand("Curso C#", "Descrição válida", invalidCat, 40);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Categoria);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_CargaHoraria_Is_Zero_Or_Negative(int invalidHours)
    {
        // Arrange
        var command = new CreateCourseCommand("Curso C#", "Descrição válida", "Tecnologia", invalidHours);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CargaHoraria);
    }
}
