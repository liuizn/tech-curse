using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using StackExchange.Redis;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Infrastructure.Data;
using TechCurse.src.Infrastructure.ExternalServices;

namespace TechCurse.Test.Integration.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtSigningKey = "TechCurseSuperSecretKeyForIntegrationTesting123456!";
    public const string JwtIssuer = "TechCurseTestIssuer";
    public const string JwtAudience = "TechCurseTestAudience";

    public InMemoryTestCacheService CacheService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("ConnectionStrings:RedisCache", "localhost:6379,abortConnect=false");
        builder.UseSetting("ConnectionStrings:SeqUrl", "http://localhost:5341");
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<TechCurseContext>(options =>
            {
                options.UseInMemoryDatabase("TechCurse_IntegrationDb");
            });

            // Substitui ICacheService
            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICacheService));
            if (cacheDescriptor != null)
            {
                services.Remove(cacheDescriptor);
            }
            services.AddSingleton<ICacheService>(CacheService);

            // Substitui IConnectionMultiplexer com Mock
            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor != null)
            {
                services.Remove(redisDescriptor);
            }
            var mockMultiplexer = new Mock<IConnectionMultiplexer>();
            services.AddSingleton(mockMultiplexer.Object);

            // Garante SimulatedPaymentGatewayAdapter
            var gatewayDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPaymentGatewayAdapter));
            if (gatewayDescriptor != null)
            {
                services.Remove(gatewayDescriptor);
            }
            services.AddScoped<IPaymentGatewayAdapter, SimulatedPaymentGatewayAdapter>();
        });
    }

    public async Task EnsureRolesCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = { "Admin", "Instructor", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    public string GenerateJwtToken(string userId, string email, string role)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(JwtSigningKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        var token = GenerateJwtToken("admin-guid-1", "admin@techcurse.com", "Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateStudentClient(string email = "student@techcurse.com", string userId = "student-guid-1")
    {
        var client = CreateClient();
        var token = GenerateJwtToken(userId, email, "Student");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateInstructorClient()
    {
        var client = CreateClient();
        var token = GenerateJwtToken("instructor-guid-1", "instructor@techcurse.com", "Instructor");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        return CreateClient();
    }

    public async Task ExecuteDbContextAsync(Func<TechCurseContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TechCurseContext>();
        await action(dbContext);
    }
}
