using Microsoft.AspNetCore.Hosting;

namespace TechCurse.Api.IntegrationTests.Fixtures;

public class CorsEnabledWebApplicationFactory : CustomWebApplicationFactory
{
    public const string OrigemPermitida = "http://localhost:4200";
    public const string OrigemNaoPermitida = "http://malicioso.example.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Cors:AllowedOrigins", $"{OrigemPermitida}, http://localhost:8081/");
    }
}
