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

        services.AddHealthChecks().AddRedis(
            cacheConnectionString,
            name: "Cache_Redis",
            tags: [HealthCheckTags.Ready]);

        return services;
    }
}
