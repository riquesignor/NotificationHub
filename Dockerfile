FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NotificationHub.sln .
COPY src/NotificationService.Domain/NotificationService.Domain.csproj src/NotificationService.Domain/
COPY src/NotificationService.Application/NotificationService.Application.csproj src/NotificationService.Application/
COPY src/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj src/NotificationService.Infrastructure/
COPY src/NotificationService.API/NotificationService.API.csproj src/NotificationService.API/
COPY tests/NotificationService.Tests/NotificationService.Tests.csproj tests/NotificationService.Tests/
RUN dotnet restore NotificationHub.sln

COPY . .
RUN dotnet publish src/NotificationService.API/NotificationService.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

# Railway (e a maioria dos PaaS) injeta $PORT em runtime; localmente cai pra 8080.
CMD ["sh", "-c", "dotnet NotificationService.API.dll --urls http://+:${PORT:-8080}"]
