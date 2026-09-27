# NotificationHub

Sistema centralizado de notificações multi-canal (Push + Email) com retry
automático, fallback inteligente e rastreamento de delivery. Projetado para
ser integrado em qualquer aplicação (web, mobile, backend).

**Stack**: C# / .NET 8 · ASP.NET Core Web API · EF Core (PostgreSQL) ·
Expo Push API (push) · SendGrid (email) · Hangfire (jobs)

Só usa serviços com tier gratuito — por isso não há canal de SMS/Twilio.
Push é via [Expo Push API](https://docs.expo.dev/push-notifications/sending-notifications/)
(sem credencial nenhuma) porque os apps consumidores até agora usam Expo,
que gera `ExponentPushToken[...]` e não token nativo de FCM.

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
│   └── NotificationService.Infrastructure/ → EF Core, repositórios, DI de SendGrid/Hangfire
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

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — já
  instalado nesta máquina em `~/.dotnet` (via `dotnet-install.sh`, sem apt).
- PostgreSQL rodando localmente (ou ajuste a connection string).
- Conta SendGrid (tier free cobre o volume de um projeto pessoal) — só se
  for usar o canal de email; push não pede nenhuma credencial.

### 2. Restaurar e buildar

```bash
dotnet restore
dotnet build
```

### 3. Credenciais

Push (Expo) não precisa de nenhuma credencial. Só o canal de email pede
uma:

```bash
export SendGrid__ApiKey="SG.xxxxxxxxxxxx"
```

Prefira `dotnet user-secrets` a colocar chaves reais em
`appsettings.Development.json` (detalhes em
[docs/deployment.md](docs/deployment.md)).

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

## Deploy

Guia completo (comparação de hosts grátis, passo a passo no Railway,
variáveis de ambiente necessárias) em
[docs/deployment.md](docs/deployment.md). Já tem [Dockerfile](Dockerfile)
na raiz pronto pro build em container.

## Status desta versão

Build e testes (`dotnet build` + `dotnet test`) passam limpos, 0 erros/
avisos. `dotnet run` sobe até faltar um Postgres real pra conectar — ou
seja, toda a injeção de dependência, EF Core e Hangfire estão corretos; só
falta infraestrutura externa (banco) pra rodar de ponta a ponta.

Integrado com o [Symbius](../Symbius) (app do TCC): `UserId` é string
(compatível com UID do Firebase Auth) e o push vai pela Expo Push API
(compatível com `ExponentPushToken[...]`).

Pendências para próximas versões: migrations do EF Core, seed de
templates, testes de integração dos controllers, endpoint pra
registrar/atualizar email do usuário (hoje o fallback de email só funciona
se algo popular a tabela `Users`), e (se algum dia precisar) autenticação
da API.
