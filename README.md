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

1. Uma app (identificada pela sua chave de API) manda
   `POST /api/notifications/send` com canal primário e fallbacks. Se a
   `idempotencyKey` já foi usada por essa app, nada é reenviado.
2. O `NotificationOrchestrator` tenta o canal primário (normalmente Push);
   se falhar, cai pro próximo da lista (Email).
3. Cada tentativa é registrada em `NotificationLog` com status, mensagem de
   erro e id retornado pelo provider.
4. Um job do Hangfire (`RetryFailedNotificationsJob`) roda a cada hora e
   reentrega (a mesma notificação, sem criar cópia) as que estão `Failed`
   com menos de 3 tentativas.
5. Tokens que a Expo responde como `DeviceNotRegistered` (app desinstalado)
   são desativados automaticamente.

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
    └── NotificationService.Tests/          → xUnit + Moq: orquestrador, push, aparelhos, chaves, modelo EF
```

## Endpoints

| Método | Rota | Chave | Descrição |
| --- | --- | --- | --- |
| `POST` | `/api/notifications/send` | secreta | Envia notificação (canal primário + fallbacks) |
| `POST` | `/api/notifications/send-template` | secreta | Envia usando um template salvo |
| `GET` | `/api/notifications/{id}/status` | secreta | Status + logs de tentativas |
| `GET` | `/api/templates` | secreta | Lista templates da app |
| `POST` | `/api/devices/register` | pública | Registra/atualiza o aparelho (token + atributos) |
| `POST` | `/api/devices/unregister` | pública | Desativa o aparelho (ex.: usuário desligou notificações) |
| `GET` | `/api/devices` | secreta | Lista aparelhos ativos, com filtro `?userId=` e `?attr[chave]=valor` |

### Apps e chaves de API

Cada app consumidora é cadastrada na seção `ClientApps` da configuração
com um `Id` e **duas chaves**, enviadas no header `X-Api-Key`:

- **Pública**: pode ir embutida no app do celular. Só registra ou remove
  o próprio aparelho.
- **Secreta**: só em servidor, job ou backend. Envia notificações e lista
  aparelhos.

Tudo (aparelhos, notificações, templates, chaves de idempotência) é
separado por app: uma app nunca vê nem notifica os usuários de outra.

```json
"ClientApps": [
  { "Id": "meu-app", "Name": "Meu App", "PublicKey": "<aleatória>", "SecretKey": "<outra aleatória>" }
]
```

**Modo legado:** se nenhuma app estiver cadastrada, a API funciona sem
chave (como nas versões anteriores) e loga um aviso na inicialização. Serve
para não quebrar consumidoras antigas; não exponha o hub publicamente assim.

> Com a chave pública, um app pode registrar um token sob qualquer
> `userId`. Para apps com login, prefira que o **backend** da app registre
> o aparelho com a chave secreta. Para apps sem login, use um id aleatório
> por instalação (UUID), que não dá para adivinhar.

### Recursos do envio

```json
{
  "userId": "user-123",
  "title": "Pedido enviado",
  "message": "Seu pedido saiu para entrega",
  "primaryChannel": "push",
  "fallbackChannels": ["email"],
  "data": { "route": "/pedidos/42" },
  "idempotencyKey": "pedido-42-enviado",
  "push": { "priority": "high", "channelId": "pedidos", "ttlSeconds": 3600 }
}
```

- `idempotencyKey` (opcional): repetir o envio com a mesma chave devolve a
  notificação original (`duplicate: true`) sem reenviar. Ideal para jobs
  que reavaliam a mesma condição de tempos em tempos.
- `push` (opcional): prioridade, canal do Android e por quanto tempo tentar
  entregar se o aparelho estiver offline (repassados à Expo Push API).
- Atributos do aparelho (`attributes` no registro) são chave-valor livres
  (ex.: idioma, região, preferências). O hub não interpreta, só guarda e
  permite filtrar em `GET /api/devices`.

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

Integrado também com o Sereno (app de clima): o app registra o aparelho
com atributos (localização e preferências de alerta) e um job agendado
lista os aparelhos, avalia a previsão e envia com `idempotencyKey`.

Pendências para próximas versões: migrations do EF Core, seed de
templates, testes de integração dos controllers (precisam de Postgres),
endpoint pra registrar/atualizar email do usuário (hoje o fallback de email
só funciona se algo popular a tabela `Users`), filtro de atributos no banco
(jsonb + índice GIN; hoje é em memória) e consulta dos *receipts* da Expo
para preencher o status `Delivered`.
