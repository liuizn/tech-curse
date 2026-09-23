# PR A — Registro somente de aluno: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fechar a falha que permite se cadastrar como `Admin` pelo registro público, criar a rota administrativa `POST /Auth/users` e semear um Admin só em `Development`.

**Architecture:** O `AuthService` concentra a criação de usuário num método privado único, usado pelo registro público (role fixa `Student`) e pela rota nova (role do corpo, só Admin). O `DbInitializer` ganha a criação idempotente de um Admin de desenvolvimento, lida de configuração.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core Identity, EF Core 10, xUnit, FluentAssertions, Moq.

**Spec:** `docs/superpowers/specs/2026-09-23-ajustes-backend-portal-aluno-design.md` (seção "PR A").

## Global Constraints

- **Nenhum comentário em arquivo-fonte** (`.cs`, `Dockerfile`, YAML, `.props`, `.editorconfig`, `.dockerignore`). Exceções já existentes do `CLAUDE.md` continuam valendo (migrations, `.gitignore`, primeira linha do `Dockerfile`, duas últimas linhas do `.env.example`). Markdown segue normal.
- pt-BR em mensagens de exceção, documentação e commits. Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`, `refactor:`, `build:`, `ci:`). **Sem** linhas `Co-Authored-By` ou qualquer assinatura de IA.
- Chaves em Allman, `using` ordenados; rode `dotnet format TechCurse.slnx` antes de cada commit e confirme com `dotnet format TechCurse.slnx --verify-no-changes`.
- Versões de pacote só em `Directory.Packages.props` (Central Package Management). Este PR não adiciona pacotes.
- Branch: `fix/registro-somente-aluno` (já criado a partir de `main`, com o spec commitado).
- Rotas ficam sob `tech-curse/[controller]`; o registro é `POST /tech-curse/Auth/register`, a rota nova é `POST /tech-curse/Auth/users`.
- Testes de integração usam `CustomWebApplicationFactory` (provider EF em memória, ambiente `Testing`); o seed **não** roda neles pelo `Program.cs` e deve ser testado chamando `DbInitializer.SeedDataAsync` diretamente.
- Chaves de configuração do seed: `Seed:Admin:Email` e `Seed:Admin:Password`. Variáveis do compose: `SEED_ADMIN_EMAIL` e `SEED_ADMIN_PASSWORD`, repassadas como `Seed__Admin__Email` e `Seed__Admin__Password`.

---

## Mapa de arquivos

| Arquivo | Mudança |
|---|---|
| `src/Application/DTOs/AuthDTOs.cs` | `RegisterInputDto` sem `Role`; novo `CreateUserInputDto` |
| `src/Application/Interfaces/IAuthService.cs` | novo `CreateUserAsync` |
| `src/Infrastructure/Identity/AuthService.cs` | método privado `CriarUsuarioAsync`; `RegisterAsync` fixo em `Student`; `CreateUserAsync` |
| `src/Api/Controllers/AuthController.cs` | nova action `POST users` (Admin) |
| `tests/TechCurse.Api.IntegrationTests/Endpoints/AuthEndpointsTests.cs` | testes do registro e da rota nova |
| `src/Infrastructure/Data/DbInitializer.cs` | Admin semeado em `Development` |
| `tests/TechCurse.Api.IntegrationTests/Persistence/DbInitializerTests.cs` | testes do seed (novo) |
| `docker-compose.yml`, `.env.example` | variáveis do seed |
| `README.md`, `CLAUDE.md`, `docs/postman_collection.json`, `CHANGELOG.md` | documentação |

---

### Task 1: Registro sempre `Student` e rota `POST /Auth/users`

**Files:**
- Modify: `src/Application/DTOs/AuthDTOs.cs`
- Modify: `src/Application/Interfaces/IAuthService.cs`
- Modify: `src/Infrastructure/Identity/AuthService.cs:30-70`
- Modify: `src/Api/Controllers/AuthController.cs`
- Test: `tests/TechCurse.Api.IntegrationTests/Endpoints/AuthEndpointsTests.cs`

**Interfaces:**
- Produces:
  - `public record RegisterInputDto(string Name, string Email, string Password, string ConfirmPassword);`
  - `public record CreateUserInputDto(string Name, string Email, [Required] UserRole Role, string Password, string ConfirmPassword);`
  - `IAuthService.CreateUserAsync(CreateUserInputDto input): Task`
  - `private async Task<IdentityUser> CriarUsuarioAsync(string nome, string email, string senha, string confirmacaoSenha, UserRole role)` no `AuthService` (o PR B vai usar o `IdentityUser` retornado).

- [ ] **Step 1: Escrever os testes que falham**

Em `AuthEndpointsTests.cs`, troque a construção do DTO no teste `Register_WhenValid_ShouldReturn201Created` por:

```csharp
        var input = new RegisterInputDto("NovoUsuario", email, "SenhaForte@123", "SenhaForte@123");
