# Ajustes do backend para o portal do aluno

**Data:** 2026-09-23
**Status:** aprovado em brainstorming, aguardando plano de implementação
**Motivação:** a Fase 2 do front-end (`tech-curse-web`, portal do aluno) expôs três problemas no backend: uma falha de segurança no registro, a ausência de perfil de estudante para quem se cadastra e a impossibilidade de ligar pagamento a curso.

A entrega é dividida em **dois PRs independentes**, nesta ordem:

- **PR A — `fix/registro-somente-aluno`:** segurança do registro, rota administrativa de criação de usuários e Admin semeado em desenvolvimento.
- **PR B — `feat/perfil-aluno-e-curso-no-pagamento`:** criação automática do perfil de estudante e novos campos nos contratos de pagamento e de matrículas.

As regras do `CLAUDE.md` valem para os dois: nenhum comentário em arquivo-fonte, pt-BR em mensagens e documentação, Conventional Commits, branch a partir de `main` e PR para `main`.

## Situação atual

- `POST /Auth/register` é público e recebe `RegisterInputDto(Name, Email, Role, Password, ConfirmPassword)`. O `AuthService.RegisterAsync` adiciona o usuário à role enviada no corpo. **Qualquer pessoa consegue se cadastrar como `Admin`.**
- O cadastro cria só o `IdentityUser`. A entidade `Student` só nasce por `POST /Student` (role `Admin`). Um aluno recém-cadastrado recebe 404 em `/Student/me` e não consegue se matricular.
- `PaymentOutputDto` traz `EnrollmentId`, mas não o curso; `CourseStudentOutputDto` traz `CourseId`, mas não `EnrollmentId`. Um cliente não consegue ligar pagamento a curso.
- `DbInitializer.SeedDataAsync` só cria as roles. Não há usuário Admin em desenvolvimento.
- O seed roda em `Program.cs` só quando `Database.IsRelational()`; nos testes de integração (provider em memória) ele não roda.
- O provider em memória do EF, usado pelos testes de integração, não suporta transação.

## PR A — segurança do registro

### Contratos

| Rota | Acesso | Corpo | Resposta |
|---|---|---|---|
| `POST /Auth/register` | público | `RegisterInputDto(Name, Email, Password, ConfirmPassword)` | 201 `{ mensagem }` — sempre cria um usuário `Student` |
| `POST /Auth/users` (nova) | `Admin` | `CreateUserInputDto(Name, Email, Role, Password, ConfirmPassword)` | 201 `{ mensagem }`; 401 anônimo; 403 não-Admin; 422 validação (inclui `DuplicateEmail`) |

- `RegisterInputDto` perde o campo `Role`. Um JSON antigo com `role` continua sendo aceito: o campo extra é ignorado pelo desserializador e o usuário é criado como `Student`.
- `CreateUserInputDto` aceita `Admin`, `Instructor` e `Student` (`UserRole`, com `[Required]`).
- A rota nova fica no `AuthController` e herda a política de rate limiting `PoliticaAutenticacao`, aplicada na classe.

### Implementação

- `IAuthService` ganha `CreateUserAsync(CreateUserInputDto input)`.
- A criação comum (validar confirmação de senha, `CreateAsync`, mapear erros do Identity para `ValidationException`, `AddToRoleAsync`) vai para um método privado único do `AuthService`, usado por `RegisterAsync` (role fixa `Student`) e por `CreateUserAsync` (role do corpo). Nenhuma lógica duplicada.
- Nova action em `AuthController`, com `[Authorize(Roles = "Admin")]` e as anotações Swagger no padrão das demais.

### Admin semeado em `Development`

- `DbInitializer.SeedDataAsync` continua criando as roles e passa a criar um Admin quando **todas** as condições valem:
  - o ambiente (`IHostEnvironment`) é `Development`;
  - `Seed:Admin:Email` e `Seed:Admin:Password` estão preenchidas na configuração.
- Idempotente: se já existe usuário com o e-mail, não faz nada (não altera senha nem role).
- Nome de usuário do Admin semeado: a parte local do e-mail (antes do `@`), respeitando os caracteres aceitos pelo Identity.
- Falha ao criar o Admin (senha fora da política, por exemplo) lança exceção com os erros do Identity, e o `Program.cs` já registra e interrompe a inicialização. Configuração ausente não é erro: o seed simplesmente pula o Admin.
- Origem das credenciais — nunca em arquivo versionado:
  - `dotnet run` no host: user-secrets (`dotnet user-secrets set "Seed:Admin:Email" ...`);
  - `docker-compose`: novas variáveis `SEED_ADMIN_EMAIL` e `SEED_ADMIN_PASSWORD` no `.env`, repassadas ao serviço `api` como `Seed__Admin__Email` e `Seed__Admin__Password`; `.env.example` ganha as duas chaves com valores de exemplo.

### Testes do PR A

- Integração (`AuthEndpointsTests`):
  - registro com JSON contendo `"role": "Admin"` cria o usuário apenas na role `Student`;
  - `POST /Auth/users` → 401 sem token, 403 com token de `Student`, 201 com token de `Admin` e usuário criado na role pedida;
  - `POST /Auth/users` com e-mail em uso → 422 com a chave `DuplicateEmail` (erro do Identity mapeado para `ValidationException`, como no registro atual).
