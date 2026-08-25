using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.Configuration;

/// <summary>
/// Persistência do chaveiro do ASP.NET Data Protection.
/// </summary>
/// <remarks>
/// Sem configuração explícita o ASP.NET grava as chaves em
/// <c>/home/app/.aspnet/DataProtection-Keys</c> dentro do container — o próprio
/// framework avisa no log que esse diretório "may not be persisted outside of the
/// container". Na prática isso significa que tudo que depende do chaveiro (cookies,
/// tokens de reset de senha, tokens de confirmação de e-mail, antiforgery) é
/// invalidado a cada redeploy, e que duas réplicas nunca conseguem ler o que a
/// outra protegeu.
///
/// Optou-se pelo banco (<c>PersistKeysToDbContext</c>) em vez de um volume nomeado
/// no Compose por duas razões:
///
/// 1. A imagem final é <em>chiseled</em> e roda como o usuário não-root <c>app</c>.
///    Um volume nomeado montado num caminho que ainda não existe na imagem é criado
///    pelo Docker com dono <c>root</c>, e o processo perderia a permissão de escrita —
///    e, sem shell na imagem chiseled, não há como corrigir o dono em runtime.
/// 2. O chaveiro em arquivo só é compartilhado entre réplicas com um volume de rede.
///    O SQL Server já é uma dependência compartilhada, com backup e migrations, então
///    guardar as chaves lá resolve o caso multi-réplica sem infraestrutura nova.
///
/// <c>SetApplicationName</c> é obrigatório aqui: o nome da aplicação entra na
/// derivação das chaves, e o padrão deriva do caminho físico do conteúdo, que muda
/// entre host e container. Fixá-lo garante que qualquer instância leia o mesmo chaveiro.
/// </remarks>
public static class DataProtectionSetup
{
    /// <summary>Nome lógico da aplicação usado na derivação das chaves.</summary>
    private const string NomeDaAplicacao = "TechCurse";

    public static IServiceCollection AddDataProtectionSetup(this IServiceCollection services)
    {
        services
            .AddDataProtection()
            .SetApplicationName(NomeDaAplicacao)
            // O ciclo de vida padrão do framework é de 90 dias, com rotação automática
            // e retenção das chaves antigas para decriptação. Mantido de propósito.
            .PersistKeysToDbContext<TechCurseContext>();

        return services;
    }
}