```

E acrescente ao final da classe (antes do `}` final), mantendo o estilo do arquivo:

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenCorpoTrazRoleAdmin_ShouldCriarUsuarioApenasComoStudent()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"tentativa_admin_{Guid.NewGuid():N}@techcurse.com";
        var corpo = new
        {
            name = $"tentativa{Guid.NewGuid():N}",
            email,
            role = "Admin",
            password = "SenhaForte@123",
            confirmPassword = "SenhaForte@123"
        };

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/register", corpo);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().NotBeNull();
        var roles = await userManager.GetRolesAsync(usuario!);
        roles.Should().BeEquivalentTo(new[] { "Student" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenAnonimo_ShouldReturn401Unauthorized()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var input = new CreateUserInputDto($"anonimo{Guid.NewGuid():N}", $"anonimo_{Guid.NewGuid():N}@techcurse.com", UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", input);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenStudent_ShouldReturn403Forbidden()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateStudentClient();
        var input = new CreateUserInputDto($"aluno{Guid.NewGuid():N}", $"aluno_{Guid.NewGuid():N}@techcurse.com", UserRole.Admin, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", input);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenAdmin_ShouldReturn201_ECriarNaRolePedida()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"instrutor_{Guid.NewGuid():N}@techcurse.com";
        var input = new CreateUserInputDto($"instrutor{Guid.NewGuid():N}", email, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", input);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().NotBeNull();
        var roles = await userManager.GetRolesAsync(usuario!);
        roles.Should().BeEquivalentTo(new[] { "Instructor" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenEmailEmUso_ShouldReturn422_ComDuplicateEmail()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"repetido_{Guid.NewGuid():N}@techcurse.com";
        var primeiro = new CreateUserInputDto($"primeiro{Guid.NewGuid():N}", email, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");
        var segundo = new CreateUserInputDto($"segundo{Guid.NewGuid():N}", email, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");
        (await client.PostAsJsonAsync("/tech-curse/Auth/users", primeiro)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", segundo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("errors").TryGetProperty("DuplicateEmail", out _).Should().BeTrue();
    }
```

Acrescente `using System.Text.Json;` aos `using` do arquivo (ordenados).

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthEndpointsTests"`
Expected: FAIL de compilação — `RegisterInputDto` não tem construtor com 4 argumentos e `CreateUserInputDto` não existe.

- [ ] **Step 3: DTOs e interface**

`src/Application/DTOs/AuthDTOs.cs` — troque a linha do `RegisterInputDto` por estas duas:

```csharp
public record RegisterInputDto(string Name, string Email, string Password, string ConfirmPassword);
public record CreateUserInputDto(string Name, string Email, [Required] UserRole Role, string Password, string ConfirmPassword);
```

`src/Application/Interfaces/IAuthService.cs` — acrescente, depois de `RegisterAsync`:

```csharp
    Task CreateUserAsync(CreateUserInputDto input);
```

- [ ] **Step 4: `AuthService`**

Em `src/Infrastructure/Identity/AuthService.cs`, acrescente `using TechCurse.Domain.Enums;` (ordenado) e substitua o método `RegisterAsync` inteiro por:

