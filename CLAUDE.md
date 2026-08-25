# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> O projeto é documentado em **português brasileiro**. Mantenha commits, mensagens de exceção e documentação em pt-BR.

> **Não escreva comentários no código.** Ao criar ou reescrever qualquer arquivo, não inclua comentário de nenhum tipo — nem de rodapé, nem XML doc, nem explicação de decisão de design. A justificativa de uma escolha vai na resposta da conversa ou na mensagem de commit, nunca dentro do arquivo-fonte. Isto vale também para instruções passadas a subagentes. Documentação em arquivos `.md` segue normalmente.

## Visão geral

API REST em .NET 10 / C# 14 para uma plataforma de cursos (cursos, estudantes, matrículas e pagamentos), estruturada em Clean Architecture + CQRS com Vertical Slices (MediatR), SQL Server (EF Core 10), Redis, Serilog/Seq e autenticação JWT com ASP.NET Core Identity.

Material de apoio (diagrama de arquitetura, collection do Postman) fica em `docs/`.

## Comandos

Todos executados na raiz do repositório. A solution é `TechCurse.slnx` (formato slnx, não `.sln`). Versões de pacote são gerenciadas centralmente em `Directory.Packages.props` (Central Package Management) — os `.csproj` só declaram `<PackageReference Include="..." />` sem `Version`; propriedades comuns (`TargetFramework`, `Nullable`, `ImplicitUsings`, etc.) vêm de `Directory.Build.props` na raiz, com um `tests/Directory.Build.props` adicional para o toolchain xUnit dos quatro projetos de teste.

```bash
dotnet build TechCurse.slnx
```

```bash
dotnet test TechCurse.slnx --logger "console;verbosity=normal"
```

Rodar apenas um projeto de teste ou um teste específico:

```bash
dotnet test tests/TechCurse.Application.UnitTests/TechCurse.Application.UnitTests.csproj --filter "FullyQualifiedName~ProcessPaymentCommandHandlerTests"
```

```bash
dotnet test TechCurse.slnx --filter "Category=Unit"
```

Subir a stack completa (API + SQL Server + Redis + Seq). Requer `.env` — copie de `.env.example`:

```bash
docker-compose up -d --build
```

