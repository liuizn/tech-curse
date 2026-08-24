using FluentValidation.TestHelper;
using TechCurse.Application.Features.Enrollments.Commands.CreateEnrollment;
using Xunit;

namespace TechCurse.Application.UnitTests.Validators.Enrollments;

public class CreateEnrollmentCommandValidatorTests
{
    private readonly CreateEnrollmentCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Pass_Validation_When_Command_Is_Valid()
    {
        // Arrange
        var command = new CreateEnrollmentCommand(1, 2);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_StudentId_Is_Zero_Or_Negative(int invalidStudentId)
    {
        // Arrange
        var command = new CreateEnrollmentCommand(invalidStudentId, 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.StudentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void Should_Have_Error_When_CourseId_Is_Zero_Or_Negative(int invalidCourseId)
    {
        // Arrange
        var command = new CreateEnrollmentCommand(1, invalidCourseId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CourseId);
    }
}
