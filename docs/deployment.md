# Deploy do NotificationHub

## Por que localhost não basta

`dotnet run` só fica de pé enquanto seu PC estiver ligado. Pra qualquer app
consumidora (ex.: um backend de chat) chamar o NotificationHub 24/7, ele
precisa estar hospedado em algum lugar com URL fixa.

## Onde hospedar de graça

| Critério | Vercel | Railway | Azure (Free Tier) |
|---|---|---|---|
| Grátis | Sempre (mas não é feito pra .NET) | Primeiros US$5/mês | 12 meses |
| Suporte a .NET | Via container | Nativo | Nativo |
| PostgreSQL incluso | Não (precisa externo) | Sim, grátis | Não (precisa externo) |
| Setup | ~2 min | ~3 min | ~10 min, pede cartão |

**Recomendado: Railway** — é o único com Postgres incluso e detecção nativa
de .NET, sem precisar de cartão pra começar.

## Deploy no Railway

1. Suba este repositório pro GitHub (se ainda não estiver lá).
2. Crie conta em [railway.app](https://railway.app) e conecte o GitHub.
3. **New Project → Deploy from GitHub repo** → escolha `NotificationHub`.
   Railway usa o [Dockerfile](../Dockerfile) da raiz pra buildar (ou
   Nixpacks, se preferir remover o Dockerfile).
4. **New → Database → PostgreSQL** dentro do mesmo projeto Railway.
5. No serviço da API, aba **Variables**, configure as variáveis da tabela
   abaixo.
6. Depois do primeiro deploy, rode as migrations (veja
   [README.md](../README.md#4-banco-de-dados)) apontando pra connection
   string do Postgres do Railway.
7. Teste `GET /swagger` e `POST /api/devices/register` na URL gerada.

## Variáveis de ambiente necessárias

Configure isso direto no painel do Railway (ou do host que escolher) —
**nunca cole valores de chave/senha aqui no chat**, eu não preciso ver o
conteúdo, só preciso saber que você configurou:

| Variável | O que é | Onde conseguir |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | ambiente | fixo: `Production` |
| `ConnectionStrings__DefaultConnection` | string de conexão do Postgres | gerada automaticamente pelo add-on PostgreSQL do Railway (copie do painel dele) |
| `Firebase__CredentialsJson` | JSON da service account do Firebase, em uma linha só | Firebase Console → ⚙️ Configurações do projeto → Contas de serviço → **Gerar nova chave privada** |
| `SendGrid__ApiKey` | API key do SendGrid | SendGrid → Settings → API Keys → Create API Key |
| `SendGrid__FromEmail` | email remetente | precisa estar verificado no SendGrid (Sender Authentication) |
| `SendGrid__FromName` | nome exibido no remetente | à sua escolha, ex. `NotificationHub` |

Notas:
- `Firebase__CredentialsJson` existe porque em produção não dá pra "colocar"
  o arquivo `firebase-key.json` no container com segurança — a variável
  `Firebase:CredentialsJson` já é lida em
  [`DependencyInjection.cs`](../src/NotificationService.Infrastructure/DependencyInjection.cs)
  como alternativa ao arquivo (que continua funcionando só em dev local).
- Localmente, prefira `dotnet user-secrets` em vez de editar
  `appsettings.Development.json` com chaves reais, pra não correr risco de
  commitar por engano:
  ```bash
  cd src/NotificationService.API
  dotnet user-secrets init
  dotnet user-secrets set "SendGrid:ApiKey" "SG.xxxx"
  dotnet user-secrets set "Firebase:CredentialsJson" "$(cat firebase-key.json)"
  ```

## Telegram Bot API (mencionado, ainda não implementado)

O material de deployment cita o Telegram Bot API como um terceiro canal
grátis (além de Push e Email), mas isso ficou só no diagrama de
arquitetura — não havia código de integração no material. Não implementei
esse canal agora pra não adicionar funcionalidade sem confirmação; se
quiser, é um `Channel.Telegram` + `ITelegramNotificationService` novo,
seguindo o mesmo padrão do `EmailNotificationService`.

## Integração com apps consumidoras (ex.: um backend de chat)

Qualquer backend externo consome o NotificationHub só por HTTP, usando a
URL pública do deploy:

- `POST {URL}/api/devices/register` — registra o token de push do usuário
  ao fazer login no app cliente.
- `POST {URL}/api/notifications/send` — envia notificação (canal primário +
  fallbacks).
- `GET {URL}/api/notifications/{id}/status` — status e logs das tentativas.

Os contratos completos (body de cada request) estão na tabela de Endpoints
do [README.md](../README.md#endpoints) principal. O NotificationHub não
guarda lógica de autenticação de outros apps — se a app consumidora exigir
isso, é ela que decide antes de chamar o NotificationHub.
