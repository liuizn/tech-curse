# 📋 Release Notes & CHANGELOG Template

This document provides the standard structure for authoring Release Notes and maintaining `CHANGELOG.md`.

---

## 🚀 Template: Version Release Notes (ex: v1.2.0)

```markdown
# Release v1.2.0: Clean Architecture & CQRS Re-architecture

## 🌟 Visão Geral da Versão
A versão **v1.2.0** representa um marco arquitetural no projeto **Tech Curse API**. O sistema foi integralmente reestruturado da arquitetura tradicional baseada em serviços para **Clean Architecture com Vertical Slices (CQRS)** utilizando **MediatR** e **FluentValidation**, além da introdução de uma suíte completa de **204 testes automatizados** (Arquitetura, Unidade e Integração).

---

## 🚀 Principais Destaques & Mudanças

### 1. Migração Total para CQRS & MediatR
- **Desacoplamento Completo:** As antigas classes de serviço monolíticas (`CourseService`, `StudentService`, `EnrollmentService`, `PaymentService`) foram 100% removidas.
- **Vertical Slices:** Cada operação de negócio agora é isolada em seu próprio **Command** ou **Query Handler**.
- **Injeção Cirúrgica de Dependências:** Handlers de leitura não instanciam Gateways ou Identity, gerando ganho massivo de performance em requisições concorrentes.

### 2. Validação Determinística com FluentValidation
- Introdução do `ValidationBehavior` no pipeline do MediatR.
- Entradas inválidas ou incompletas são interceptadas antes de atingir o banco de dados, retornando **`422 Unprocessable Entity`** com o detalhamento dos campos em formato `ProblemDetails` (RFC 7807).

### 3. Eliminação de God Objects & Correção de Rotas
- A entidade `Payment` foi dividida em 7 Slices Verticais.
- Correção estrutural das rotas `/process` e `/refund` para `/tech-curse/payment/process` e `/tech-curse/payment/refund`, restaurando o padrão RESTful.

### 4. Suíte de 204 Testes Automatizados
- **Testes de Arquitetura (NetArchTest):** 23 testes garantindo isolamento de camadas e convenções de nomenclatura em um novo projeto dedicado `tech-curse.Test.Architecture`.
- **Testes Unitários:** 143 testes cobrindo todos os Handlers, Validators e Domain Specifications.
- **Testes de Integração:** 38 testes ponta a ponta com `WebApplicationFactory` cobrindo endpoints e middlewares.

---

## ⚠️ Breaking Changes & Guia de Migração

| Endpoint Antigo | Novo Endpoint / Mudança | Status Code Atualizado |
| :--- | :--- | :--- |
| `POST /process` | `POST /tech-curse/payment/process` | `200 OK` (retorna `ProcessPaymentOutputDto`) |
| `POST /refund` | `POST /tech-curse/payment/refund` | `200 OK` (retorna `RefundPaymentOutputDto`) |
| Validação de DTOs | Interceptada pelo MediatR Pipeline | `422 Unprocessable Entity` em vez de falhas genéricas |
| Aluno/Curso ausente no Enrollment | `NotFoundException` | `404 Not Found` em vez de `403 Forbidden` |
| Aluno duplicado no Enrollment | `ConflictException` | `409 Conflict` em vez de `403 Forbidden` |

---

## 👥 Contribuidores
- [@Liuizn](https://github.com/Liuizn)
```
