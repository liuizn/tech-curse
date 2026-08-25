using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;
using TechCurse.ArchitectureTests.Common;

namespace TechCurse.ArchitectureTests.Controllers;

public class ControllerArchitectureTests
{
    [Fact]
    public void Controllers_Should_InheritFrom_ControllerBase()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApiAssembly)
            .That()
            .ResideInNamespace("TechCurse.Api.Controllers")
            .And()
            .AreClasses()
            .Should()
            .Inherit(typeof(ControllerBase))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All classes in API.Controllers must inherit from ControllerBase. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Not_DirectlyDependOn_DbContext()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApiAssembly)
            .That()
            .ResideInNamespace("TechCurse.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "TechCurse.Infrastructure.Data")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Controllers must not directly depend on DbContext or EntityFrameworkCore. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Not_DirectlyDependOn_RepositoryImplementations()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApiAssembly)
            .That()
            .ResideInNamespace("TechCurse.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("TechCurse.Infrastructure.Repositories")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Controllers must not directly depend on repository implementations. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Not_DirectlyDependOn_RepositoryInterfaces()
    {
        var result = Types.InAssembly(ArchitectureConstants.ApiAssembly)
            .That()
            .ResideInNamespace("TechCurse.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("TechCurse.Domain.Interfaces.Repositories")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Controllers must not directly depend on repository interfaces (use MediatR/Application Services instead). Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
