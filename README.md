# NotificationHub

Sistema centralizado de notificações multi-canal (Push + Email) com retry
automático, fallback inteligente e rastreamento de delivery. Projetado para
ser integrado em qualquer aplicação (web, mobile, backend).

**Stack**: C# / .NET 8 · ASP.NET Core Web API · EF Core (PostgreSQL) ·
Firebase Cloud Messaging (push) · SendGrid (email) · Hangfire (jobs)

Só usa serviços com tier gratuito — por isso não há canal de SMS/Twilio.

## Como funciona

1. Uma app manda `POST /api/notifications/send` com canal primário e
   fallbacks.
2. O `NotificationOrchestrator` tenta o canal primário (normalmente Push);
   se falhar, cai pro próximo da lista (Email).
3. Cada tentativa é registrada em `NotificationLog` com status, mensagem de
   erro e id retornado pelo provider.
4. Um job do Hangfire (`RetryFailedNotificationsJob`) roda a cada hora e
   reprocessa notificações com status `Failed` e menos de 3 tentativas.

## Estrutura

```
NotificationHub/
├── NotificationHub.sln
├── src/
│   ├── NotificationService.API/            → controllers, Program.cs, config
│   ├── NotificationService.Application/    → orquestração, serviços de envio, jobs, validação
│   ├── NotificationService.Domain/         → entidades, enums, contratos de repositório
│   └── NotificationService.Infrastructure/ → EF Core, repositórios, DI de Firebase/SendGrid/Hangfire
└── tests/
    └── NotificationService.Tests/          → testes do orquestrador (xUnit + Moq)
```

## Endpoints

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/notifications/send` | Envia notificação (canal primário + fallbacks) |
| `POST` | `/api/notifications/send-template` | Envia usando um template salvo |
| `GET` | `/api/notifications/{id}/status` | Status + logs de tentativas |
| `GET` | `/api/templates` | Lista templates cadastrados |
| `POST` | `/api/devices/register` | Registra/atualiza device token para push |

## Setup local

### 1. Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — **não
  está instalado nesta máquina**, instale antes de restaurar/buildar.
- PostgreSQL rodando localmente (ou ajuste a connection string).
- Projeto no Firebase com Cloud Messaging habilitado.
- Conta SendGrid (tier free cobre o volume de um projeto pessoal).

### 2. Restaurar e buildar

```bash
dotnet restore
dotnet build
```

### 3. Credenciais

```bash
# Firebase: baixe a service account key no Console e salve como:
src/NotificationService.API/firebase-key.json

# SendGrid: exporte a API key (ou edite appsettings.Development.json)
export SendGrid__ApiKey="SG.xxxxxxxxxxxx"
```

### 4. Banco de dados

Ainda não há migrations geradas (dependem do SDK instalado). Depois de
instalar o .NET 8 SDK:

```bash
cd src/NotificationService.API
dotnet tool install --global dotnet-ef   # se ainda não tiver
dotnet ef migrations add InitialCreate --project ../NotificationService.Infrastructure
dotnet ef database update --project ../NotificationService.Infrastructure
```

### 5. Rodar

```bash
cd src/NotificationService.API
dotnet run

# Swagger: http://localhost:5080/swagger
# Hangfire Dashboard: http://localhost:5080/hangfire
```

## Rodar os testes

```bash
dotnet test
```

## Status desta versão

Este é o esqueleto inicial gerado a partir da especificação técnica do
projeto (entidades, orquestrador com fallback, serviços de Push/Email,
job de retry, controllers e testes unitários do orquestrador).

**Ainda não validado com `dotnet build`/`dotnet restore`** porque o SDK do
.NET não está instalado nesta máquina — instale o SDK e rode os comandos
acima para confirmar que compila. Os nomes de pacotes NuGet (versões do
EF Core, FirebaseAdmin, SendGrid, Hangfire, FluentValidation, xUnit) foram
escolhidos com base em versões estáveis conhecidas para .NET 8, mas o
`dotnet restore` pode pedir ajuste fino se alguma tiver saído de linha.

Pendências para próximas versões: migrations do EF Core, seed de
templates, testes de integração dos controllers, e (se algum dia precisar)
autenticação da API.
