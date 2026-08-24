# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> O projeto é documentado e comentado em **português brasileiro**. Mantenha commits, comentários, mensagens de exceção e documentação em pt-BR.

## Visão geral

API REST em .NET 10 / C# 14 para uma plataforma de cursos (cursos, estudantes, matrículas e pagamentos), estruturada em Clean Architecture + CQRS com Vertical Slices (MediatR), SQL Server (EF Core 10), Redis, Serilog/Seq e autenticação JWT com ASP.NET Core Identity.

## Comandos

Todos executados na raiz do repositório. A solution é `tech-curse.slnx` (formato slnx, não `.sln`).

```bash
dotnet build tech-curse.slnx
```

```bash
dotnet test tech-curse.slnx --logger "console;verbosity=normal"
```

Rodar apenas um projeto de teste ou um teste específico:

```bash
dotnet test tech-curse.Test.Unit/tech-curse.Test.Unit.csproj --filter "FullyQualifiedName~ProcessPaymentCommandHandlerTests"
```

```bash
dotnet test tech-curse.slnx --filter "Category=Unit"
```

Subir a stack completa (API + SQL Server + Redis + Seq). Requer `.env` — copie de `.env.example`:

```bash
docker-compose up -d --build
```

Rodar só as dependências e a API no host (Swagger em http://localhost:5130/swagger, health em `/health`):

```bash
docker-compose up -d db redis seq
```

```bash
dotnet run --project tech-curse/src/API
```

Criar migration do EF Core (o DbContext vive em Infrastructure, o host em API):

```bash
dotnet ef migrations add NomeDaMigration --project tech-curse/src/Infrastructure --startup-project tech-curse/src/API
```

## Arquitetura

### Camadas e dependências

Quatro projetos em `tech-curse/src/`, todos com namespace raiz `TechCurse.src.<Camada>` (o segmento `src` faz parte do namespace — não o remova):

- **Domain** — entidades, enums, exceções de domínio e `ISpecification<T>`. Zero dependências de outras camadas e nenhuma referência a EF Core ou ASP.NET MVC.
- **Application** — CQRS por Vertical Slices em `Features/<Recurso>/{Commands,Queries}/<Operacao>/`, cada pasta com `Command`/`Query` + `Handler` + `Validator`. Também abriga DTOs, `Interfaces/` (contratos de repositório, cache, gateway, usuário atual), a factory e as strategies de pagamento. Depende apenas de Domain.
- **Infrastructure** — `TechCurseContext`, repositórios, Redis, Identity/JWT e o adaptador de gateway de pagamento. Depende de Application (implementa suas interfaces).
- **API** — controllers finos, middlewares e os `Configuration/*Setup.cs` (extension methods de DI: Serilog, EF Core, Identity/JWT, Redis, Swagger).

Essas regras não são só convenção: o projeto `tech-curse.Test.Architecture` as verifica com NetArchTest e **quebra a suíte de testes** se violadas.

### Fluxo de uma requisição

Controller (só `IMediator`) → `ExceptionHandlingMiddleware` → `CorrelationIdMiddleware` → MediatR → `ValidationBehavior<,>` → Handler → Repositório/Cache/Gateway.

Pontos que se repetem em todo o código:

- **Erros**: handlers lançam exceções de `TechCurse.src.Domain.Exceptions`; o `ExceptionHandlingMiddleware` as traduz em `ProblemDetails` com o status HTTP correspondente (`NotFoundException`→404, `ConflictException`/`NotAllowedException`→409, `ValidationException`→422, `GatewayTimeoutException`→504, etc.). Não retorne status de erro manualmente no controller.
- **Validação**: `ValidationBehavior` roda todos os `AbstractValidator<T>` registrados por assembly scanning e converte falhas em `ValidationException` (422). Basta criar o validator na mesma pasta da slice — não há registro manual.
- **Cache**: queries leem/gravam em Redis via `ICacheService` com chaves prefixadas (`payments:list:`, `payments:item:`, `payments:student:`, `payments:enrollment:`, …). Todo command que muta dados chama `RemoveByPrefixAsync` para cada prefixo afetado — ao adicionar uma nova query com cache, adicione a invalidação correspondente nos commands.
- **Idempotência**: endpoints de escrita de pagamento usam `[TypeFilter(typeof(IdempotencyFilterMiddleware))]`; o header `Idempotency-Key` é **obrigatório** (ausência → 400) e a resposta é replayed do Redis por 6 minutos.
- **Autorização**: RBAC por `[Authorize(Roles = "Admin|Instructor|Student")]` no controller; regras de "é o próprio usuário" ficam nos handlers via `ICurrentUserService`. As roles são criadas no startup pelo `DbInitializer`.
- **Pagamentos**: `PaymentStrategyFactory` resolve a `IPaymentStrategy` pelo `PaymentMethodType`; a elegibilidade é checada por `PaymentProcessableSpecification` antes de chamar o `IPaymentGatewayAdapter`.
- **Soft delete**: `Student` tem global query filter (`!s.IsDeleted`) no `OnModelCreating`.

### Configuração

Configuração vem de variáveis de ambiente / connection strings, não de `appsettings.json` (que só tem logging):

- `ConnectionStrings:APITechCurse`, `ConnectionStrings:RedisCache`, `ConnectionStrings:SeqUrl`
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (mínimo 32 caracteres — o startup lança exceção se for menor)
- `UseInMemoryDatabase=true` faz o `EFCoreSetup` pular o registro do SQL Server; é o gancho usado pelos testes de integração.

No Docker Compose essas chaves chegam como `Jwt__SigningKey`, `ConnectionStrings__APITechCurse`, etc., alimentadas pelo `.env`.

## Testes

Três projetos, todos xUnit + FluentAssertions:

- `tech-curse.Test.Unit` — handlers e validators isolados com **Moq** (`Mock<IPaymentRepository>` etc.), montando o handler no construtor da classe de teste. Padrão de nome: `Handle_WhenX_ShouldY`, com `[Trait("Category", "Unit")]`.
- `tech-curse.Test.Integration` — `CustomWebApplicationFactory : WebApplicationFactory<Program>` (por isso existe `public partial class Program { }` no fim do `Program.cs`). Usa EF InMemory, `InMemoryTestCacheService` no lugar do Redis e helpers `CreateAdminClient()` / `CreateStudentClient()` / `CreateInstructorClient()` que já injetam um JWT válido.
- `tech-curse.Test.Architecture` — NetArchTest sobre os assemblies referenciados em `Common/ArchitectureConstants.cs`. Impõe: isolamento de camadas, handlers/validators em `TechCurse.src.Application.Features.*`, sufixos `Handler`/`Validator`/`Controller`/`Repository`, repositórios em `Infrastructure.Repositories`, e controllers sem dependência de `TechCurseContext` nem de repositórios (só `IMediator`).

Ao criar uma nova slice, o caminho completo é: `Command`/`Query` + `Handler` + `Validator` na pasta da feature → interface de repositório em `Application/Interfaces` → implementação em `Infrastructure/Repositories` (registrada em `Infrastructure/DependencyInjection.cs`) → action no controller com `SwaggerOperation`/`SwaggerResponse` → testes unitários do handler e do validator.

## Armadilhas conhecidas

- **Migrations vivem em `tech-curse/src/Infrastructure/Migrations/`**, mesmo assembly do `TechCurseContext` — por isso não é preciso configurar `MigrationsAssembly()`. O namespace declarado nos arquivos é `TechCurse.Migrations` e não acompanha a pasta: o EF localiza migrations pelos atributos `[DbContext]`/`[Migration]`, não por convenção de diretório. Nada fora de `tech-curse/src/` entra em compilação — o `.csproj` da raiz que existia até `f579a7f` foi removido, e o `Dockerfile` copia apenas `tech-curse/src/`. Arquivo de código colocado fora dessa árvore é silenciosamente ignorado pelo build.
- **`Student` tem query filter global (`!IsDeleted`) e é a ponta obrigatória** dos relacionamentos com `Enrollment` e `Payment`, que não têm filtro equivalente. O EF emite `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` no build: queries com join podem se comportar de forma inesperada para alunos soft-deleted.
- **`IPaymentGatewayAdapter` fica no arquivo `Application/Interfaces/IPaymentGateway.cs`** — o nome do arquivo não bate com o da interface; procure pelo nome da interface, não do arquivo.
- **Nenhum `IPaymentGatewayAdapter` é registrado em Production**: o `if (environment.IsProduction())` em `Infrastructure/DependencyInjection.cs` está vazio, e só o `SimulatedPaymentGatewayAdapter` é registrado fora de produção.
- **`BadRequestExecption`** está grafado assim (com o typo) em todo o código — mantenha a grafia ao referenciá-la.

## Branches

O remoto tem apenas `main` e `tech-curse_v1.1`, e os PRs recentes foram mesclados em `main`. Trabalhe em branch de feature a partir de `main` e abra PR para `main`, salvo instrução em contrário. Commits seguem Conventional Commits em pt-BR (`feat:`, `fix:`, `test:`, `docs:`, `chore:`, `refactor:`).
