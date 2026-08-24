# 📋 Changelog

Todas as alterações notáveis deste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/).

---

## [Unreleased]

### ♻️ Alterado
- **Estrutura de pastas padronizada** para o layout convencional de Clean Architecture em .NET: `src/` e `tests/` na raiz do repositório, no estilo do template `dotnet new ca-sln`. A pasta `tech-curse/` intermediária foi eliminada e `src/API` passou a `src/Api`.
- **Projetos e assemblies renomeados** de `tech-curse.*` para `TechCurse.*`, restabelecendo a regra `.csproj` = `AssemblyName` = `RootNamespace`. A solution passou de `tech-curse.slnx` para `TechCurse.slnx`, agora com solution folders espelhando o disco.
- **Namespaces** `TechCurse.src.<Camada>` passaram a `TechCurse.<Camada>`; o segmento `src` era artefato de derivação por caminho. As migrations passaram de `TechCurse.Migrations` para `TechCurse.Infrastructure.Migrations`.
- **Projetos de teste renomeados** para `TechCurse.ArchitectureTests`, `TechCurse.Application.UnitTests` e `TechCurse.Api.IntegrationTests`.
- `PaymentPersistenceTests` (antigo `PaymentTests`) convertido de ISO-8859-1 para UTF-8, corrigindo os acentos dos comentários em pt-BR.

### ✨ Adicionado
- **`TechCurse.Domain.UnitTests`** — quarto projeto de teste, com os 6 testes de entidades e Specifications que antes viviam no projeto de unitários de aplicação. Referencia exclusivamente `TechCurse.Domain`.

### 🐛 Corrigido
- `TechCurse.Application.UnitTests` referenciava `TechCurse.Api` sem utilizá-lo e dependia de `TechCurse.Infrastructure` apenas por transitividade. Com os testes de persistência movidos para o projeto de integração, ambas as referências foram removidas.

> Distribuição atual dos 204 testes: 23 arquitetura, 6 domínio, 133 aplicação, 42 integração.

---

## [1.2.0] - 2026-08-16

### 🌟 Resumo Executivo
A versão **v1.2.0** representa um marco arquitetural na evolução da **Tech Curse API**. O projeto foi completamente reestruturado da arquitetura em camadas tradicional baseada em serviços monolíticos para uma arquitetura moderna baseada em **Clean Architecture** e **CQRS (Command Query Responsibility Segregation)** com **Vertical Slices**, utilizando **MediatR** e **FluentValidation**.

Além disso, esta release consolida uma suíte robusta de **204 testes automatizados** com 100% de taxa de aprovação, abrangendo testes de integridade arquitetural (NetArchTest), testes unitários em isolamento e testes de integração de ponta a ponta.

---

### 🚀 Principais Destaques & Mudanças

#### 1. Migração Total para CQRS & MediatR
- **Desacoplamento Completo de Serviços:** As antigas classes monolíticas (`CourseService`, `StudentService`, `EnrollmentService`, `PaymentService`) foram 100% removidas.
- **Vertical Slices Isoladas:** Cada operação de leitura e escrita do sistema agora é tratada por um **Command Handler** ou **Query Handler** dedicado e autocontido.
- **Injeção Cirúrgica de Dependências:** Handlers de consulta não instanciam dependências pesadas de escrita ou de gateway, resultando em menor consumo de memória e ganhos expressivos de performance em requisições concorrentes.

#### 2. Validação Determinística com FluentValidation & Pipeline Behaviors
- Implementação do `ValidationBehavior<TRequest, TResponse>` no pipeline do MediatR.
- Entradas inválidas ou em formatos incorretos são interceptadas antes de interagir com o banco de dados.
- Padronização de respostas de erro no formato RFC 7807 (`ProblemDetails`) com status **`422 Unprocessable Entity`**, listando o detalhamento de cada campo violado.

