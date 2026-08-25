using StackExchange.Redis;

namespace TechCurse.Api.Configuration;

public static class CacheRedisSetup
{
    public static IServiceCollection AddRedisCacheSetup(this IServiceCollection services, IConfiguration configuration)
    {
        var cacheConnectionString =
            configuration.GetConnectionString("RedisCache")
            ?? throw new InvalidOperationException("Connection string 'RedisCache' not found.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = cacheConnectionString;
            options.InstanceName = "TechCurseAPI_";
        });

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(cacheConnectionString));

        // Tag "ready": mesma lógica do banco — cache indisponível degrada o
        // atendimento, não invalida o processo. Só entra em /health/ready.
        services.AddHealthChecks().AddRedis(
            cacheConnectionString,
            name: "Cache_Redis",
            tags: [HealthCheckTags.Ready]);

        return services;
    }
}
