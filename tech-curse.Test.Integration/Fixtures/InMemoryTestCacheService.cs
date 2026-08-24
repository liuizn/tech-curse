using System.Collections.Concurrent;
using System.Text.Json;
using TechCurse.src.Application.Interfaces;

namespace TechCurse.Test.Integration.Fixtures;

public class InMemoryTestCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task<T?> GetAsync<T>(string key)
    {
        if (_store.TryGetValue(key, out var json))
        {
            if (typeof(T) == typeof(string))
            {
                return Task.FromResult((T?)(object)json);
            }
            var deserialized = JsonSerializer.Deserialize<T>(json);
            return Task.FromResult(deserialized);
        }
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null)
    {
        if (value is string str)
        {
            _store[key] = str;
        }
        else
        {
            _store[key] = JsonSerializer.Serialize(value);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefixKey)
    {
        var keysToRemove = _store.Keys.Where(k => k.StartsWith(prefixKey)).ToList();
        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _store.Clear();
    }
}
