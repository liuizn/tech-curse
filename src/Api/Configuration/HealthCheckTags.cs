namespace TechCurse.Api.Configuration;

/// <summary>
/// Tags usadas para separar liveness de readiness nos health checks.
/// <para>
/// Liveness ("o processo está de pé?") não pode depender de recurso externo:
/// se um SQL Server fora do ar reprovar o liveness, o orquestrador reinicia um
/// processo que estava perfeitamente saudável, e o restart não conserta o banco.
/// Readiness ("dá para receber tráfego?") é justamente o oposto — só aí as
/// dependências entram na conta.
/// </para>
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Marca uma verificação como dependência externa exigida para receber
    /// tráfego. Só os checks com esta tag participam de <c>/health/ready</c>.
    /// </summary>
    public const string Ready = "ready";
}
