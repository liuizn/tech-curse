using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TechCurse.Api.Configuration;
using TechCurse.Api.Middleware;
using TechCurse.Application;
using TechCurse.Infrastructure;
using TechCurse.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container from each layer
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSerilogSetup(builder.Configuration);
builder.Services.AddEFCoreSetup(builder.Configuration);
builder.Services.AddIdentityAuthenticationSetup(builder.Configuration);
builder.Services.AddRedisCacheSetup(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentationSetup(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Homolog"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tech Curse API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Liveness: "o processo está de pé?". Predicate sempre falso significa que
// nenhuma verificação roda — nem banco, nem cache. É deliberado: se o liveness
// reprovasse por um SQL Server fora do ar, o orquestrador reiniciaria um
// processo saudável, e o restart não conserta o banco. Resposta é o texto puro
// do writer padrão, sem detalhe de infraestrutura.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: "dá para receber tráfego?". Agrega apenas os checks marcados com a
// tag "ready" (banco e cache). O writer padrão responde apenas
// "Healthy"/"Unhealthy" em texto puro, sem dizer qual verificação falhou;
// detalhar por check é o que torna um 503 diagnosticável no pipeline, onde só se
// enxerga a resposta HTTP.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            duracaoMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                nome = entry.Key,
                status = entry.Value.Status.ToString(),
                duracaoMs = entry.Value.Duration.TotalMilliseconds,
                descricao = entry.Value.Description,
                erro = entry.Value.Exception?.Message
            })
        });
    }
});

// Seção de Seed de Dados
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<TechCurseContext>();

    try
    {
        dbContext.Database.Migrate();

        await DbInitializer.SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao rodar as Migrations ou o Seed do banco de dados.");
    }
}

app.Run();

public partial class Program { }

