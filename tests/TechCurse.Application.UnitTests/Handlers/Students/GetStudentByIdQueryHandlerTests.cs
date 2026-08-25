using FluentAssertions;
using Moq;
using TechCurse.Application.Features.Students.Queries.GetStudentById;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Students;

public class GetStudentByIdQueryHandlerTests
{
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetStudentByIdQueryHandler _handler;

    public GetStudentByIdQueryHandlerTests()
    {
        _handler = new GetStudentByIdQueryHandler(_studentRepositoryMock.Object, _currentUserServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentNotFoundOrDeleted_ShouldThrowNotFoundException()
    {
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Student?)null);

        var query = new GetStudentByIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Estudante não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchUser_ShouldThrowNotAllowedException()
    {
        var student = new Student { StudentId = 1, IdentityUserId = "user-real", IsDeleted = false };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-fake");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var query = new GetStudentByIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possui permissão suficiente para acessar este registro.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenAdminOrMatchingUser_ShouldReturnStudentDto()
    {
        var student = new Student { StudentId = 1, Nome = "Maria", Email = "maria@example.com", IdentityUserId = "user-1", IsDeleted = false, DataCadastro = DateTime.UtcNow };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-1");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var query = new GetStudentByIdQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Nome.Should().Be("Maria");
        result.Email.Should().Be("maria@example.com");
    }
}
