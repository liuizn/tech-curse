using Microsoft.EntityFrameworkCore;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.Configuration;

public static class EFCoreSetup
{
    public static IServiceCollection AddEFCoreSetup(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("UseInMemoryDatabase"))
        {
            return services;
        }

        var apiConnectionString = configuration.GetConnectionString("APITechCurse");

        if (string.IsNullOrWhiteSpace(apiConnectionString))
        {
            throw new InvalidOperationException("A connection string 'APITechCurse' não está configurada.");
        }

        services.AddDbContext<TechCurseContext>(options =>
        {
            options.UseSqlServer(
                apiConnectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                });
        });

        services.AddHealthChecks().AddSqlServer(
            apiConnectionString,
            name: "Database_SQLServer",
            tags: [HealthCheckTags.Ready]);

        return services;
    }
}
