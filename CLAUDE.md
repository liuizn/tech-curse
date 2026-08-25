# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> O projeto é documentado e comentado em **português brasileiro**. Mantenha commits, comentários, mensagens de exceção e documentação em pt-BR.

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
- **Soft delete**: `Student` tem global query filter (`!s.IsDeleted`) no `OnModelCreating`.
- **Health checks**: `/health/live` é liveness — nenhum check roda (`Predicate = _ => false`), responde texto puro. `/health/ready` é readiness — agrega só os checks marcados com `HealthCheckTags.Ready` (`src/Api/Configuration/HealthCheckTags.cs`) e devolve o JSON detalhado por verificação. Ao registrar uma nova dependência crítica em um `*Setup.cs`, passe `tags: [HealthCheckTags.Ready]`; sem a tag o check não aparece em endpoint nenhum. O smoke test do pipeline sonda `/health/ready`.

### Configuração

Configuração vem de variáveis de ambiente / connection strings, não de `appsettings.json` (que só tem logging):

- `ConnectionStrings:APITechCurse`, `ConnectionStrings:RedisCache`, `ConnectionStrings:SeqUrl`
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (mínimo 32 caracteres — o startup lança exceção se for menor)
- `UseInMemoryDatabase=true` faz o `EFCoreSetup` pular o registro do SQL Server; é o gancho usado pelos testes de integração.

No Docker Compose essas chaves chegam como `Jwt__SigningKey`, `ConnectionStrings__APITechCurse`, etc., alimentadas pelo `.env`.

## Testes

Quatro projetos em `tests/`, todos xUnit + FluentAssertions. Cada um referencia só a camada que exercita — se um teste novo pede uma referência a mais, é sinal de que ele está no projeto errado:

- `TechCurse.Domain.UnitTests` — entidades e `ISpecification<T>` sem nenhum mock. Referencia apenas `TechCurse.Domain`.
- `TechCurse.Application.UnitTests` — handlers e validators isolados com **Moq** (`Mock<IPaymentRepository>` etc.), montando o handler no construtor da classe de teste. Padrão de nome: `Handle_WhenX_ShouldY`, com `[Trait("Category", "Unit")]`. Referencia `TechCurse.Application` e `TechCurse.Domain` — **não** referencia Infrastructure nem Api.
- `TechCurse.Api.IntegrationTests` — `CustomWebApplicationFactory : WebApplicationFactory<Program>` (por isso existe `public partial class Program { }` no fim do `Program.cs`). Usa EF InMemory, `InMemoryTestCacheService` no lugar do Redis e helpers `CreateAdminClient()` / `CreateStudentClient()` / `CreateInstructorClient()` que já injetam um JWT válido. `Persistence/` abriga os testes que batem direto no `TechCurseContext`, sem subir o host HTTP.
- `TechCurse.ArchitectureTests` — NetArchTest sobre os assemblies referenciados em `Common/ArchitectureConstants.cs`. Impõe: isolamento de camadas, handlers/validators em `TechCurse.Application.Features.*`, sufixos `Handler`/`Validator`/`Controller`/`Repository`, repositórios em `Infrastructure.Repositories`, e controllers sem dependência de `TechCurseContext` nem de repositórios (só `IMediator`).

Ao criar uma nova slice, o caminho completo é: `Command`/`Query` + `Handler` + `Validator` na pasta da feature → interface de repositório em `Application/Interfaces` → implementação em `Infrastructure/Repositories` (registrada em `Infrastructure/DependencyInjection.cs`) → action no controller com `SwaggerOperation`/`SwaggerResponse` → testes unitários do handler e do validator.

## Armadilhas conhecidas

