using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TechCurse.Infrastructure.Data;

public static class DbInitializer
{
    private const string ChaveEmailAdmin = "Seed:Admin:Email";
    private const string ChaveSenhaAdmin = "Seed:Admin:Password";

    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = { "Admin", "Instructor", "Student" };

        foreach (var roleName in roles)
        {
            var roleExists = await roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        await SemearAdminDeDesenvolvimentoAsync(serviceProvider);
    }

    private static async Task SemearAdminDeDesenvolvimentoAsync(IServiceProvider serviceProvider)
    {
        var ambiente = serviceProvider.GetRequiredService<IHostEnvironment>();

        if (!ambiente.IsDevelopment())
        {
            return;
        }

        var configuracao = serviceProvider.GetRequiredService<IConfiguration>();
        var email = configuracao[ChaveEmailAdmin];
        var senha = configuracao[ChaveSenhaAdmin];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new IdentityUser
        {
            UserName = email.Split('@')[0],
            Email = email,
            EmailConfirmed = true
        };

        var resultado = await userManager.CreateAsync(admin, senha);

        if (!resultado.Succeeded)
        {
            var erros = string.Join("; ", resultado.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"Não foi possível criar o Admin semeado: {erros}");
        }

        await userManager.AddToRoleAsync(admin, "Admin");
    }
}