```csharp
    public async Task<bool> RegisterAsync(RegisterInputDto input)
    {
        await CriarUsuarioAsync(input.Name, input.Email, input.Password, input.ConfirmPassword, UserRole.Student);

        return true;
    }

    public async Task CreateUserAsync(CreateUserInputDto input)
    {
        await CriarUsuarioAsync(input.Name, input.Email, input.Password, input.ConfirmPassword, input.Role);
    }

    private async Task<IdentityUser> CriarUsuarioAsync(string nome, string email, string senha, string confirmacaoSenha, UserRole role)
    {
        if (senha != confirmacaoSenha)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Password", new[] { "A senha e a confirmação de senha não coincidem." } }
            });
        }

        var user = new IdentityUser { UserName = nome, Email = email };

        var result = await _userManager.CreateAsync(user, senha);

        if (result.Succeeded == false)
        {
            var errorList = new Dictionary<string, string[]>();

            foreach (var error in result.Errors)
            {
                if (errorList.TryGetValue(error.Code, out var existingErrors))
                {
                    errorList[error.Code] = existingErrors.Concat(new[] { error.Description }).ToArray();
                }
                else
                {
                    errorList.Add(error.Code, new[] { error.Description });
                }
            }

            throw new ValidationException(errorList);
        }

        await _userManager.AddToRoleAsync(user, role.ToString());

        return user;
    }
```

Se `ValidationException` e `Dictionary` já estavam importados, nada mais muda nos `using`.

- [ ] **Step 5: Action no `AuthController`**

Em `src/Api/Controllers/AuthController.cs`, acrescente `using Microsoft.AspNetCore.Authorization;` (ordenado) e, logo depois da action `Register`, a action nova:

```csharp
    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Cria um usuário com a role informada.",
        Description = "**Acesso:** Requer role de Admin."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Usuário criado com sucesso.", typeof(MensagemOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação nos campos enviados.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserInputDto input)
    {
        await _authService.CreateUserAsync(input);

        return StatusCode(201, new MensagemOutputDto("Usuário criado com sucesso."));
    }
```

Na action `Register`, troque `var actionResult = await _authService.RegisterAsync(input);` por `await _authService.RegisterAsync(input);` (a variável não era usada) e o `Description` do `SwaggerOperation` por `"**Acesso:** Público. O usuário é sempre criado com a role Student."`.

- [ ] **Step 6: Rodar e ver passar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthEndpointsTests"`
Expected: todos os testes de `AuthEndpointsTests` passando (os 6 existentes + 5 novos).

Se `SwaggerDocumentTests` ou algum teste de contrato comparar o schema do registro, rode também `dotnet test TechCurse.slnx` e ajuste só o que for consequência direta da remoção de `Role` do `RegisterInputDto`.

- [ ] **Step 7: Formatar, suíte completa e commit**

```bash
dotnet format TechCurse.slnx
dotnet format TechCurse.slnx --verify-no-changes
dotnet test TechCurse.slnx
git add src/Application/DTOs/AuthDTOs.cs src/Application/Interfaces/IAuthService.cs src/Infrastructure/Identity/AuthService.cs src/Api/Controllers/AuthController.cs tests/TechCurse.Api.IntegrationTests/Endpoints/AuthEndpointsTests.cs
git commit -m "fix: registro publico cria apenas alunos e rota de usuarios exclusiva de admin"
```

---

### Task 2: Admin semeado em `Development`

**Files:**
- Modify: `src/Infrastructure/Data/DbInitializer.cs`
- Test (create): `tests/TechCurse.Api.IntegrationTests/Persistence/DbInitializerTests.cs`

**Interfaces:**
- Consumes: `IHostEnvironment`, `IConfiguration`, `UserManager<IdentityUser>`, `RoleManager<IdentityRole>` resolvidos do `IServiceProvider` recebido por `SeedDataAsync`.
- Produces: `DbInitializer.SeedDataAsync(IServiceProvider)` com a mesma assinatura; chaves `Seed:Admin:Email` e `Seed:Admin:Password`.

- [ ] **Step 1: Escrever os testes que falham**

