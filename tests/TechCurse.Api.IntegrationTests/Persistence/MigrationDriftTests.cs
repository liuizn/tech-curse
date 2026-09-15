using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechCurse.Infrastructure.Data;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Persistence;

public class MigrationDriftTests
{
    private static TechCurseContext CriarContextoRelacional()
    {
        var options = new DbContextOptionsBuilder<TechCurseContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=APITechCurse;Username=postgres;Password=SenhaIrrelevante1!")
            .Options;

        return new TechCurseContext(options);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Modelo_NaoDeveTerAlteracoesSemMigrationCorrespondente()
    {
        using var context = CriarContextoRelacional();

        context.Database.HasPendingModelChanges().Should().BeFalse(
            "toda alteracao de entidade ou de OnModelCreating precisa de uma migration: "
            + "rode 'dotnet ef migrations add <Nome> --project src/Infrastructure --startup-project src/Api'");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Assembly_DeveConterTodasAsMigrationsAplicaveis()
    {
        using var context = CriarContextoRelacional();

        var migrations = context.Database.GetMigrations().ToList();

        migrations.Should().NotBeEmpty();
        migrations.Should().OnlyHaveUniqueItems();
    }
}
