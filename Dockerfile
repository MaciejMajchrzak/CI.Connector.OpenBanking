ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

ARG API_PROJECT=src/CI.Connector.OpenBanking.API/CI.Connector.OpenBanking.API.csproj

COPY nuget.config .
COPY ["src/CI.Connector.OpenBanking.Domain/CI.Connector.OpenBanking.Domain.csproj",                 "src/CI.Connector.OpenBanking.Domain/"]
COPY ["src/CI.Connector.OpenBanking.Core/CI.Connector.OpenBanking.Core.csproj",                     "src/CI.Connector.OpenBanking.Core/"]
COPY ["src/CI.Connector.OpenBanking.Infrastructure/CI.Connector.OpenBanking.Infrastructure.csproj", "src/CI.Connector.OpenBanking.Infrastructure/"]
COPY ["src/CI.Connector.OpenBanking.API/CI.Connector.OpenBanking.API.csproj",                       "src/CI.Connector.OpenBanking.API/"]
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username ci \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text && \
    dotnet restore ${API_PROJECT}

COPY . .
RUN dotnet publish ${API_PROJECT} -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CI.Connector.OpenBanking.API.dll"]
