using FluentAssertions;
using NetArchTest.Rules;
using TechCurse.Test.Architecture.Common;

namespace TechCurse.Test.Architecture.Domain;

public class DomainArchitectureTests
{
    [Fact]
    public void DomainEntities_Should_ResideIn_Domain_Entities()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .That()
            .ResideInNamespace("TechCurse.src.Domain.Entities")
            .Should()
            .BeClasses()
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Domain entities must be classes in Domain.Entities. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void DomainExceptions_Should_InheritFrom_Exception()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .That()
            .ResideInNamespace("TechCurse.src.Domain.Exceptions")
            .And()
            .AreClasses()
            .Should()
            .Inherit(typeof(Exception))
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"All domain exceptions must inherit from System.Exception. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_EntityFramework()
    {
        // Act
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Domain layer must not have dependencies on Entity Framework Core. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
