using Microsoft.OpenApi.Models;

namespace TechCurse.src.API.Configuration;

public static class SwaggerDocumentationSetup
{
    public static IServiceCollection AddSwaggerDocumentationSetup(this IServiceCollection services, IConfiguration configuration)
    {
        string securitySchemeName = "Bearer";

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Tech Curse API",
                Version = "v1",
                Description = "API para gestão de cursos e alunos baseada em Clean Architecture."
            });

            c.AddSecurityDefinition(securitySchemeName, new OpenApiSecurityScheme
            {
                Description = "Insira o token JWT desta maneira: Bearer {seu_token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = securitySchemeName
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement()
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = securitySchemeName
                        },
                        Scheme = "oauth2",
                        Name = securitySchemeName,
                        In = ParameterLocation.Header,
                    },
                    new List<string>()
                }
            });

            c.EnableAnnotations();
        });
        return services;
    }
}
