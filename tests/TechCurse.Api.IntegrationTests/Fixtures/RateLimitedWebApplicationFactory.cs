using Microsoft.AspNetCore.Hosting;

namespace TechCurse.Api.IntegrationTests.Fixtures;

public class RateLimitedWebApplicationFactory : CustomWebApplicationFactory
{
    public const int LimiteDeAutenticacao = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", LimiteDeAutenticacao.ToString());
        builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
    }
}
