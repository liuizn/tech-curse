using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using TechCurse.Application.Features.Students.Commands.CreateStudent;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Students;

public class CreateStudentCommandHandlerTests
{
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
    private readonly CreateStudentCommandHandler _handler;

    public CreateStudentCommandHandlerTests()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        _userManagerMock = new Mock<UserManager<IdentityUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _handler = new CreateStudentCommandHandler(_studentRepositoryMock.Object, _userManagerMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenEmailAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        _studentRepositoryMock.Setup(r => r.EmailExistsAsync("test@example.com"))
            .ReturnsAsync(true);

        var command = new CreateStudentCommand("João", "test@example.com");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("O e-mail informado já está em uso por outro estudante.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenIdentityUserNotFound_ShouldThrowConflictException()
    {
        // Arrange
        _studentRepositoryMock.Setup(r => r.EmailExistsAsync("test@example.com"))
            .ReturnsAsync(false);

        _userManagerMock.Setup(m => m.FindByEmailAsync("test@example.com"))
            .ReturnsAsync((IdentityUser?)null);

        var command = new CreateStudentCommand("João", "test@example.com");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Usuário não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValid_ShouldCreateStudentAndReturnDto()
    {
        // Arrange
        var identityUser = new IdentityUser { Id = "identity-123", Email = "test@example.com" };

        _studentRepositoryMock.Setup(r => r.EmailExistsAsync("test@example.com"))
            .ReturnsAsync(false);

        _userManagerMock.Setup(m => m.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(identityUser);

        _studentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Student>()))
            .Callback<Student>(s => s.StudentId = 1)
            .Returns(Task.CompletedTask);

        var command = new CreateStudentCommand("João Silva", "test@example.com");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Nome.Should().Be("João Silva");
        result.Email.Should().Be("test@example.com");

        _studentRepositoryMock.Verify(r => r.AddAsync(It.Is<Student>(s => s.Nome == "João Silva" && s.IdentityUserId == "identity-123")), Times.Once);
    }
}