- **Migrations vivem em `src/Infrastructure/Migrations/`** (namespace `TechCurse.Infrastructure.Migrations`), mesmo assembly do `TechCurseContext` — por isso não é preciso configurar `MigrationsAssembly()`. O EF localiza migrations pelos atributos `[DbContext]`/`[Migration]`, não por convenção de diretório. Nada fora de `src/` entra em compilação, e o `Dockerfile` copia apenas `src/`: arquivo de código colocado fora dessa árvore é silenciosamente ignorado pelo build.
- **`Student` tem query filter global (`!IsDeleted`) e é a ponta obrigatória** dos relacionamentos com `Enrollment` e `Payment`, que não têm filtro equivalente. O EF sinaliza isso com `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`, emitido na construção do modelo (runtime / comandos `dotnet ef`) — **não** na saída do compilador. Ver a seção "Soft delete: decisão em aberto" abaixo antes de mexer em qualquer query de `Payment` ou `Enrollment`.
- **Propriedades de entidade usam `= null!` / `= string.Empty`** em vez de `required`: navegações são preenchidas pelo EF, e `required` quebraria os object initializers espalhados pelos testes. O build roda com **zero warnings** — se um `dotnet build` seu passar a emitir CS86xx, é código novo, não ruído herdado.

## Soft delete: decisão em aberto

Comportamento **medido** (EF InMemory, contexto limpo), não suposto:

| Consulta | Aluno ativo | Aluno soft-deleted |
| --- | --- | --- |
| `_context.Payments...` (sem join) | traz o pagamento | **traz o pagamento** |
| `.Include(p => p.Student)` | traz o pagamento | **o pagamento some por inteiro** |
| `.Where(p => p.Student.Nome == x)` | traz o pagamento | **o pagamento some por inteiro** |

A navegação `Payment.Student` é obrigatória, então o EF traduz o `Include` em `INNER JOIN`; o filtro `!IsDeleted` no `Student` elimina a linha do lado de fora. O resultado é que hoje o sistema é **incoerente consigo mesmo**: `PaymentRepository.GetPagedAsync` e `GetByStudentIdAsync` (sem join) continuam listando pagamentos de alunos removidos, mas qualquer consulta que toque a navegação os apaga silenciosamente.

**Duas armadilhas concretas já presentes no código** (`src/Infrastructure/Repositories/PaymentRepository.cs`):

- `GetByIdAsync` usa `AsNoTracking()` **sem `Include`**, e `GetPaymentByIdQueryHandler` lê `payment.Student.IdentityUserId`. Sem lazy loading (o pacote `Microsoft.EntityFrameworkCore.Proxies` está referenciado, mas `UseLazyLoadingProxies()` nunca é chamado e as navegações não são `virtual`), `payment.Student` é **null** — `NullReferenceException`. Os testes unitários não pegam isso porque mockam `IPaymentRepository` devolvendo um `Payment` com `Student` preenchido à mão.
- `GetByEnrollmentIdAsync` tem o mesmo problema com `Enrollment`, lido em `GetPaymentsByEnrollmentIdQueryHandler` via `.Enrollment.Student`.

O `Include` que corrige essas duas é exatamente o que ativa a interação com o query filter — por isso as duas questões precisam ser resolvidas juntas, e a escolha é **de produto**:

1. **Propagar o soft delete** — dar filtro `!Student.IsDeleted` a `Enrollment` e `Payment`. Coerente e previsível, mas some com o histórico financeiro do aluno removido: relatórios de faturamento e conciliação passam a não fechar retroativamente quando alguém é removido.
2. **Restringir o filtro ao agregado `Student`** e usar `IgnoreQueryFilters()` nas consultas de `Payment`/`Enrollment` que precisam da navegação. Preserva o histórico, mas expõe dados de aluno removido por um caminho indireto — o que pode conflitar com a promessa implícita de LGPD do soft delete.
3. **Trocar o filtro por consulta explícita** — remover o filtro global e escrever `.Where(s => !s.IsDeleted)` onde importa. Elimina toda a interação implícita ao custo de repetição e de risco de esquecimento.

Enquanto não houver decisão, **não adicione `Include(p => p.Student)` nem predicados sobre a navegação** achando que é correção inócua: o efeito colateral é fazer linhas sumirem.

## Branches

O remoto tem apenas `main` e `tech-curse_v1.1`, e os PRs recentes foram mesclados em `main`. Trabalhe em branch de feature a partir de `main` e abra PR para `main`, salvo instrução em contrário. Commits seguem Conventional Commits em pt-BR (`feat:`, `fix:`, `test:`, `docs:`, `chore:`, `refactor:`).