#### 3. Eliminação de God Objects & Quebra do Módulo Payment
- A entidade e operações de `Payment` foram desmembradas em 7 Vertical Slices independentes:
  - `CreatePaymentCommand` / `CreatePaymentCommandHandler`
  - `ProcessPaymentCommand` / `ProcessPaymentCommandHandler`
  - `RefundPaymentCommand` / `RefundPaymentCommandHandler`
  - `GetPaymentsQuery` / `GetPaymentsQueryHandler`
  - `GetPaymentByIdQuery` / `GetPaymentByIdQueryHandler`
  - `GetPaymentsByStudentIdQuery` / `GetPaymentsByStudentIdQueryHandler`
  - `GetPaymentsByEnrollmentIdQuery` / `GetPaymentsByEnrollmentIdQueryHandler`
- Introdução da especificação de domínio `PaymentProcessableSpecification` para blindar regras de transição de estado de pagamentos.

#### 4. Correção e Padronização de Rotas RESTful
- Correção de rotas legadas fora do padrão:
  - `POST /process` ➔ **`POST /tech-curse/payment/process`**
  - `POST /refund` ➔ **`POST /tech-curse/payment/refund`**
- Todos os controladores agora utilizam o prefixo padronizado `/tech-curse/[controller]`.

#### 5. Cache Distribuído com Redis, Idempotência & Observabilidade
- Implementação de middleware de idempotência (`IdempotencyFilterMiddleware`) com armazenamento em Redis para evitar processamento duplicado de pagamentos.
- Rastreamento fim a fim de requisições com `CorrelationIdMiddleware`.
- Integração com Serilog e sink para Seq Dashboard (`http://localhost:9000`).

#### 6. Suíte de 204 Testes Automatizados (100% Passing)
- **Testes de Arquitetura (`tech-curse.Test.Architecture` - 23 testes):** Governança automatizada via NetArchTest para garantir a integridade das camadas da Clean Architecture (nenhuma dependência inversa para o Domain).
- **Testes Unitários (`tech-curse.Test.Unit` - 143 testes):** Cobertura unitária exaustiva de todos os Handlers, Validators do FluentValidation e Domain Specifications com `Moq` e `FluentAssertions`.
- **Testes de Integração (`tech-curse.Test.Integration` - 38 testes):** Testes de ponta a ponta com `WebApplicationFactory` cobrindo autenticação JWT, autorização RBAC, middlewares e persistência em memória.

---

### ⚠️ Breaking Changes & Guia de Migração

| Endpoint / Componente Antigo | Novo Endpoint / Comportamento v1.2.0 | Status Code Atualizado | Impacto / Ação Necessária |
| :--- | :--- | :---: | :--- |
| `POST /process` | `POST /tech-curse/payment/process` | `200 OK` | Atualizar URLs de chamadas de processamento de pagamento. Retorna `ProcessPaymentOutputDto`. |
| `POST /refund` | `POST /tech-curse/payment/refund` | `200 OK` | Atualizar URLs de chamadas de estorno de pagamento. Retorna `RefundPaymentOutputDto`. |
| Validação de DTOs | Interceptado pelo MediatR Pipeline | `422 Unprocessable Entity` | Clientes devem tratar `422` com `ValidationExceptionResponse` (ProblemDetails) em vez de `400 Bad Request` genérico. |
| Aluno ou Curso inexistente na Matrícula | `NotFoundException` | `404 Not Found` | Tratamento semântico corrigido (anteriormente retornava `403 Forbidden` indevido). |
| Aluno já matriculado no Curso | `ConflictException` | `409 Conflict` | Tratamento semântico corrigido (anteriormente retornava `403 Forbidden` indevido). |
| Handlers de Leitura / Escrita | Substituição de Services por MediatR Requests | N/A Interno | Classes de serviço `*Service` foram removidas em favor de `IRequest` / `IRequestHandler`. |

---

### 👥 Contribuidores
- [@Liuizn](https://github.com/Liuizn)
