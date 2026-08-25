using FluentAssertions;
using FluentValidation;
using MediatR;
using NetArchTest.Rules;
using TechCurse.ArchitectureTests.Common;

namespace TechCurse.ArchitectureTests.DesignPatterns;

public class DesignPatternTests
{
    [Fact]
    public void Handlers_Should_ResideIn_Application_Features()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Or()
            .ImplementInterface(typeof(IRequestHandler<>))
            .Should()
            .ResideInNamespaceStartingWith("TechCurse.Application.Features")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All MediatR handlers must reside in Application.Features namespaces. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Validators_Should_ResideIn_Application_Features()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .Should()
            .ResideInNamespaceStartingWith("TechCurse.Application.Features")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All FluentValidation validators must reside in Application.Features namespaces. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void RepositoryImplementations_Should_ResideIn_Infrastructure_Repositories()
    {
        var result = Types.InAssembly(ArchitectureConstants.InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespace("TechCurse.Infrastructure.Repositories")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All repository implementations must reside in Infrastructure.Repositories namespace. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_ResideIn_API_Controllers()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespace("TechCurse.Api.Controllers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All controllers must reside in API.Controllers namespace. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
