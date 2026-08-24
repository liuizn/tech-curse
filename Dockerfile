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
WORKDIR /build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Copia APENAS os projetos que compõem a aplicação (sem projetos de teste)
COPY ["src/Domain/TechCurse.Domain.csproj", "src/Domain/"]
COPY ["src/Application/TechCurse.Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/TechCurse.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Api/TechCurse.Api.csproj", "src/Api/"]

# Restaura dependências com Cache Mount do BuildKit
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "src/Api/TechCurse.Api.csproj"

# Copia todo o código-fonte
COPY src/ src/

# Publica a aplicação otimizada com ReadyToRun (R2R)
WORKDIR "/build/src/Api"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "TechCurse.Api.csproj" \
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
ENTRYPOINT ["dotnet", "TechCurse.Api.dll"]