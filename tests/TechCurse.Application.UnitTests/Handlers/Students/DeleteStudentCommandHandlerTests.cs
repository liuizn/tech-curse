using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using TechCurse.Application.Features.Students.Commands.DeleteStudent;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Students;

public class DeleteStudentCommandHandlerTests
{
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
    private readonly DeleteStudentCommandHandler _handler;

    public DeleteStudentCommandHandlerTests()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        _userManagerMock = new Mock<UserManager<IdentityUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _handler = new DeleteStudentCommandHandler(
            _studentRepositoryMock.Object,
            _cacheServiceMock.Object,
            _userManagerMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentNotFoundOrDeleted_ShouldThrowNotFoundException()
    {
        // Arrange
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Student?)null);

        var command = new DeleteStudentCommand(1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Estudante não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValid_ShouldSoftDeleteStudentAndLockoutUser()
    {
        // Arrange
        var identityUser = new IdentityUser { Id = "user-1", Email = "test@example.com" };
        var student = new Student
        {
            StudentId = 1,
            Nome = "João",
            Email = "test@example.com",
            IsDeleted = false,
            IdentityUser = identityUser
        };

        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(student);

        var command = new DeleteStudentCommand(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        student.IsDeleted.Should().BeTrue();
        student.DeletedAt.Should().NotBeNull();

        _studentRepositoryMock.Verify(r => r.UpdateAsync(student), Times.Once);
        _userManagerMock.Verify(m => m.SetLockoutEndDateAsync(identityUser, DateTimeOffset.MaxValue), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveAsync("students:item:1"), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("students:list:"), Times.Once);
    }
}
