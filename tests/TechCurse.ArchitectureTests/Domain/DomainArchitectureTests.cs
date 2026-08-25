using FluentAssertions;
using NetArchTest.Rules;
using TechCurse.ArchitectureTests.Common;

namespace TechCurse.ArchitectureTests.Domain;

public class DomainArchitectureTests
{
    [Fact]
    public void DomainEntities_Should_ResideIn_Domain_Entities()
    {
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .That()
            .ResideInNamespace("TechCurse.Domain.Entities")
            .Should()
            .BeClasses()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain entities must be classes in Domain.Entities. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void DomainExceptions_Should_InheritFrom_Exception()
    {
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .That()
            .ResideInNamespace("TechCurse.Domain.Exceptions")
            .And()
            .AreClasses()
            .Should()
            .Inherit(typeof(Exception))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All domain exceptions must inherit from System.Exception. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_EntityFramework()
    {
        var result = Types.InAssembly(ArchitectureConstants.DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain layer must not have dependencies on Entity Framework Core. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
