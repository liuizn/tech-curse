using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;
using TechCurse.ArchitectureTests.Common;

namespace TechCurse.ArchitectureTests.NamingConventions;

public class NamingConventionTests
{
    [Fact]
    public void Handlers_Should_Have_NameEndingWith_Handler()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Or()
            .ImplementInterface(typeof(IRequestHandler<>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All MediatR request handlers must end with 'Handler'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Validators_Should_Have_NameEndingWith_Validator()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .Should()
            .HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All FluentValidation validators must end with 'Validator'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Have_NameEndingWith_Controller()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .Should()
            .HaveNameEndingWith("Controller")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All controllers must end with 'Controller'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void RepositoryImplementations_Should_Have_NameEndingWith_Repository()
    {
        var result = Types.InAssembly(ArchitectureConstants.InfrastructureAssembly)
            .That()
            .ResideInNamespace("TechCurse.Infrastructure.Repositories")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Repository")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All repository implementations must end with 'Repository'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void RepositoryInterfaces_Should_StartWithI_And_EndWithRepository()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameEndingWith("Repository")
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All repository interfaces must start with 'I' and end with 'Repository'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Commands_Should_Have_NameEndingWith_Command()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .ResideInNamespaceMatching(@"TechCurse\.Application\.Features\..*\.Commands\..*")
            .And()
            .ImplementInterface(typeof(IBaseRequest))
            .Should()
            .HaveNameEndingWith("Command")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All Command requests must end with 'Command'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Queries_Should_Have_NameEndingWith_Query()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .ResideInNamespaceMatching(@"TechCurse\.Application\.Features\..*\.Queries\..*")
            .And()
            .ImplementInterface(typeof(IBaseRequest))
            .Should()
            .HaveNameEndingWith("Query")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All Query requests must end with 'Query'. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
