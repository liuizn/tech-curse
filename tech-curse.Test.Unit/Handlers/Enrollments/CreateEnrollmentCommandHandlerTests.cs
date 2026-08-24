using FluentAssertions;
using Moq;
using TechCurse.src.Application.Features.Enrollments.Commands.CreateEnrollment;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Exceptions;
using Xunit;

namespace TechCurse.Test.Unit.Handlers.Enrollments;

public class CreateEnrollmentCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<IEnrollmentRepository> _enrollmentRepositoryMock = new();
    private readonly CreateEnrollmentCommandHandler _handler;

    public CreateEnrollmentCommandHandlerTests()
    {
        _handler = new CreateEnrollmentCommandHandler(
            _currentUserServiceMock.Object,
            _courseRepositoryMock.Object,
            _studentRepositoryMock.Object,
            _enrollmentRepositoryMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenUserIsNotStudentNorAdmin_ShouldThrowNotAllowedException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Admin)).Returns(false);
        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(false);

        var command = new CreateEnrollmentCommand(1, 1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Apenas estudantes e administradores podem criar matrículas!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenUserEmailIsNull_ShouldThrowNotAllowedException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns((string?)null);

        var command = new CreateEnrollmentCommand(1, 1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Email do usuário não encontrado!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns("student@example.com");
        _studentRepositoryMock.Setup(r => r.GetByEmailAsync("student@example.com")).ReturnsAsync((Student?)null);

        var command = new CreateEnrollmentCommand(1, 1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Estudante não encontrado!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentIsNotActive_ShouldThrowNotAllowedException()
    {
        // Arrange
        var student = new Student { StudentId = 1, Email = "student@example.com", Nome = "Student" };
        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns("student@example.com");
        _studentRepositoryMock.Setup(r => r.GetByEmailAsync("student@example.com")).ReturnsAsync(student);
        _studentRepositoryMock.Setup(r => r.StudentIsActiveAsync(student)).ReturnsAsync(false);

        var command = new CreateEnrollmentCommand(1, 1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Estudante não está ativo!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var student = new Student { StudentId = 1, Email = "student@example.com", Nome = "Student" };
        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns("student@example.com");
        _studentRepositoryMock.Setup(r => r.GetByEmailAsync("student@example.com")).ReturnsAsync(student);
        _studentRepositoryMock.Setup(r => r.StudentIsActiveAsync(student)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync((Course?)null);

        var command = new CreateEnrollmentCommand(1, 10);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Curso não encontrado!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentAlreadyEnrolled_ShouldThrowConflictException()
    {
        // Arrange
        var student = new Student { StudentId = 1, Email = "student@example.com", Nome = "Student" };
        var course = new Course
        {
            CourseId = 10,
            Titulo = "Curso C#",
            Descricao = "Desc",
            Categoria = "Tech",
            CargaHoraria = 40
        };
        var existingEnrollment = new Enrollment { EnrollmentId = 99, StudentId = 1, CourseId = 10 };

        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns("student@example.com");
        _studentRepositoryMock.Setup(r => r.GetByEmailAsync("student@example.com")).ReturnsAsync(student);
        _studentRepositoryMock.Setup(r => r.StudentIsActiveAsync(student)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(course);
        _enrollmentRepositoryMock.Setup(r => r.GetByStudentCourseAsync(1, 10)).ReturnsAsync(existingEnrollment);

        var command = new CreateEnrollmentCommand(1, 10);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Estudante já está matriculado neste curso!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValidRequestByStudent_ShouldCreateEnrollment()
    {
        // Arrange
        var student = new Student { StudentId = 1, Email = "student@example.com", Nome = "Student" };
        var course = new Course
        {
            CourseId = 10,
            Titulo = "Curso C#",
            Descricao = "Desc",
            Categoria = "Tech",
            CargaHoraria = 40
        };

        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Student)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns("student@example.com");
        _studentRepositoryMock.Setup(r => r.GetByEmailAsync("student@example.com")).ReturnsAsync(student);
        _studentRepositoryMock.Setup(r => r.StudentIsActiveAsync(student)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(course);
        _enrollmentRepositoryMock.Setup(r => r.GetByStudentCourseAsync(1, 10)).ReturnsAsync((Enrollment?)null);

        var command = new CreateEnrollmentCommand(1, 10);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _enrollmentRepositoryMock.Verify(r => r.AddAsync(It.Is<Enrollment>(e => e.StudentId == 1 && e.CourseId == 10)), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValidRequestByAdmin_ShouldCreateEnrollmentUsingStudentId()
    {
        // Arrange
        var student = new Student { StudentId = 5, Email = "student5@example.com", Nome = "Student 5" };
        var course = new Course
        {
            CourseId = 10,
            Titulo = "Curso C#",
            Descricao = "Desc",
            Categoria = "Tech",
            CargaHoraria = 40
        };

        _currentUserServiceMock.Setup(s => s.IsInRole(UserRole.Admin)).Returns(true);
        _currentUserServiceMock.Setup(s => s.GetUserEmail()).Returns("admin@example.com");
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(student);
        _studentRepositoryMock.Setup(r => r.StudentIsActiveAsync(student)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(course);
        _enrollmentRepositoryMock.Setup(r => r.GetByStudentCourseAsync(5, 10)).ReturnsAsync((Enrollment?)null);

        var command = new CreateEnrollmentCommand(5, 10);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _enrollmentRepositoryMock.Verify(r => r.AddAsync(It.Is<Enrollment>(e => e.StudentId == 5 && e.CourseId == 10)), Times.Once);
    }
}
