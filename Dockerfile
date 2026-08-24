# syntax=docker/dockerfile:1.4

# ===================================================
# 1. RUNTIME BASE (Imagem Chiseled Ultraleve e Segura)
# ===================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
USER app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

# ===================================================
# 2. BUILD STAGE (Compilação com Cache de NuGet)
# ===================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Copia APENAS os projetos que compõem a aplicação (sem projetos de teste)
COPY ["tech-curse/src/Domain/tech-curse.Domain.csproj", "tech-curse/src/Domain/"]
COPY ["tech-curse/src/Application/tech-curse.Application.csproj", "tech-curse/src/Application/"]
COPY ["tech-curse/src/Infrastructure/tech-curse.Infrastructure.csproj", "tech-curse/src/Infrastructure/"]
COPY ["tech-curse/src/API/tech-curse.API.csproj", "tech-curse/src/API/"]

# Restaura dependências com Cache Mount do BuildKit
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "tech-curse/src/API/tech-curse.API.csproj"

# Copia todo o código-fonte
COPY tech-curse/src/ tech-curse/src/

# Publica a aplicação otimizada com ReadyToRun (R2R)
WORKDIR "/src/tech-curse/src/API"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "tech-curse.API.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

# ===================================================
# 3. FINAL IMAGE (Apenas os binários compilados)
# ===================================================
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "tech-curse.API.dll"]