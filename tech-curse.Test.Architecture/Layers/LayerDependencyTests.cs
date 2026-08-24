using FluentAssertions;
using NetArchTest.Rules;
using TechCurse.Test.Architecture.Common;

namespace TechCurse.Test.Architecture.Layers;

public class LayerDependencyTests
{
    [Fact]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ArchitectureConstants.ApplicationNamespace,
                ArchitectureConstants.InfrastructureNamespace,
                ArchitectureConstants.ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Domain layer should have zero dependencies on other layers. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_Infrastructure_Or_Api()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ArchitectureConstants.InfrastructureNamespace,
                ArchitectureConstants.ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Application layer should not depend on Infrastructure or API. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Infrastructure_Should_Not_HaveDependencyOn_Api()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ArchitectureConstants.ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Infrastructure layer should not depend on API layer. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_AspNetCoreMvc()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Domain layer should not depend on presentation framework. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Application_Should_Not_DependOn_AspNetCoreMvc()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Application layer should not depend on presentation framework. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