Rodar só as dependências e a API no host (Swagger em http://localhost:5130/swagger, liveness em `/health/live`, readiness em `/health/ready`):

```bash
docker-compose up -d db redis seq
```

```bash
dotnet run --project src/Api
```

Criar migration do EF Core (o DbContext vive em Infrastructure, o host em API):

```bash
dotnet ef migrations add NomeDaMigration --project src/Infrastructure --startup-project src/Api
```

## Arquitetura

### Camadas e dependências

Quatro projetos em `src/`, no estilo do template `dotnet new ca-sln` (pasta com o nome curto da camada, `.csproj` com o nome completo). Nome da pasta ≠ nome do projeto, mas `.csproj` = `AssemblyName` = `RootNamespace` = `TechCurse.<Camada>`:

| Pasta | Projeto | Namespace raiz |
| --- | --- | --- |
| `src/Domain/` | `TechCurse.Domain.csproj` | `TechCurse.Domain` |
| `src/Application/` | `TechCurse.Application.csproj` | `TechCurse.Application` |
| `src/Infrastructure/` | `TechCurse.Infrastructure.csproj` | `TechCurse.Infrastructure` |
| `src/Api/` | `TechCurse.Api.csproj` | `TechCurse.Api` |


- **Domain** — entidades, enums, exceções de domínio e `ISpecification<T>`. Zero dependências de outras camadas e nenhuma referência a EF Core ou ASP.NET MVC.
- **Application** — CQRS por Vertical Slices em `Features/<Recurso>/{Commands,Queries}/<Operacao>/`, cada pasta com `Command`/`Query` + `Handler` + `Validator`. Também abriga DTOs, `Interfaces/` (contratos de repositório, cache, gateway, usuário atual), a factory e as strategies de pagamento. Depende apenas de Domain.
- **Infrastructure** — `TechCurseContext`, repositórios, Redis, Identity/JWT e o adaptador de gateway de pagamento. Depende de Application (implementa suas interfaces).
- **API** — controllers finos, middlewares e os `Configuration/*Setup.cs` (extension methods de DI: Serilog, EF Core, Identity/JWT, Redis, Swagger).

Essas regras não são só convenção: o projeto `TechCurse.ArchitectureTests` as verifica com NetArchTest e **quebra a suíte de testes** se violadas.

### Fluxo de uma requisição

Controller (só `IMediator`) → `ExceptionHandlingMiddleware` → `CorrelationIdMiddleware` → MediatR → `ValidationBehavior<,>` → Handler → Repositório/Cache/Gateway.

Pontos que se repetem em todo o código:

- **Erros**: handlers lançam exceções de `TechCurse.Domain.Exceptions`; o `ExceptionHandlingMiddleware` as traduz em `ProblemDetails` com o status HTTP correspondente (`NotFoundException`→404, `ConflictException`/`NotAllowedException`→409, `ValidationException`→422, `GatewayTimeoutException`→504, etc.). Não retorne status de erro manualmente no controller.
- **Validação**: `ValidationBehavior` roda todos os `AbstractValidator<T>` registrados por assembly scanning e converte falhas em `ValidationException` (422). Basta criar o validator na mesma pasta da slice — não há registro manual.
- **Cache**: queries leem/gravam em Redis via `ICacheService` com chaves prefixadas (`payments:list:`, `payments:item:`, `payments:student:`, `payments:enrollment:`, …). Todo command que muta dados chama `RemoveByPrefixAsync` para cada prefixo afetado — ao adicionar uma nova query com cache, adicione a invalidação correspondente nos commands.
- **Idempotência**: endpoints de escrita de pagamento usam `[TypeFilter(typeof(IdempotencyFilterMiddleware))]`; o header `Idempotency-Key` é **obrigatório** (ausência → 400) e a resposta é replayed do Redis por 6 minutos.
- **Autorização**: RBAC por `[Authorize(Roles = "Admin|Instructor|Student")]` no controller; regras de "é o próprio usuário" ficam nos handlers via `ICurrentUserService`. As roles são criadas no startup pelo `DbInitializer`.
- **Pagamentos**: `PaymentStrategyFactory` resolve a `IPaymentStrategy` pelo `PaymentMethodType`; a elegibilidade é checada por `PaymentProcessableSpecification` antes de chamar o `IPaymentGatewayAdapter`.
- **Soft delete**: `Student` tem global query filter (`!s.IsDeleted`) no `OnModelCreating`. Consultas de `Payment` que usam navegação chamam `IgnoreQueryFilters()` — ver a seção "Soft delete: decisão tomada".
- **Rate limiting**: `RateLimitingSetup` registra um limiter global (por usuário autenticado, ou por IP quando anônimo) e a política nomeada `RateLimitingSetup.PoliticaAutenticacao`, aplicada ao `AuthController` via `[EnableRateLimiting]`. O `UseRateLimiter()` fica entre `UseAuthentication()` e `UseAuthorization()`, e a rejeição devolve `ProblemDetails` 429 no mesmo formato do `ExceptionHandlingMiddleware`.
- **Refresh token**: o `AuthService` persiste em `AspNetUserTokens` o **SHA-256** do refresh token (`JWTApp`/`RefreshToken`) e a expiração ISO-8601 (`JWTApp`/`RefreshTokenExpiry`). A comparação no `/refresh` é feita em tempo constante por `ITokenService.RefreshTokenMatches`. O valor em texto puro só existe na resposta HTTP.
- **Data Protection**: o chaveiro é persistido no banco (`TechCurseContext : IDataProtectionKeyContext`, tabela `DataProtectionKeys`), com `SetApplicationName` fixo — nada de chaves efêmeras no filesystem do container.
- **Health checks**: `/health/live` é liveness — nenhum check roda (`Predicate = _ => false`), responde texto puro. `/health/ready` é readiness — agrega só os checks marcados com `HealthCheckTags.Ready` (`src/Api/Configuration/HealthCheckTags.cs`) e devolve **apenas o status agregado** para quem não é Admin; o JSON detalhado por verificação (nome, duração, descrição e mensagem de erro de cada check) só sai para quem chega com JWT de role `Admin`, porque a mensagem de erro carrega endereço e nome de servidor. A distinção é feita no `ResponseWriter` via `context.User.IsInRole("Admin")`, e não com `RequireAuthorization()`, porque sonda de readiness — do pipeline ou de orquestrador — não autentica. Ao registrar uma nova dependência crítica em um `*Setup.cs`, passe `tags: [HealthCheckTags.Ready]`; sem a tag o check não aparece em endpoint nenhum. O smoke test do pipeline sonda `/health/ready`.

### Configuração

Configuração vem de variáveis de ambiente / connection strings, não de `appsettings.json` (que só tem logging):

- `ConnectionStrings:APITechCurse`, `ConnectionStrings:RedisCache`, `ConnectionStrings:SeqUrl`
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (mínimo 32 caracteres — o startup lança exceção se for menor)
- `Jwt:RefreshTokenDays` — validade do refresh token (padrão 7)
- `RateLimiting:Enabled` (padrão `true`), `RateLimiting:GlobalPermitLimit` (200), `RateLimiting:GlobalWindowSeconds` (60), `RateLimiting:AuthPermitLimit` (10), `RateLimiting:AuthWindowSeconds` (60)
- `UseInMemoryDatabase=true` faz o `EFCoreSetup` pular o registro do SQL Server; é o gancho usado pelos testes de integração.

No Docker Compose essas chaves chegam como `Jwt__SigningKey`, `ConnectionStrings__APITechCurse`, etc., alimentadas pelo `.env`.

## Testes

Quatro projetos em `tests/`, todos xUnit + FluentAssertions. Cada um referencia só a camada que exercita — se um teste novo pede uma referência a mais, é sinal de que ele está no projeto errado:

- `TechCurse.Domain.UnitTests` — entidades e `ISpecification<T>` sem nenhum mock. Referencia apenas `TechCurse.Domain`.
- `TechCurse.Application.UnitTests` — handlers e validators isolados com **Moq** (`Mock<IPaymentRepository>` etc.), montando o handler no construtor da classe de teste. Padrão de nome: `Handle_WhenX_ShouldY`, com `[Trait("Category", "Unit")]`. Referencia `TechCurse.Application` e `TechCurse.Domain` — **não** referencia Infrastructure nem Api.
- `TechCurse.Api.IntegrationTests` — `CustomWebApplicationFactory : WebApplicationFactory<Program>` (por isso existe `public partial class Program { }` no fim do `Program.cs`). Usa EF InMemory, `InMemoryTestCacheService` no lugar do Redis e helpers `CreateAdminClient()` / `CreateStudentClient()` / `CreateInstructorClient()` que já injetam um JWT válido. `Persistence/` abriga os testes que batem direto no `TechCurseContext`, sem subir o host HTTP.
- `TechCurse.ArchitectureTests` — NetArchTest sobre os assemblies referenciados em `Common/ArchitectureConstants.cs`. Impõe: isolamento de camadas, handlers/validators em `TechCurse.Application.Features.*`, sufixos `Handler`/`Validator`/`Controller`/`Repository`, repositórios em `Infrastructure.Repositories`, e controllers sem dependência de `TechCurseContext` nem de repositórios (só `IMediator`).

Ao criar uma nova slice, o caminho completo é: `Command`/`Query` + `Handler` + `Validator` na pasta da feature → interface de repositório em `Application/Interfaces` → implementação em `Infrastructure/Repositories` (registrada em `Infrastructure/DependencyInjection.cs`) → action no controller com `SwaggerOperation`/`SwaggerResponse` → testes unitários do handler e do validator.

Pastas do projeto de integração e o que cada uma protege:

| Pasta | Cobre |
| --- | --- |
| `Endpoints/` | rotas HTTP ponta a ponta, incluindo `PaymentNavigationTests` (as navegações que já estouraram `NullReferenceException`) e `HealthEndpointTests` (liveness, readiness e o corte de detalhe por role) |
| `Middlewares/` | correlation id, tratamento de exceção e idempotência |
| `Persistence/` | `TechCurseContext` direto, sem host HTTP — inclui `MigrationDriftTests`, que falha se uma entidade mudar sem migration correspondente |
| `Composition/` | montagem do container e do host: o abort em Production sem gateway real, e a recusa de subir quando o banco relacional está inalcançável |
| `Fixtures/` | factories e dublês |

## Armadilhas conhecidas

- **Migrations vivem em `src/Infrastructure/Migrations/`** (namespace `TechCurse.Infrastructure.Migrations`), mesmo assembly do `TechCurseContext` — por isso não é preciso configurar `MigrationsAssembly()`. O EF localiza migrations pelos atributos `[DbContext]`/`[Migration]`, não por convenção de diretório. Nada fora de `src/` entra em compilação, e o `Dockerfile` copia apenas `src/`: arquivo de código colocado fora dessa árvore é silenciosamente ignorado pelo build.
- **`Student` tem query filter global (`!IsDeleted`) e é a ponta obrigatória** dos relacionamentos com `Enrollment` e `Payment`, que não têm filtro equivalente. O EF sinaliza isso com `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`, emitido na construção do modelo (runtime / comandos `dotnet ef`) — **não** na saída do compilador. Ver a seção "Soft delete: decisão em aberto" abaixo antes de mexer em qualquer query de `Payment` ou `Enrollment`.
- **Navegações usam `= null!`**, mas escalares obrigatórios variam: `Course` declara `Titulo`, `Descricao`, `Categoria` e `CargaHoraria` como `required`, enquanto `Student` e `Payment` usam `= null!` / `= string.Empty`. Ao montar entidade em teste, confira qual convenção aquela classe segue antes de escrever o object initializer. O build roda com **zero warnings** — se um `dotnet build` seu passar a emitir CS86xx, é código novo, não ruído herdado.

## Soft delete: decisão tomada

Opção escolhida: **restringir o filtro global ao agregado `Student` e usar `IgnoreQueryFilters()` nas consultas de `Payment` que precisam da navegação.** O histórico financeiro é preservado — um aluno removido não faz pagamentos sumirem de relatório.

Comportamento **medido** (EF InMemory, contexto limpo), não suposto:

| Consulta | Aluno ativo | Aluno soft-deleted |
| --- | --- | --- |
| `_context.Payments...` (sem join) | traz o pagamento | traz o pagamento |
| `.Include(p => p.Student)` | traz o pagamento | **o pagamento some por inteiro** |
| `.Include(...).IgnoreQueryFilters()` | traz o pagamento | traz o pagamento |

A navegação `Payment.Student` é obrigatória, então o EF traduz o `Include` em `INNER JOIN`; o filtro `!IsDeleted` no `Student` elimina a linha do lado de fora. `IgnoreQueryFilters()` desliga o filtro para aquela query.

**Regra ao mexer em `PaymentRepository`:** todo `Include` que atravesse `Student` — direta ou indiretamente, como `Enrollment.Student` — precisa vir acompanhado de `IgnoreQueryFilters()`. Sem ele, a linha some silenciosamente para aluno removido; sem o `Include`, a navegação vem `null` e o handler estoura com `NullReferenceException`. Hoje só `GetByIdAsync` e `GetByEnrollmentIdAsync` usam navegação; `GetPagedAsync` e `GetByStudentIdAsync` não, e por isso não têm nenhum dos dois.

Não há lazy loading para salvar de um `Include` esquecido: `Microsoft.EntityFrameworkCore.Proxies` está referenciado, mas `UseLazyLoadingProxies()` nunca é chamado e as navegações não são `virtual`.

Os testes unitários **não** cobrem isso, porque mockam `IPaymentRepository` devolvendo um `Payment` com `Student` preenchido à mão. Quem protege é `tests/TechCurse.Api.IntegrationTests/Endpoints/PaymentNavigationTests.cs`, que exercita os dois caminhos com aluno ativo e removido.

## Branches

O remoto tem apenas `main` e `tech-curse_v1.1`, e os PRs recentes foram mesclados em `main`. Trabalhe em branch de feature a partir de `main` e abra PR para `main`, salvo instrução em contrário. Commits seguem Conventional Commits em pt-BR (`feat:`, `fix:`, `test:`, `docs:`, `chore:`, `refactor:`).
