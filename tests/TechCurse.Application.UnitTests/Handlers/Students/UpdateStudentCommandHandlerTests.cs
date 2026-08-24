using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Students.Commands.UpdateStudent;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Students;

public class UpdateStudentCommandHandlerTests
{
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly UpdateStudentCommandHandler _handler;

    public UpdateStudentCommandHandlerTests()
    {
        _handler = new UpdateStudentCommandHandler(
            _studentRepositoryMock.Object,
            _cacheServiceMock.Object,
            _currentUserServiceMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentNotFoundOrDeleted_ShouldThrowNotFoundException()
    {
        // Arrange
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Student?)null);

        var command = new UpdateStudentCommand(1, "Novo Nome");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Estudante não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchUser_ShouldThrowNotAllowedException()
    {
        // Arrange
        var student = new Student { StudentId = 1, IdentityUserId = "user-1", IsDeleted = false };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-2");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var command = new UpdateStudentCommand(1, "Novo Nome");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possui permissão suficiente para atualizar este registro.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValid_ShouldUpdateStudentAndInvalidateCache()
    {
        // Arrange
        var student = new Student { StudentId = 1, Nome = "Antigo", Email = "test@example.com", IdentityUserId = "user-1", IsDeleted = false };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-1");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var command = new UpdateStudentCommand(1, "Novo Nome");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        student.Nome.Should().Be("Novo Nome");
        _studentRepositoryMock.Verify(r => r.UpdateAsync(student), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync("students:item:1", It.IsAny<StudentOutputDto>(), It.IsAny<TimeSpan>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("students:list:"), Times.Once);
    }
}