- Seed (chamando `DbInitializer.SeedDataAsync` com um provedor de serviços de teste e contexto em memória):
  - cria o Admin em `Development` com configuração completa;
  - não cria fora de `Development`;
  - não cria sem configuração;
  - segunda execução não duplica nem altera o usuário.
- Unitários do `AuthService`, se existirem para o registro, ajustados ao novo DTO.

### Documentação do PR A

- `README.md` e `CLAUDE.md`: novo contrato do registro, a rota `/Auth/users`, como configurar o Admin semeado e a regra de que a role nunca vem do registro público.
- `docs/postman_collection.json`: remover `role` do registro, adicionar `POST /Auth/users`.
- `CHANGELOG.md`: entrada de segurança.

## PR B — perfil automático e curso no pagamento

### Perfil de estudante no cadastro

- `RegisterAsync` e `CreateUserAsync` (quando a role é `Student`) criam o `Student` logo após o usuário: `Nome` = `Name` do cadastro, `Email`, `IdentityUserId`, `DataCadastro` = `DateTime.UtcNow`, `IsDeleted` = `false`.
- A criação usa `IStudentRepository.AddAsync`, injetado no `AuthService`.
- **Compensação em vez de transação:** se a criação do `Student` lançar exceção, o `AuthService` apaga o `IdentityUser` recém-criado (`UserManager.DeleteAsync`) e relança a exceção original. Motivo: o provider em memória dos testes não suporta transação, e a compensação funciona igual nos dois providers sem código condicional.
- Se já existir `Student` com o mesmo e-mail (dado antigo), o cadastro falha com `ConflictException` antes de criar o usuário, usando `IStudentRepository.EmailExistsAsync`.
- `POST /Student` (Admin) continua existindo para casos manuais, como usuários `Student` criados antes desta mudança.
- **Sem backfill:** usuários `Student` antigos continuam sem perfil. O front trata esse caso como "perfil pendente".

### Contratos (só campos novos; nada removido ou renomeado)

- `PaymentOutputDto` ganha, ao final, `int CourseId` e `string CourseTitulo`, preenchidos por `Enrollment.Course`.
  - Os cinco pontos que montam o DTO passam a preenchê-los: `CreatePaymentCommandHandler`, `GetPaymentByIdQueryHandler`, `GetPaymentsQueryHandler`, `GetPaymentsByEnrollmentIdQueryHandler`, `GetPaymentsByStudentIdQueryHandler`.
  - As consultas do `PaymentRepository` usadas por esses handlers incluem `Enrollment` e `Enrollment.Course` (`Include`/`ThenInclude`) para evitar N+1. No `CreatePaymentCommandHandler`, o curso vem da matrícula já carregada para validar o pagamento; se ela não vier com o curso, o repositório de matrícula passa a incluí-lo.
  - Se algum desses handlers usa cache, a chave e o valor cacheados passam a conter os campos novos (invalidar ou versionar a chave para não servir o formato antigo).
- `CourseStudentOutputDto` ganha, ao final, `int EnrollmentId`, preenchido em `StudentRepository.GetCoursesAsync` a partir de `e.EnrollmentId`.

### Testes do PR B

- Integração:
  - `POST /Auth/register` seguido de login e `GET /Student/me` → 200 com o nome e o e-mail do cadastro;
  - `POST /Auth/users` com role `Student` cria o perfil; com `Instructor` não cria;
  - cadastro com e-mail que já tem `Student` → 409 e nenhum usuário novo;
  - `GET /Payment/student/{id}`, `GET /Payment/{id}` e `GET /Payment/enrollment/{id}` trazem `courseId` e `courseTitulo` corretos;
  - `GET /Student/{id}/enrollments` traz `enrollmentId`.
- Unitários do `AuthService`: a compensação apaga o usuário quando `AddAsync` do `Student` lança exceção e relança a exceção original.
- Unitários dos handlers de pagamento: DTO com os campos novos.

### Documentação do PR B

- `README.md` e `CLAUDE.md`: o cadastro cria o perfil de estudante; os campos novos dos DTOs.
- `docs/postman_collection.json`: exemplos de resposta atualizados, se houver.
- `CHANGELOG.md`: entrada da funcionalidade.

## Verificação comum aos dois PRs

- `dotnet build TechCurse.slnx` e `dotnet test TechCurse.slnx` passando.
- `dotnet format TechCurse.slnx --verify-no-changes` limpo.
- Subida local pelo fluxo documentado (`docker-compose up -d db redis seq` + `dotnet run --project src/Api`) com `/health/ready` → 200.
- Depois do PR B, os E2E do front (`npm run e2e` no `tech-curse-web`) continuam passando.

## Impacto no front-end (`tech-curse-web`)

Depois dos dois merges, o spec da Fase 2 do front é atualizado:

- "Meus pagamentos" mostra o título do curso (`courseTitulo`), com link para o curso.
- O estado "perfil pendente" continua existindo, mas vira exceção (usuários antigos ou perfis desativados).
- Os E2E deixam de registrar um Admin descartável: fazem login no Admin semeado, com as credenciais em `E2E_ADMIN_EMAIL` e `E2E_ADMIN_SENHA`, e o aluno criado no registro já nasce com perfil.

## Fora de escopo

- Cancelamento de matrícula e listagem de categorias (ficam para uma rodada futura).
- Backfill de perfis para usuários `Student` antigos.
- Qualquer mudança na persistência ou nas migrations (nenhuma tabela ou coluna muda).