Crie `tests/TechCurse.Api.IntegrationTests/Persistence/DbInitializerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TechCurse.Infrastructure.Data;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Persistence;

public class DbInitializerTests
{
    private const string EmailAdmin = "admin.semeado@techcurse.dev";
    private const string SenhaAdmin = "SenhaForte@123";

    private sealed class AmbienteDeTeste : IHostEnvironment
    {
        public AmbienteDeTeste(string nome)
        {
            EnvironmentName = nome;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "TechCurse.Testes";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider CriarProvedor(string ambiente, Dictionary<string, string?> configuracao)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TechCurseContext>(options => options.UseInMemoryDatabase($"Seed_{Guid.NewGuid():N}"));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TechCurseContext>();
        services.AddSingleton<IHostEnvironment>(new AmbienteDeTeste(ambiente));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuracao).Build());
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ConfiguracaoCompleta(string senha = SenhaAdmin) => new()
    {
        ["Seed:Admin:Email"] = EmailAdmin,
        ["Seed:Admin:Password"] = senha
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenDevelopmentComConfiguracao_ShouldCriarAdmin()
    {
        await using var provedor = CriarProvedor(Environments.Development, ConfiguracaoCompleta());
        using var scope = provedor.CreateScope();

        await DbInitializer.SeedDataAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await userManager.FindByEmailAsync(EmailAdmin);
        admin.Should().NotBeNull();
        admin!.UserName.Should().Be("admin.semeado");
        (await userManager.GetRolesAsync(admin)).Should().BeEquivalentTo(new[] { "Admin" });
        (await userManager.CheckPasswordAsync(admin, SenhaAdmin)).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenForaDeDevelopment_ShouldNaoCriarAdmin()
    {
        await using var provedor = CriarProvedor(Environments.Production, ConfiguracaoCompleta());
        using var scope = provedor.CreateScope();

        await DbInitializer.SeedDataAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        (await userManager.FindByEmailAsync(EmailAdmin)).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenSemConfiguracao_ShouldNaoCriarAdmin_ENaoFalhar()
    {
        await using var provedor = CriarProvedor(Environments.Development, new Dictionary<string, string?>());
        using var scope = provedor.CreateScope();

        var acao = () => DbInitializer.SeedDataAsync(scope.ServiceProvider);

        await acao.Should().NotThrowAsync();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();
        (await context.Users.CountAsync()).Should().Be(0);
        (await context.Roles.CountAsync()).Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenExecutadoDuasVezes_ShouldNaoDuplicarNemAlterarAdmin()
    {
        await using var provedor = CriarProvedor(Environments.Development, ConfiguracaoCompleta());
        using (var primeiroEscopo = provedor.CreateScope())
        {
            await DbInitializer.SeedDataAsync(primeiroEscopo.ServiceProvider);
        }

        using var scope = provedor.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();
        var hashAntes = (await context.Users.SingleAsync(u => u.Email == EmailAdmin)).PasswordHash;

        await DbInitializer.SeedDataAsync(scope.ServiceProvider);

        (await context.Users.CountAsync(u => u.Email == EmailAdmin)).Should().Be(1);
        (await context.Users.AsNoTracking().SingleAsync(u => u.Email == EmailAdmin)).PasswordHash.Should().Be(hashAntes);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenSenhaForaDaPolitica_ShouldLancarComOsErrosDoIdentity()
    {
        await using var provedor = CriarProvedor(Environments.Development, ConfiguracaoCompleta("fraca"));
        using var scope = provedor.CreateScope();

        var acao = () => DbInitializer.SeedDataAsync(scope.ServiceProvider);

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Admin semeado*");
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~DbInitializerTests"`
Expected: FAIL — `SeedData_WhenDevelopmentComConfiguracao_ShouldCriarAdmin`, `..._WhenExecutadoDuasVezes_...` e `..._WhenSenhaForaDaPolitica_...` falham (o Admin não é criado); os dois testes de "não criar" podem passar já.

- [ ] **Step 3: Implementar o seed**

Substitua o conteúdo de `src/Infrastructure/Data/DbInitializer.cs` por:

```csharp
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
```

