using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Infrastructure.Data;
using TechCurse.Infrastructure.Repositories;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenValid_ShouldReturn201Created()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"new_auth_user_{Guid.NewGuid():N}@techcurse.com";
        var input = new RegisterInputDto("NovoUsuario", email, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/register", input);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenCredentialsValid_ShouldReturn200OK_WithToken()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"login_user_{Guid.NewGuid():N}@techcurse.com";
        var password = "SenhaForte@123";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Student");
            }
        }

        var input = new LoginInputDto(email, password);

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authOutput = await response.Content.ReadFromJsonAsync<AuthOutputDto>();
        authOutput.Should().NotBeNull();
        authOutput!.AccessToken.Should().NotBeNullOrEmpty();
        authOutput.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenPasswordInvalid_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateAnonymousClient();
        var input = new LoginInputDto("nonexistent@techcurse.com", "WrongPassword@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenCredentialsValid_ShouldPersistApenasOHashDoRefreshToken()
    {
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var authOutput = await response.Content.ReadFromJsonAsync<AuthOutputDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(email);

        var valorPersistido = await userManager.GetAuthenticationTokenAsync(user!, "JWTApp", "RefreshToken");

        valorPersistido.Should().NotBeNullOrEmpty();
        valorPersistido.Should().NotBe(authOutput!.RefreshToken);
        Convert.FromBase64String(valorPersistido!).Should().HaveCount(32);

        var expiracaoPersistida = await userManager.GetAuthenticationTokenAsync(user!, "JWTApp", "RefreshTokenExpiry");
        expiracaoPersistida.Should().NotBeNullOrEmpty();
        DateTimeOffset.Parse(expiracaoPersistida!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_WhenRefreshTokenValido_ShouldReturn200_ComTokensRotacionados()
    {
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var loginResponse = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthOutputDto>();

        var response = await client.PostAsJsonAsync(
            "/tech-curse/Auth/refresh",
            new RefreshTokenInputDto(login!.AccessToken, login.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await response.Content.ReadFromJsonAsync<AuthOutputDto>();
        refresh.Should().NotBeNull();
        refresh!.RefreshToken.Should().NotBeNullOrEmpty();
        refresh.RefreshToken.Should().NotBe(login.RefreshToken, "o refresh token é rotacionado a cada uso");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_WhenRefreshTokenNaoConfere_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var loginResponse = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthOutputDto>();

        var refreshTokenForjado = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var response = await client.PostAsJsonAsync(
            "/tech-curse/Auth/refresh",
            new RefreshTokenInputDto(login!.AccessToken, refreshTokenForjado));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string Email, string Senha)> CriarUsuarioAsync()
    {
        await _factory.EnsureRolesCreatedAsync();

        var email = $"refresh_user_{Guid.NewGuid():N}@techcurse.com";
        const string senha = "SenhaForte@123";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, senha);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Student");
        }

        return (email, senha);
    }

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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenRoleInexistente_ShouldReturn422_ENaoCriarUsuario()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"role_invalida_{Guid.NewGuid():N}@techcurse.com";
        var corpo = new
        {
            name = $"roleinvalida{Guid.NewGuid():N}",
            email,
            role = 99,
            password = "SenhaForte@123",
            confirmPassword = "SenhaForte@123"
        };

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("errors").TryGetProperty("Role", out _).Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenRoleAusente_ShouldReturn422_ComRole()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"role_ausente_{Guid.NewGuid():N}@techcurse.com";
        var corpo = new
        {
            name = $"roleausente{Guid.NewGuid():N}",
            email,
            password = "SenhaForte@123",
            confirmPassword = "SenhaForte@123"
        };

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("errors").TryGetProperty("Role", out _).Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenRoleComNomeDesconhecido_ShouldReturn400()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"role_desconhecida_{Guid.NewGuid():N}@techcurse.com";
        var corpo = new
        {
            name = $"roledesconhecida{Guid.NewGuid():N}",
            email,
            role = "SuperAdmin",
            password = "SenhaForte@123",
            confirmPassword = "SenhaForte@123"
        };

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", corpo);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().BeNull();
    }

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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenGravacaoDoPerfilFalhaNoBanco_ShouldApagarOUsuario()
    {
        await _factory.EnsureRolesCreatedAsync();
        var studentIdEmConflito = Random.Shared.Next(1_000_000, 2_000_000);
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var donoDoConflito = new IdentityUser { Id = $"conflito-{Guid.NewGuid():N}", UserName = $"conflito{Guid.NewGuid():N}", Email = $"conflito_{Guid.NewGuid():N}@techcurse.com" };
            context.Users.Add(donoDoConflito);
            context.Students.Add(new Student { StudentId = studentIdEmConflito, Nome = "Conflito", Email = $"conflito_perfil_{Guid.NewGuid():N}@techcurse.com", IdentityUserId = donoDoConflito.Id, DataCadastro = DateTime.UtcNow, IsDeleted = false });
            await context.SaveChangesAsync();
        });
        using var fabrica = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IStudentRepository>(sp => new StudentRepositoryComConflitoDeChave(sp.GetRequiredService<TechCurseContext>(), studentIdEmConflito))));
        var anonimo = fabrica.CreateClient();
        var email = $"conflito_registro_{Guid.NewGuid():N}@techcurse.com";

        var resposta = await anonimo.PostAsJsonAsync("/tech-curse/Auth/register", new RegisterInputDto($"conflito{Guid.NewGuid():N}", email, "SenhaForte@123", "SenhaForte@123"));

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var scope = fabrica.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull();
    }

    private sealed class StudentRepositoryComConflitoDeChave : IStudentRepository
    {
        private readonly StudentRepository _repositorioReal;
        private readonly int _studentIdEmConflito;

        public StudentRepositoryComConflitoDeChave(TechCurseContext context, int studentIdEmConflito)
        {
            _repositorioReal = new StudentRepository(context);
            _studentIdEmConflito = studentIdEmConflito;
        }

        public Task AddAsync(Student student)
        {
            student.StudentId = _studentIdEmConflito;
            return _repositorioReal.AddAsync(student);
        }

        public Task<(IEnumerable<Student> Items, int TotalCount)> GetPagedAsync(PaginationParamsDto searchParams)
            => _repositorioReal.GetPagedAsync(searchParams);

        public Task<IEnumerable<Student>> GetAllAsync()
            => _repositorioReal.GetAllAsync();

        public Task<Student?> GetByIdAsync(int id)
            => _repositorioReal.GetByIdAsync(id);

        public Task<Student?> GetByEmailAsync(string email)
            => _repositorioReal.GetByEmailAsync(email);

        public Task<IEnumerable<CourseStudentOutputDto>> GetCoursesAsync(Student student)
            => _repositorioReal.GetCoursesAsync(student);

        public Task UpdateAsync(Student student)
            => _repositorioReal.UpdateAsync(student);

        public Task DeleteAsync(Student student)
            => _repositorioReal.DeleteAsync(student);

        public Task<bool> EmailExistsAsync(string email)
            => _repositorioReal.EmailExistsAsync(email);

        public Task<bool> StudentIsActiveAsync(Student student)
            => _repositorioReal.StudentIsActiveAsync(student);
    }
}
