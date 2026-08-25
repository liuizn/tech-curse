# syntax=docker/dockerfile:1.4

# ===================================================
# 1. RUNTIME BASE (Imagem Chiseled Ultraleve e Segura)
# ===================================================
# Variante "-extra": chiseled (sem shell, sem gerenciador de pacotes, non-root),
# porem com ICU. A variante sem "-extra" nao traz ICU, e o Microsoft.Data.SqlClient
# recusa operar sem globalizacao — era a causa do 503 no /health.
#
# Pinado por digest para build reproduzivel: a tag flutua e o mesmo commit
# passaria a produzir imagens diferentes com o tempo. A tag e mantida na
# referencia so como documentacao — quem resolve e o digest.
# Para atualizar o pin:
#   curl -sI -H "Accept: application/vnd.oci.image.index.v1+json" \
#     https://mcr.microsoft.com/v2/dotnet/aspnet/manifests/10.0-noble-chiseled-extra \
#     | grep -i docker-content-digest
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:f5b3b2e2e548828d50e349726f51a5de001286f02c4bbde77db0dd34eb9f55ff AS base
USER app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

# ===================================================
# 2. BUILD STAGE (Compilação com Cache de NuGet)
# ===================================================
# Tambem pinado por digest (mesmo procedimento de atualizacao do estagio base,
# trocando "aspnet" por "sdk" e a tag por 10.0).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Central Package Management: os .csproj nao carregam Version, entao o restore
# depende destes dois arquivos da raiz. Precisam vir antes dos projetos.
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]

# Copia APENAS os projetos que compõem a aplicação (sem projetos de teste)
COPY ["src/Domain/TechCurse.Domain.csproj", "src/Domain/"]
COPY ["src/Application/TechCurse.Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/TechCurse.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Api/TechCurse.Api.csproj", "src/Api/"]

# Restaura dependências com Cache Mount do BuildKit.
#
# --use-current-runtime e -p:PublishReadyToRun=true precisam estar AQUI, e nao
# apenas no publish: o R2R exige um RuntimeIdentifier, e um restore sem RID nao
# grava o alvo correspondente no project.assets.json nem baixa os runtime packs
# do crossgen. Sem isso, o publish --no-restore falha com NETSDK1047/NETSDK1112.
# --use-current-runtime resolve o RID do proprio container de build, entao a
# imagem continua construivel em amd64 e arm64 sem arquitetura fixa no arquivo.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "src/Api/TechCurse.Api.csproj" \
    --use-current-runtime \
    -p:SelfContained=false \
    -p:PublishReadyToRun=true

# Copia todo o código-fonte
COPY src/ src/

# Publica a aplicação otimizada com ReadyToRun (R2R).
# --no-restore: o restore ja rodou acima, numa camada que so invalida quando um
# .csproj muda. Sem a flag, o publish refazia o restore inteiro a cada alteracao
# de codigo-fonte. As flags de RID repetem as do restore — precisam bater, senao
# o publish procura um alvo que o assets file nao tem.
# --no-self-contained: a imagem final e a runtime "aspnet", entao o publish
# continua framework-dependent; declarar um RID sozinho poderia virar
# self-contained e inchar a imagem com o runtime inteiro.
WORKDIR "/build/src/Api"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "TechCurse.Api.csproj" \
    -c Release \
    --use-current-runtime \
    --no-self-contained \
    --no-restore \
    -o /app/publish \
    -p:UseAppHost=false \
    -p:PublishReadyToRun=true

# ===================================================
# 3. FINAL IMAGE (Apenas os binários compilados)
# ===================================================
FROM base AS final

# Revisao do commit que originou a imagem. O pipeline passa o SHA real; um build
# local sem o arg fica com "unknown", que e honesto — melhor que um valor falso.
ARG GIT_REVISION=unknown

# Labels OCI. "source" e o que liga o pacote publicado ao repositorio no GitHub
# Container Registry: sem ele, o pacote aparece orfao, sem link para o codigo.
# No CI o docker/metadata-action sobrescreve estes valores com os reais; estes
# aqui sao a linha de base para qualquer build fora do pipeline.
LABEL org.opencontainers.image.source="https://github.com/Liuizn/tech-curse" \
      org.opencontainers.image.revision="${GIT_REVISION}" \
      org.opencontainers.image.licenses="Apache-2.0"

WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TechCurse.Api.dll"]