Mantenha o corpo do laço de roles exatamente como estava no arquivo original (se diferir do acima só em formatação, preserve o original).

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~DbInitializerTests"`
Expected: 5 testes passando.

Se `CheckPasswordAsync` ou a política de senha divergirem porque o `AddIdentityCore` de teste não aplica as opções do `IdentityAuthenticationSetup`, ajuste só o teste de senha fraca: `"fraca"` já falha na política padrão do Identity (menos de 6 caracteres e sem dígito), então nenhuma opção extra deve ser necessária.

- [ ] **Step 5: Formatar, suíte completa e commit**

```bash
dotnet format TechCurse.slnx
dotnet format TechCurse.slnx --verify-no-changes
dotnet test TechCurse.slnx
git add src/Infrastructure/Data/DbInitializer.cs tests/TechCurse.Api.IntegrationTests/Persistence/DbInitializerTests.cs
git commit -m "feat: semear admin de desenvolvimento a partir da configuracao"
```

---

### Task 3: Compose, `.env.example` e documentação

**Files:**
- Modify: `docker-compose.yml` (serviço `api`, lista `environment`)
- Modify: `.env.example`
- Modify: `README.md`, `CLAUDE.md`, `docs/postman_collection.json`, `CHANGELOG.md`

**Interfaces:**
- Consumes: chaves `Seed:Admin:Email`, `Seed:Admin:Password` (Task 2); rota `POST /tech-curse/Auth/users` e o corpo sem `role` do registro (Task 1).

- [ ] **Step 1: `docker-compose.yml`**

No serviço `api`, na lista `environment`, logo depois da linha `- ConnectionStrings__SeqUrl=http://seq:5341`, acrescente (mesma indentação das vizinhas):

```yaml
      - Seed__Admin__Email=${SEED_ADMIN_EMAIL:-}
      - Seed__Admin__Password=${SEED_ADMIN_PASSWORD:-}
```

- [ ] **Step 2: `.env.example`**

Acrescente, **antes** das duas últimas linhas comentadas (`# ASPNETCORE_ENVIRONMENT=...` e `# API_IMAGE=...`, que devem continuar sendo as duas últimas), um bloco separado por linha em branco:

```
SEED_ADMIN_EMAIL=admin@techcurse.dev
SEED_ADMIN_PASSWORD=TechCurse@Admin1
```

Confirme que continua havendo uma linha em branco antes das duas linhas comentadas finais.

- [ ] **Step 3: `README.md`**

1. Na tabela de endpoints, na linha `| **Auth** | \`POST\` | \`/tech-curse/Auth/register\` | Público | Registra novo usuário no Identity |`, troque a descrição por `Registra um novo aluno (a role é sempre Student)` e acrescente logo abaixo:

```
| **Auth** | `POST` | `/tech-curse/Auth/users` | Admin | Cria usuário com a role informada (`Admin`, `Instructor` ou `Student`) |
```

2. Logo depois do bloco que explica `dotnet user-secrets set "ConnectionStrings:APITechCurse" ...`, acrescente a seção:

````markdown
#### Admin de desenvolvimento

Em `Development`, a API cria um Admin no startup quando `Seed:Admin:Email` e `Seed:Admin:Password` estão configurados. A criação é idempotente (se o e-mail já existe, nada muda) e nunca acontece em outros ambientes. O registro público (`POST /tech-curse/Auth/register`) nunca cria Admin nem Instructor — use `POST /tech-curse/Auth/users` autenticado como Admin.

Rodando a API no host:

```bash
dotnet user-secrets set "Seed:Admin:Email" "admin@techcurse.dev" --project src/Api
```

```bash
dotnet user-secrets set "Seed:Admin:Password" "<senha forte>" --project src/Api
```

Pelo compose, preencha `SEED_ADMIN_EMAIL` e `SEED_ADMIN_PASSWORD` no `.env`.
````

- [ ] **Step 4: `CLAUDE.md`**

1. Na linha que começa com `- **Autorização**: RBAC por`, troque a frase final `As roles são criadas no startup pelo \`DbInitializer\`.` por:

