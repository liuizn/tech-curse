# 🧪 Test Patterns & Reference Guide

This document contains standard patterns, code snippets, and guidelines for testing the **Tech Curse API**.

---

## 1. Unit Testing: MediatR Command & Query Handlers

Use `Moq` or `NSubstitute` to mock external dependencies, ensuring each handler is tested in complete isolation.

### Pattern: Command Handler Test
```csharp
public class CreateCourseCommandHandlerTests
{
    private readonly ICourseRepository _courseRepositoryMock = Substitute.For<ICourseRepository>();
    private readonly ICacheService _cacheServiceMock = Substitute.For<ICacheService>();
    private readonly CreateCourseCommandHandler _handler;

    public CreateCourseCommandHandlerTests()
    {
        _handler = new CreateCourseCommandHandler(_courseRepositoryMock, _cacheServiceMock);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCreateCourseAndClearCache()
    {
        // Arrange
        var command = new CreateCourseCommand("C# Avançado", "Curso de C#", 199.90m, 40);
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Titulo.Should().Be("C# Avançado");
        await _courseRepositoryMock.Received(1).AddAsync(Arg.Any<Course>());
        await _cacheServiceMock.Received(1).RemoveByPrefixAsync("courses:list:");
    }

    [Fact]
    public async Task Handle_WhenCourseAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        _courseRepositoryMock.ExistsByTitleAsync("C# Avançado").Returns(true);
        var command = new CreateCourseCommand("C# Avançado", "Curso de C#", 199.90m, 40);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
```

---

## 2. Unit Testing: FluentValidation Validators

Use `FluentValidation.TestHelper` for fast, declarative validation tests without mocking.

```csharp
public class CreateCourseCommandValidatorTests
{
    private readonly CreateCourseCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_Have_Error_When_Titulo_Is_Empty(string invalidTitle)
    {
        var command = new CreateCourseCommand(invalidTitle, "Desc", 100m, 20);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Titulo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Should_Have_Error_When_Preco_Is_Zero_Or_Negative(decimal invalidPrice)
    {
        var command = new CreateCourseCommand("Título Válido", "Desc", invalidPrice, 20);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Preco);
    }
}
```

---

## 3. Architecture Testing: Clean Architecture Rules (NetArchTest)

Enforce architectural boundaries across assemblies.

```csharp
public class ArchitectureTests
{
    private const string DomainNamespace = "TechCurse.src.Domain";
    private const string ApplicationNamespace = "TechCurse.src.Application";
    private const string InfrastructureNamespace = "TechCurse.src.Infrastructure";
    private const string ApiNamespace = "TechCurse.src.API";

    [Fact]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        var assembly = typeof(Course).Assembly;

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_Infrastructure_Or_Api()
    {
        var assembly = typeof(CreateCourseCommand).Assembly;

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_Should_Have_NameEndingWith_Handler()
    {
        var assembly = typeof(CreateCourseCommandHandler).Assembly;

        var result = Types.InAssembly(assembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Or()
            .ImplementInterface(typeof(IRequestHandler<>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

---

## 4. Integration Testing: WebApplicationFactory

Test real HTTP pipelines, middleware, validation behaviors, and status codes.

```csharp
public class CourseEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CourseEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCourses_ShouldReturn200OK_WithPagedList()
    {
        var response = await _client.GetAsync("/tech-curse/course?pageNumber=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```
