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
// O writer padrão responde apenas "Healthy"/"Unhealthy" em texto puro, sem dizer
// qual verificação falhou. Detalhar por check é o que torna um 503 diagnosticável
// no pipeline, onde só se enxerga a resposta HTTP.
app.MapHealthChecks("/health", new HealthCheckOptions
{
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

// Seção de Migrations e Seed de Dados
//
// LIMITAÇÃO CONHECIDA: aplicar migrations no startup do próprio host é frágil quando a
// API roda com múltiplas réplicas — todas sobem juntas e disputam o mesmo banco, o que
// pode resultar em deadlock ou em uma migration aplicada pela metade. O caminho
// convencional é extrair esta etapa para um job de migração dedicado, executado uma
// única vez antes do deploy das réplicas (e com a API subindo só depois que ele termina).
// Mantido aqui pela simplicidade do projeto; não implementado de propósito.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<TechCurseContext>();

    // Providers não relacionais — o InMemory usado pelos testes de integração — não têm
    // pipeline de migrations: o schema é derivado direto do modelo. Chamar Migrate() ali
    // lançaria InvalidOperationException, então a etapa toda só faz sentido no relacional.
    if (dbContext.Database.IsRelational())
    {
        try
        {
            dbContext.Database.Migrate();

            await DbInitializer.SeedDataAsync(services);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Ocorreu um erro ao rodar as Migrations ou o Seed do banco de dados.");

            // Engolir esta exceção deixaria a aplicação no ar com o banco em estado
            // desconhecido — sem schema ou com schema desatualizado — servindo requisições
            // que só falhariam muito depois, longe da causa. Relançar aborta o startup e
            // faz o processo morrer com o erro à vista.
            throw;
        }
    }
}

app.Run();

public partial class Program { }