```
As roles são criadas no startup pelo `DbInitializer`, que em `Development` também cria um Admin a partir de `Seed:Admin:Email`/`Seed:Admin:Password` (idempotente; nada fora de `Development`). A role de um usuário **nunca** vem do registro público: `POST /Auth/register` cria sempre `Student`, e só um Admin cria outras roles por `POST /Auth/users`.
```

2. No parágrafo que diz que **`DbInitializer.SeedDataAsync` nunca roda nos testes de integração**, acrescente ao final: `Os testes do seed chamam o método diretamente, com um provedor de serviços próprio (\`Persistence/DbInitializerTests.cs\`).`

- [ ] **Step 5: `docs/postman_collection.json`**

1. No corpo `raw` da requisição de registro, remova o trecho `\n  \"role\": \"Student\",` para que o JSON fique `{ name, email, password, confirmPassword }`.
2. Duplique o item da requisição de registro dentro da mesma pasta `Auth`, com: `name` = `"Criar usuário (Admin)"`, método `POST`, URL `raw` = `"{{baseUrl}}/tech-curse/Auth/users"` (e `path` correspondente, se o item original tiver `host`/`path` separados), cabeçalho `Authorization: Bearer {{token}}` no mesmo formato usado pelas requisições autenticadas da collection, e corpo `raw`:

```
"{\n  \"name\": \"InstrutorExemplo\",\n  \"email\": \"instrutor@techcurse.com\",\n  \"role\": \"Instructor\",\n  \"password\": \"SenhaForte1!\",\n  \"confirmPassword\": \"SenhaForte1!\"\n}"
```

Valide o JSON: `python -c "import json;json.load(open('docs/postman_collection.json',encoding='utf-8'))"` (ou `node -e "JSON.parse(require('fs').readFileSync('docs/postman_collection.json','utf8'))"`).

- [ ] **Step 6: `CHANGELOG.md`**

Na seção `## [Não lançado]`:

1. Na tabela de `### ⚠️ Breaking Changes`, acrescente a linha:

```
| `POST /Auth/register` não aceita mais `role`: o usuário é sempre criado como `Student` | Clientes que criavam Admin ou Instructor pelo registro passam a usar `POST /Auth/users` autenticados como Admin. O campo `role` enviado no registro é ignorado |
```

2. Acrescente, logo antes de `### 🔄 Alterado`, as seções (use as já existentes se houver `### 🔒 Segurança` ou `### ✨ Adicionado`):

```markdown
### 🔒 Segurança

- O registro público aceitava a role no corpo e permitia que qualquer pessoa se cadastrasse como `Admin`. Agora `POST /Auth/register` cria sempre `Student`.

### ✨ Adicionado

- `POST /Auth/users` (Admin): cria usuário com role `Admin`, `Instructor` ou `Student`.
- Admin semeado em `Development` a partir de `Seed:Admin:Email`/`Seed:Admin:Password` (user-secrets ou `SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD` no compose).
```

- [ ] **Step 7: Verificar e commit**

```bash
dotnet build TechCurse.slnx
docker compose config --quiet
git add docker-compose.yml .env.example README.md CLAUDE.md docs/postman_collection.json CHANGELOG.md
git commit -m "docs: documentar registro somente de aluno, rota de usuarios e admin semeado"
```

`docker compose config --quiet` só valida a sintaxe do compose (precisa de um `.env` presente; se não houver, pule e diga no relatório).

---

## Verificação final do PR

- [ ] `dotnet build TechCurse.slnx`, `dotnet test TechCurse.slnx` e `dotnet format TechCurse.slnx --verify-no-changes` limpos.
- [ ] Manual (controlador, fora dos subagentes): com `Seed:Admin:*` configurados em user-secrets, `docker-compose up -d db redis seq` + `dotnet run --project src/Api` → `/health/ready` 200; login com o Admin semeado retorna token; `POST /Auth/register` com `"role":"Admin"` cria usuário `Student`; `POST /Auth/users` com o token do Admin cria um `Instructor`.
- [ ] Os E2E do front (`npm run e2e` no `tech-curse-web`) continuam passando: o front já não envia `role` diferente de `Student`.
