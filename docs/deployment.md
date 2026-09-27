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
| `SendGrid__ApiKey` | API key do SendGrid (só se for usar o canal de email) | SendGrid → Settings → API Keys → Create API Key |
| `SendGrid__FromEmail` | email remetente | precisa estar verificado no SendGrid (Sender Authentication) |
| `SendGrid__FromName` | nome exibido no remetente | à sua escolha, ex. `NotificationHub` |

Push não entra nessa tabela: vai pela [Expo Push
API](https://docs.expo.dev/push-notifications/sending-notifications/), que
não pede credencial nenhuma (sem conta, sem chave).

Nota: localmente, prefira `dotnet user-secrets` em vez de editar
`appsettings.Development.json` com chaves reais, pra não correr risco de
commitar por engano:
```bash
cd src/NotificationService.API
dotnet user-secrets init
dotnet user-secrets set "SendGrid:ApiKey" "SG.xxxx"
```

## Telegram Bot API (mencionado, ainda não implementado)

O material de deployment cita o Telegram Bot API como um terceiro canal
grátis (além de Push e Email), mas isso ficou só no diagrama de
arquitetura — não havia código de integração no material. Não implementei
esse canal agora pra não adicionar funcionalidade sem confirmação; se
quiser, é um `Channel.Telegram` + `ITelegramNotificationService` novo,
seguindo o mesmo padrão do `EmailNotificationService`.

## Integração com apps consumidoras

Qualquer app consome o NotificationHub só por HTTP, usando a URL pública
do deploy. `userId` é sempre string — qualquer id externo serve (UID do
Firebase Auth, id de outro provedor, etc.), não precisa ser Guid:

- `POST {URL}/api/devices/register` — registra o token de push do usuário
  ao fazer login no app cliente. Para apps Expo, `token` é o valor de
  `getExpoPushTokenAsync()` (formato `ExponentPushToken[...]`).
- `POST {URL}/api/notifications/send` — envia notificação (canal primário +
  fallbacks).
- `GET {URL}/api/notifications/{id}/status` — status e logs das tentativas.

Os contratos completos (body de cada request) estão na tabela de Endpoints
do [README.md](../README.md#endpoints) principal. O NotificationHub não
guarda lógica de autenticação de outros apps — se a app consumidora exigir
isso, é ela que decide antes de chamar o NotificationHub.

Quando o app consumidor não tem backend próprio (caso do Symbius: é só
React Native + Firebase, sem servidor HTTP dele), quem chama
`/api/notifications/send` é o próprio cliente, direto do dispositivo de
quem está enviando a mensagem/ação — o que importa é que essa chamada
HTTP aconteça a partir de um processo que já está rodando (o remetente,
que está com o app aberto), não do destinatário (que pode estar com o
app fechado). O push então chega no destinatário via Expo/FCM
independente do app dele estar aberto ou não.
