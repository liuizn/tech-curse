# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:f5b3b2e2e548828d50e349726f51a5de001286f02c4bbde77db0dd34eb9f55ff AS base
USER app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

COPY ["Directory.Build.props", "Directory.Packages.props", "./"]

COPY ["src/Domain/TechCurse.Domain.csproj", "src/Domain/"]
COPY ["src/Application/TechCurse.Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/TechCurse.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Api/TechCurse.Api.csproj", "src/Api/"]

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "src/Api/TechCurse.Api.csproj" \
    --use-current-runtime \
    -p:SelfContained=false \
    -p:PublishReadyToRun=true

COPY src/ src/

WORKDIR "/build/src/Api"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "TechCurse.Api.csproj" \
    -c Release \
    --use-current-runtime \
    --no-self-contained \
    -o /app/publish \
    -p:UseAppHost=false \
    -p:PublishReadyToRun=true

FROM base AS final

ARG GIT_REVISION=unknown

LABEL org.opencontainers.image.source="https://github.com/Liuizn/tech-curse" \
      org.opencontainers.image.revision="${GIT_REVISION}" \
      org.opencontainers.image.licenses="Apache-2.0"

WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TechCurse.Api.dll"]
