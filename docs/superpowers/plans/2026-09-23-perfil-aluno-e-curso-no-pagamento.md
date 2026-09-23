# PR B — Perfil de aluno no cadastro e curso no pagamento: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Todo cadastro de aluno já nasce com perfil `Student` (sem usuário órfão se algo falhar), e os contratos passam a ligar pagamento e matrícula ao curso.

**Architecture:** O `AuthService` ganha o `IStudentRepository` e cria o perfil depois do usuário, com compensação (apaga o usuário se a role ou o perfil falharem). Os DTOs ganham só campos novos, no final. A montagem do `PaymentOutputDto`, hoje repetida em cinco handlers, vai para um único mapeamento; as consultas de pagamento incluem `Enrollment.Course`; as chaves de cache de pagamento passam a um lugar só, com versão nova, para não servir o formato antigo.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core Identity, EF Core 10, MediatR, xUnit, FluentAssertions, Moq.

**Spec:** `docs/superpowers/specs/2026-09-23-ajustes-backend-portal-aluno-design.md` (seção "PR B"). Pontos herdados do PR A (#39): checar o `AddToRoleAsync` dentro da compensação; `RegisterAsync` passar a `Task`; tirar o `[Required]` sem efeito do `UserRole`.

## Global Constraints

- **Nenhum comentário em arquivo-fonte** (`.cs`, YAML, `.props`…). Markdown segue normal.
- pt-BR em mensagens de exceção, documentação e commits. Conventional Commits. **Sem** linhas `Co-Authored-By` ou assinatura de IA.
- Allman, `using` ordenados; `dotnet format TechCurse.slnx` e depois `dotnet format TechCurse.slnx --verify-no-changes` antes de cada commit. Sem pacotes novos.
- Branch: `feat/perfil-aluno-e-curso-no-pagamento` (já criado a partir de `main` em `3f721a5`).
- **Contratos só crescem:** nenhum campo removido ou renomeado. Campos novos entram no **final** dos records: `PaymentOutputDto(..., int CourseId, string CourseTitulo)` e `CourseStudentOutputDto(..., int EnrollmentId)`. JSON: `courseId`, `courseTitulo`, `enrollmentId`.
- Sem migrations: nenhuma tabela ou coluna muda.
- Testes de integração usam `CustomWebApplicationFactory` (EF em memória, banco compartilhado entre os testes da mesma classe): e-mails, nomes e títulos sempre únicos (`Guid.NewGuid():N`).
- Nomes de usuário do Identity não aceitam espaço.

---

## Mapa de arquivos

| Arquivo | Mudança |
|---|---|
| `src/Infrastructure/Identity/AuthService.cs` | cria o `Student`; compensação; `AddToRoleAsync` checado; `RegisterAsync` → `Task` |
| `src/Application/Interfaces/IAuthService.cs` | `RegisterAsync` → `Task` |
| `src/Application/DTOs/AuthDTOs.cs` | tira `[Required]` do `UserRole` |
| `src/Application/DTOs/StudentDTO.cs` | `CourseStudentOutputDto` + `EnrollmentId` |
| `src/Infrastructure/Repositories/StudentRepository.cs` | preenche `EnrollmentId` |
| `src/Application/DTOs/PaymentDTO.cs` | `PaymentOutputDto` + `CourseId`, `CourseTitulo` |
| `src/Application/Features/Payments/PaymentMapping.cs` (novo) | `Payment` → `PaymentOutputDto` |
| `src/Application/Features/Payments/ChavesDeCachePagamento.cs` (novo) | prefixos de cache (versão `v2`) |
| 7 handlers em `src/Application/Features/Payments/**` | usam o mapeamento e as chaves compartilhadas |
| `src/Infrastructure/Repositories/PaymentRepository.cs`, `EnrollmentRepository.cs` | `Include` do curso |
| `tests/TechCurse.Api.IntegrationTests/Endpoints/AuthEndpointsTests.cs` | perfil e compensação |
| `tests/TechCurse.Api.IntegrationTests/Endpoints/StudentsEndpointsTests.cs` | `enrollmentId` |
| `tests/TechCurse.Api.IntegrationTests/Endpoints/PaymentsEndpointsTests.cs` | `courseId`/`courseTitulo` |
| `tests/TechCurse.Application.UnitTests/Handlers/Payments/*.cs` + `DadosDePagamento.cs` (novo) | fixtures com curso; chaves de cache |
| `README.md`, `CLAUDE.md`, `CHANGELOG.md` | documentação |

---

### Task 1: Perfil de estudante no cadastro, com compensação

**Files:**
- Modify: `src/Infrastructure/Identity/AuthService.cs`
- Modify: `src/Application/Interfaces/IAuthService.cs`
- Modify: `src/Application/DTOs/AuthDTOs.cs`
- Modify: `src/Api/Controllers/AuthController.cs` (só se a assinatura de `RegisterAsync` exigir)
- Test: `tests/TechCurse.Api.IntegrationTests/Endpoints/AuthEndpointsTests.cs`

**Interfaces:**
- Consumes: `IStudentRepository.EmailExistsAsync(string)`, `IStudentRepository.AddAsync(Student)` (já existem); `ConflictException` (`TechCurse.Domain.Exceptions`).
- Produces: `IAuthService.RegisterAsync(RegisterInputDto): Task` (antes `Task<bool>`); `CreateUserInputDto(string Name, string Email, UserRole Role, string Password, string ConfirmPassword)` sem `[Required]`.

- [ ] **Step 1: Testes que falham**

Em `AuthEndpointsTests.cs`, acrescente os `using` necessários (ordenados): `Microsoft.AspNetCore.TestHost`, `Moq`, `TechCurse.Application.Interfaces`, `TechCurse.Domain.Entities`, `TechCurse.Infrastructure.Data` (os que ainda não existirem). Acrescente ao final da classe:

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenValido_ShouldCriarPerfilDeEstudante()
    {
        await _factory.EnsureRolesCreatedAsync();
        var anonimo = _factory.CreateAnonymousClient();
        var nome = $"perfil{Guid.NewGuid():N}";
        var email = $"perfil_{Guid.NewGuid():N}@techcurse.com";

        var registro = await anonimo.PostAsJsonAsync("/tech-curse/Auth/register", new RegisterInputDto(nome, email, "SenhaForte@123", "SenhaForte@123"));
        registro.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().NotBeNull();

        var aluno = _factory.CreateStudentClient(email, usuario!.Id);
        var resposta = await aluno.GetAsync("/tech-curse/Student/me");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var perfil = await resposta.Content.ReadFromJsonAsync<StudentOutputDto>();
        perfil!.Nome.Should().Be(nome);
        perfil.Email.Should().Be(email);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenRoleStudent_ShouldCriarPerfil_EInstructorNao()
    {
        await _factory.EnsureRolesCreatedAsync();
        var admin = _factory.CreateAdminClient();
        var emailAluno = $"aluno_admin_{Guid.NewGuid():N}@techcurse.com";
        var emailInstrutor = $"instrutor_admin_{Guid.NewGuid():N}@techcurse.com";

        (await admin.PostAsJsonAsync("/tech-curse/Auth/users", new CreateUserInputDto($"aluno{Guid.NewGuid():N}", emailAluno, UserRole.Student, "SenhaForte@123", "SenhaForte@123")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync("/tech-curse/Auth/users", new CreateUserInputDto($"instrutor{Guid.NewGuid():N}", emailInstrutor, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();
        (await context.Students.AnyAsync(s => s.Email == emailAluno)).Should().BeTrue();
        (await context.Students.AnyAsync(s => s.Email == emailInstrutor)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenJaExistePerfilComOEmail_ShouldReturn409_ENaoCriarUsuario()
    {
        await _factory.EnsureRolesCreatedAsync();
        var email = $"perfil_antigo_{Guid.NewGuid():N}@techcurse.com";
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var donoAntigo = new IdentityUser { Id = $"antigo-{Guid.NewGuid():N}", UserName = $"antigo{Guid.NewGuid():N}", Email = $"outro_{Guid.NewGuid():N}@techcurse.com" };
            context.Users.Add(donoAntigo);
            context.Students.Add(new Student { Nome = "Perfil Antigo", Email = email, IdentityUserId = donoAntigo.Id, DataCadastro = DateTime.UtcNow, IsDeleted = false });
            await context.SaveChangesAsync();
        });
        var anonimo = _factory.CreateAnonymousClient();

        var resposta = await anonimo.PostAsJsonAsync("/tech-curse/Auth/register", new RegisterInputDto($"novo{Guid.NewGuid():N}", email, "SenhaForte@123", "SenhaForte@123"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenCriacaoDoPerfilFalha_ShouldApagarOUsuario()
    {
        await _factory.EnsureRolesCreatedAsync();
        var repositorioQueFalha = new Mock<IStudentRepository>();
        repositorioQueFalha.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        repositorioQueFalha.Setup(r => r.AddAsync(It.IsAny<Student>())).ThrowsAsync(new InvalidOperationException("falha simulada ao gravar o perfil"));
        using var fabrica = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => repositorioQueFalha.Object)));
        var anonimo = fabrica.CreateClient();
        var email = $"compensacao_{Guid.NewGuid():N}@techcurse.com";

        var resposta = await anonimo.PostAsJsonAsync("/tech-curse/Auth/register", new RegisterInputDto($"compensacao{Guid.NewGuid():N}", email, "SenhaForte@123", "SenhaForte@123"));

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var scope = fabrica.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull();
    }
```

Se `ExecuteDbContextAsync` ou `TechCurseContext.Students` exigirem outro `using`, acrescente-o. Se `StudentOutputDto` não desserializar por nome (`nome`, `email`), confirme o formato do JSON de `/Student/me` no próprio teste antes de mudar qualquer coisa.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthEndpointsTests"`
Expected: FAIL — `Register_WhenValido_...` (404 no `/me`), `CreateUser_WhenRoleStudent_...` (sem perfil), `Register_WhenJaExistePerfil...` (201 em vez de 409) e `Register_WhenCriacaoDoPerfilFalha_...` (201 e usuário existe).

- [ ] **Step 3: `AuthService`**

1. Acrescente `using TechCurse.Domain.Entities;` e injete o repositório:

```csharp
    private readonly IStudentRepository _studentRepository;

    public AuthService(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ITokenService tokenService, IStudentRepository studentRepository)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _studentRepository = studentRepository;
    }
```

2. `RegisterAsync` passa a devolver `Task`:

```csharp
    public async Task RegisterAsync(RegisterInputDto input)
    {
        await CriarUsuarioAsync(input.Name, input.Email, input.Password, input.ConfirmPassword, UserRole.Student);
    }
```

3. Em `CriarUsuarioAsync`, logo depois da checagem de confirmação de senha e **antes** do `CreateAsync`, acrescente:

```csharp
        if (role == UserRole.Student && await _studentRepository.EmailExistsAsync(email))
        {
            throw new ConflictException("Já existe um perfil de estudante com este e-mail.");
        }
```

4. Substitua a linha `await _userManager.AddToRoleAsync(user, role.ToString());` (e o `return user;` que a segue) por:

```csharp
        try
        {
            var resultadoRole = await _userManager.AddToRoleAsync(user, role.ToString());

            if (!resultadoRole.Succeeded)
            {
                var erros = string.Join("; ", resultadoRole.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Não foi possível atribuir a role {role} ao usuário: {erros}");
            }

            if (role == UserRole.Student)
            {
                await _studentRepository.AddAsync(new Student
                {
                    Nome = nome,
                    Email = email,
                    IdentityUserId = user.Id,
                    DataCadastro = DateTime.UtcNow,
                    IsDeleted = false
                });
            }
        }
        catch
        {
            await _userManager.DeleteAsync(user);
            throw;
        }

        return user;
```

Se o `Student` tiver outras propriedades obrigatórias (ex.: navegação `IdentityUser` marcada como `required`), preencha-as seguindo o `CreateStudentCommandHandler` — ele atribui `IdentityUser = user` e `Enrollments = new List<Enrollment>()`; faça o mesmo.

- [ ] **Step 4: Interface, DTO e controller**

- `IAuthService`: `Task RegisterAsync(RegisterInputDto input);`
- `AuthDTOs.cs`: `public record CreateUserInputDto(string Name, string Email, UserRole Role, string Password, string ConfirmPassword);` — sem `[Required]`. Remova `using System.ComponentModel.DataAnnotations;` se deixar de ser usado no arquivo.
- `AuthController.Register`: já faz `await _authService.RegisterAsync(input);` sem usar retorno; ajuste só se não compilar.
- Se algum teste unitário ou mock usar `RegisterAsync` com `ReturnsAsync(true)`, ajuste para `Returns(Task.CompletedTask)`.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthEndpointsTests"`
Expected: todos passando (14 anteriores + 4 novos = 18). O teste de role ausente continua 422 (a falta de `[Required]` não muda nada: `Enum.IsDefined` rejeita o valor 0).

- [ ] **Step 6: Formatar, suíte completa e commit**

```bash
dotnet format TechCurse.slnx
dotnet format TechCurse.slnx --verify-no-changes
dotnet test TechCurse.slnx
git add src/Infrastructure/Identity/AuthService.cs src/Application/Interfaces/IAuthService.cs src/Application/DTOs/AuthDTOs.cs src/Api/Controllers/AuthController.cs tests/TechCurse.Api.IntegrationTests/Endpoints/AuthEndpointsTests.cs
git commit -m "feat: criar perfil de estudante no cadastro com compensacao em caso de falha"
```

(Inclua no `git add` só os arquivos que mudaram.)

---

### Task 2: `enrollmentId` nas matrículas do estudante

**Files:**
- Modify: `src/Application/DTOs/StudentDTO.cs`
- Modify: `src/Infrastructure/Repositories/StudentRepository.cs` (`GetCoursesAsync`)
- Test: `tests/TechCurse.Api.IntegrationTests/Endpoints/StudentsEndpointsTests.cs`

**Interfaces:**
- Produces: `public record CourseStudentOutputDto(int CourseId, string Titulo, string Descricao, string Categoria, bool MatriculaAtiva, int EnrollmentId);`

- [ ] **Step 1: Teste que falha**

Em `StudentsEndpointsTests.cs` (mesmo estilo e `using` do arquivo; acrescente `Microsoft.AspNetCore.Identity` e `TechCurse.Domain.Entities` se faltarem), acrescente:

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetEnrollments_WhenAdmin_ShouldIncluirEnrollmentId()
    {
        var studentId = 0;
        var enrollmentId = 0;
        var courseId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var usuario = new IdentityUser { Id = $"matriculas-{Guid.NewGuid():N}", UserName = $"matriculas{Guid.NewGuid():N}", Email = $"matriculas_{Guid.NewGuid():N}@techcurse.com" };
            context.Users.Add(usuario);
            var aluno = new Student { Nome = "Aluno Matriculas", Email = usuario.Email!, IdentityUserId = usuario.Id, DataCadastro = DateTime.UtcNow, IsDeleted = false };
            context.Students.Add(aluno);
            var curso = new Course { Titulo = $"Curso Matriculas {Guid.NewGuid():N}", Descricao = "Desc", Categoria = "Tech", CargaHoraria = 10, DataCriacao = DateTime.UtcNow };
            context.Courses.Add(curso);
            await context.SaveChangesAsync();
            var matricula = new Enrollment { StudentId = aluno.StudentId, CourseId = curso.CourseId, DataMatricula = DateTime.UtcNow, Status = true };
            context.Enrollments.Add(matricula);
            await context.SaveChangesAsync();
            studentId = aluno.StudentId;
            enrollmentId = matricula.EnrollmentId;
            courseId = curso.CourseId;
        });
        var admin = _factory.CreateAdminClient();

        var resposta = await admin.GetAsync($"/tech-curse/Student/{studentId}/enrollments");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var matriculas = await resposta.Content.ReadFromJsonAsync<List<CourseStudentOutputDto>>();
        matriculas.Should().ContainSingle();
        matriculas![0].EnrollmentId.Should().Be(enrollmentId);
        matriculas[0].CourseId.Should().Be(courseId);
    }
```

Se a classe usar outro nome de campo para a fábrica, adapte.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~StudentsEndpointsTests"`
Expected: FAIL de compilação (`EnrollmentId` não existe).

- [ ] **Step 3: Implementar**

- `StudentDTO.cs`: `public record CourseStudentOutputDto(int CourseId, string Titulo, string Descricao, string Categoria, bool MatriculaAtiva, int EnrollmentId);`
- `StudentRepository.GetCoursesAsync`: acrescente `e.EnrollmentId` como último argumento do `new CourseStudentOutputDto(...)`.
- Corrija qualquer outro construtor desse record que o compilador apontar (testes incluídos), acrescentando o último argumento.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~StudentsEndpointsTests"`
Expected: todos passando.

- [ ] **Step 5: Formatar, suíte completa e commit**

```bash
dotnet format TechCurse.slnx
dotnet format TechCurse.slnx --verify-no-changes
dotnet test TechCurse.slnx
git add src/Application/DTOs/StudentDTO.cs src/Infrastructure/Repositories/StudentRepository.cs tests/TechCurse.Api.IntegrationTests/Endpoints/StudentsEndpointsTests.cs
git commit -m "feat: incluir enrollmentId nas matriculas do estudante"
```

---

### Task 3: Curso no pagamento, mapeamento único e chaves de cache compartilhadas

**Files:**
- Create: `src/Application/Features/Payments/PaymentMapping.cs`
- Create: `src/Application/Features/Payments/ChavesDeCachePagamento.cs`
- Modify: `src/Application/DTOs/PaymentDTO.cs`
- Modify: `src/Application/Features/Payments/Commands/CreatePayment/CreatePaymentCommandHandler.cs`, `.../ProcessPayment/ProcessPaymentCommandHandler.cs`, `.../RefundPayment/RefundPaymentCommandHandler.cs`, `.../Queries/GetPaymentById/GetPaymentByIdQueryHandler.cs`, `.../GetPayments/GetPaymentsQueryHandler.cs`, `.../GetPaymentsByEnrollmentId/GetPaymentsByEnrollmentIdQueryHandler.cs`, `.../GetPaymentsByStudentId/GetPaymentsByStudentIdQueryHandler.cs`
- Modify: `src/Infrastructure/Repositories/PaymentRepository.cs`, `src/Infrastructure/Repositories/EnrollmentRepository.cs`
- Create: `tests/TechCurse.Application.UnitTests/Handlers/Payments/DadosDePagamento.cs`
- Modify: `tests/TechCurse.Application.UnitTests/Handlers/Payments/*.cs`
- Test: `tests/TechCurse.Api.IntegrationTests/Endpoints/PaymentsEndpointsTests.cs`

**Interfaces:**
- Produces:
  - `public record PaymentOutputDto(int PaymentId, int EnrollmentId, int StudentId, decimal Amount, PaymentStatus Status, bool IsActive, DateTime CreatedAt, DateTime? PaidAt, string? ExternalTransactionId, int CourseId, string CourseTitulo);`
  - `PaymentMapping.ParaDto(this Payment payment): PaymentOutputDto` (namespace `TechCurse.Application.Features.Payments`) — exige `payment.Enrollment.Course` carregado.
  - `ChavesDeCachePagamento.Lista = "payments:v2:list:"`, `.Item = "payments:v2:item:"`, `.PorEstudante = "payments:v2:student:"`, `.PorMatricula = "payments:v2:enrollment:"`.
  - `IEnrollmentRepository.GetByIdAsync(int)` passa a trazer `Course` (mesma assinatura).

- [ ] **Step 1: Teste de integração que falha**

Em `PaymentsEndpointsTests.cs` acrescente (reaproveitando o padrão de seed do arquivo):

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Consultas_ShouldIncluirCursoDoPagamento()
    {
        var userId = $"curso-pag-{Guid.NewGuid():N}";
        var email = $"curso_pag_{Guid.NewGuid():N}@techcurse.com";
        var titulo = $"Curso do Pagamento {Guid.NewGuid():N}";
        int paymentId = 0, studentId = 0, enrollmentId = 0, courseId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = $"cursopag{Guid.NewGuid():N}", NormalizedEmail = email.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student { Nome = "Aluno Curso Pagamento", Email = email, IdentityUserId = userId, IsDeleted = false, DataCadastro = DateTime.UtcNow };
            context.Students.Add(student);
            var course = new Course { Titulo = titulo, Descricao = "Desc", Categoria = "Tech", CargaHoraria = 20, DataCriacao = DateTime.UtcNow };
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            var enrollment = new Enrollment { StudentId = student.StudentId, CourseId = course.CourseId, DataMatricula = DateTime.UtcNow, Status = true };
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();
            var payment = new Payment { EnrollmentId = enrollment.EnrollmentId, StudentId = student.StudentId, Amount = 99.90m, Status = PaymentStatus.Pending, IsActive = true, CreatedAt = DateTime.UtcNow };
            context.Payments.Add(payment);
            await context.SaveChangesAsync();
            paymentId = payment.PaymentId;
            studentId = student.StudentId;
            enrollmentId = enrollment.EnrollmentId;
            courseId = course.CourseId;
        });
        var admin = _factory.CreateAdminClient();

        var porId = await admin.GetFromJsonAsync<PaymentOutputDto>($"/tech-curse/Payment/{paymentId}");
        var porEstudante = await admin.GetFromJsonAsync<PagedResultDto<PaymentOutputDto>>($"/tech-curse/Payment/student/{studentId}");
        var porMatricula = await admin.GetFromJsonAsync<List<PaymentOutputDto>>($"/tech-curse/Payment/enrollment/{enrollmentId}");

        porId!.CourseId.Should().Be(courseId);
        porId.CourseTitulo.Should().Be(titulo);
        porEstudante!.Items.Should().ContainSingle(p => p.PaymentId == paymentId && p.CourseId == courseId && p.CourseTitulo == titulo);
        porMatricula.Should().ContainSingle(p => p.PaymentId == paymentId && p.CourseId == courseId && p.CourseTitulo == titulo);
    }
```

Se `PagedResultDto<T>` não desserializar (ele só tem getters e um construtor), use `JsonDocument` para ler `items` e compare `courseId`/`courseTitulo` do item com `paymentId` — veja `Contracts/PaymentsPagedResponseTests.cs`, que já lê essa resposta, e copie a técnica.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PaymentsEndpointsTests"`
Expected: FAIL de compilação (`CourseId`/`CourseTitulo` não existem).

- [ ] **Step 3: DTO, mapeamento e chaves de cache**

`PaymentDTO.cs` — o record passa a:

```csharp
public record PaymentOutputDto(int PaymentId, int EnrollmentId, int StudentId, decimal Amount, PaymentStatus Status, bool IsActive, DateTime CreatedAt, DateTime? PaidAt, string? ExternalTransactionId, int CourseId, string CourseTitulo);
```

`src/Application/Features/Payments/PaymentMapping.cs`:

```csharp
using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Features.Payments;

public static class PaymentMapping
{
    public static PaymentOutputDto ParaDto(this Payment payment)
    {
        return new PaymentOutputDto(
            payment.PaymentId,
            payment.EnrollmentId,
            payment.StudentId,
            payment.Amount,
            payment.Status,
            payment.IsActive,
            payment.CreatedAt,
            payment.PaidAt,
            payment.ExternalTransactionId,
            payment.Enrollment.CourseId,
            payment.Enrollment.Course.Titulo);
    }
}
```

`src/Application/Features/Payments/ChavesDeCachePagamento.cs`:

```csharp
namespace TechCurse.Application.Features.Payments;

public static class ChavesDeCachePagamento
{
    public const string Lista = "payments:v2:list:";
    public const string Item = "payments:v2:item:";
    public const string PorEstudante = "payments:v2:student:";
    public const string PorMatricula = "payments:v2:enrollment:";
}
```

- [ ] **Step 4: Handlers**

Nos sete handlers de pagamento:

1. Remova as constantes privadas `PAYMENT_LIST_PREFIX`, `PAYMENT_ITEM_PREFIX`, `PAYMENT_BY_STUDENT_PREFIX`, `PAYMENT_BY_ENROLLMENT_PREFIX` e use `ChavesDeCachePagamento.Lista`, `.Item`, `.PorEstudante`, `.PorMatricula` nos mesmos lugares (montagem de chave, `GetAsync`, `SetAsync`, `RemoveByPrefixAsync`).
2. Nos cinco que montam o DTO, troque `new PaymentOutputDto(...)` por `.ParaDto()`:
   - `GetPaymentByIdQueryHandler`: `var dto = payment.ParaDto();`
   - `GetPaymentsQueryHandler`, `GetPaymentsByStudentIdQueryHandler`, `GetPaymentsByEnrollmentIdQueryHandler`: `items.Select(p => p.ParaDto())` (ou `payments.Select(...)`, conforme o nome da variável), mantendo o `.ToList()` que já existir.
   - `CreatePaymentCommandHandler`: na criação do `Payment` novo, acrescente `Enrollment = enrollment` ao inicializador; no retorno, `return newPayment.ParaDto();`.
3. Acrescente `using TechCurse.Application.Features.Payments;` onde o namespace do handler não o cobrir.

- [ ] **Step 5: Repositórios**

`PaymentRepository` — em `GetByIdAsync`, `GetByStudentIdAsync`, `GetPagedAsync` e `GetByEnrollmentIdAsync`, acrescente à consulta (antes do `Where`/`OrderBy`, depois de `AsNoTracking`/`IgnoreQueryFilters` se houver):

```csharp
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Course)
```

Em `GetByEnrollmentIdAsync`, que já tem `.Include(p => p.Enrollment).ThenInclude(e => e.Student)`, acrescente um segundo `.Include(p => p.Enrollment).ThenInclude(e => e.Course)`. Em `GetByIdAsync`, mantenha o `.Include(p => p.Student)` existente.

`EnrollmentRepository.GetByIdAsync`:

```csharp
    public async Task<Enrollment?> GetByIdAsync(int id)
        => await _context.Enrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.EnrollmentId == id);
```

Se `ProcessPaymentCommandHandler` ou `RefundPaymentCommandHandler` usarem `PaymentRepository.GetByIdAsync` e depois `UpdateAsync` na mesma entidade, confirme que o `Include` não quebra a atualização (a consulta é `AsNoTracking`, então a entidade chega destacada como antes).

- [ ] **Step 6: Testes unitários**

1. Crie `tests/TechCurse.Application.UnitTests/Handlers/Payments/DadosDePagamento.cs` (namespace igual ao dos testes da pasta):

```csharp
using TechCurse.Domain.Entities;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public static class DadosDePagamento
{
    public const int CursoId = 7;
    public const string CursoTitulo = "Curso de Teste";

    public static Enrollment MatriculaComCurso(int enrollmentId = 1, int studentId = 2)
    {
        return new Enrollment
        {
            EnrollmentId = enrollmentId,
            StudentId = studentId,
            CourseId = CursoId,
            Course = new Course
            {
                CourseId = CursoId,
                Titulo = CursoTitulo,
                Descricao = "Descrição",
                Categoria = "Tech",
                CargaHoraria = 10
            }
        };
    }
}
```

(Ajuste o namespace ao usado pelos arquivos vizinhos.)

2. Em cada teste unitário de pagamento que monta um `Payment` retornado por mock e chega a montar o DTO, acrescente `Enrollment = DadosDePagamento.MatriculaComCurso(...)` ao inicializador (com o `EnrollmentId` coerente com o do `Payment`). Onde o teste já monta `Enrollment = new Enrollment { ... }` (ex.: `GetPaymentsByEnrollmentIdQueryHandlerTests`), acrescente a propriedade `Course` com os mesmos valores do helper ou troque pelo helper.
3. Em `CreatePaymentCommandHandlerTests`, troque `new Enrollment { EnrollmentId = 1, StudentId = 2, CourseId = 3 }` por `DadosDePagamento.MatriculaComCurso(1, 2)`.
4. Troque as strings de chave de cache nos testes (`"payments:list:"`, `"payments:item:1"`, `"payments:enrollment:1"`…) pelas constantes: `ChavesDeCachePagamento.Lista`, `$"{ChavesDeCachePagamento.Item}1"`, `$"{ChavesDeCachePagamento.PorMatricula}1"`.
5. Em pelo menos um teste de consulta (ex.: `GetPaymentByIdQueryHandlerTests`, caso de cache miss), acrescente as asserções `resultado.CourseId.Should().Be(DadosDePagamento.CursoId)` e `resultado.CourseTitulo.Should().Be(DadosDePagamento.CursoTitulo)`; e em `CreatePaymentCommandHandlerTests` (caso de sucesso), as mesmas duas asserções.

- [ ] **Step 7: Rodar e ver passar**

```bash
dotnet test tests/TechCurse.Application.UnitTests/TechCurse.Application.UnitTests.csproj --filter "FullyQualifiedName~Payments"
dotnet test tests/TechCurse.Api.IntegrationTests/TechCurse.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Payment"
```

Expected: todos passando, incluindo `Consultas_ShouldIncluirCursoDoPagamento`, `PaymentNavigationTests` e `PaymentsPagedResponseTests`.

- [ ] **Step 8: Formatar, suíte completa e commit**

```bash
dotnet format TechCurse.slnx
dotnet format TechCurse.slnx --verify-no-changes
dotnet test TechCurse.slnx
git add src/Application src/Infrastructure/Repositories tests
git commit -m "feat: incluir curso no pagamento e centralizar mapeamento e chaves de cache"
```

Confira com `git status` que só entraram arquivos de pagamento, repositórios citados e testes.

---

### Task 4: Documentação

**Files:**
- Modify: `README.md`, `CLAUDE.md`, `CHANGELOG.md`

- [ ] **Step 1: `README.md`**

- Na tabela de endpoints, na linha de `POST /tech-curse/Auth/register`, a descrição passa a `Registra um novo aluno e já cria o perfil de estudante (a role é sempre Student)`; na linha de `POST /tech-curse/Auth/users`, acrescente `; com role Student, também cria o perfil`.
- Onde o README descreve o fluxo de uso (o passo que manda usar `POST /tech-curse/auth/register`), acrescente que o aluno registrado já pode usar `/Student/me` e se matricular, sem passo manual de Admin.
- Se o README listar os campos de resposta de pagamento ou de matrículas do estudante, acrescente `courseId`, `courseTitulo` e `enrollmentId`; se não listar, não crie seção nova.

- [ ] **Step 2: `CLAUDE.md`**

Acrescente, perto da linha de "Autorização" (que já fala do registro e do `/Auth/users`):

```
O cadastro de um `Student` (pelo registro público ou pelo `/Auth/users`) cria também a entidade `Student` ligada ao usuário. A criação é compensada, não transacional — o provider em memória dos testes não suporta transação: se a atribuição da role ou a gravação do perfil falharem, o `AuthService` apaga o usuário recém-criado e relança o erro. E-mail que já tem perfil de estudante responde 409 antes de criar o usuário.
```

E, onde o `CLAUDE.md` fala de cache (se falar), acrescente: `As chaves de cache de pagamento ficam em ChavesDeCachePagamento (prefixo payments:v2:); mudar o formato do PaymentOutputDto exige subir a versão do prefixo.` Se não houver trecho sobre cache, coloque essa frase logo após o parágrafo acima.

- [ ] **Step 3: `CHANGELOG.md`**

Na seção `## [Não lançado]`, em `### ✨ Adicionado` (já existe desde o PR A), acrescente:

```markdown
- O cadastro de aluno (`POST /tech-curse/Auth/register`, e `POST /tech-curse/Auth/users` com role `Student`) cria o perfil de estudante automaticamente; se a gravação falhar, o usuário é removido. E-mail que já tem perfil responde 409.
- `PaymentOutputDto` ganha `courseId` e `courseTitulo`; `CourseStudentOutputDto` (matrículas do estudante) ganha `enrollmentId`.
```

E em `### 🔄 Alterado`:

```markdown
- Montagem do `PaymentOutputDto` centralizada em `PaymentMapping`; prefixos de cache de pagamento centralizados em `ChavesDeCachePagamento` e versionados (`payments:v2:`), para não servir respostas no formato antigo.
- Usuários `Student` criados antes desta versão continuam sem perfil; o Admin cria pelo `POST /tech-curse/Student`.
```

- [ ] **Step 4: Verificar e commit**

```bash
dotnet build TechCurse.slnx
git add README.md CLAUDE.md CHANGELOG.md
git commit -m "docs: documentar perfil de aluno no cadastro e curso no pagamento"
```

---

## Verificação final do PR

- [ ] `dotnet build TechCurse.slnx`, `dotnet test TechCurse.slnx` e `dotnet format TechCurse.slnx --verify-no-changes` limpos.
- [ ] Manual (controlador): API no host contra o Postgres local; registrar aluno → login → `GET /Student/me` 200; matricular (`POST /Enrollment`) → `GET /Student/{id}/enrollments` traz `enrollmentId`; com o Admin semeado, `POST /Payment` para essa matrícula → `GET /Payment/student/{id}` traz `courseId`/`courseTitulo`. Respeitar a cota de `/Auth` (10 por 60 s por IP).
- [ ] E2E do front (`npm run e2e` no `tech-curse-web`) passando contra esta API.
