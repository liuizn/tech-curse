using Serilog;
using Serilog.Formatting.Json;

namespace TechCurse.Api.Configuration;

public static class SerilogSetup
{
    public static IServiceCollection AddSerilogSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((serviceProvider, loggerConfiguration) =>
        {
            var seqConnectionString = configuration.GetConnectionString("SeqUrl");

            if (string.IsNullOrWhiteSpace(seqConnectionString))
            {
                throw new InvalidOperationException("A connection string 'SeqUrl' não está configurada.");
            }

            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new JsonFormatter())
                .WriteTo.Seq(seqConnectionString);
        });

        return services;
    }
}
