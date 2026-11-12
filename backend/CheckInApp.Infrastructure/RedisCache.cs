using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
namespace CheckInApp.Infrastructure;

public sealed class RedisCache(IConfiguration config) : IDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>> connection = new(() =>
    {
        var options = ConfigurationOptions.Parse(config.GetConnectionString("Redis") ?? "127.0.0.1:6379");
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 500;
        options.AsyncTimeout = 500;
        options.ConnectRetry = 0;
        return ConnectionMultiplexer.ConnectAsync(options);
    });
    public async Task<JsonElement?> Get(string key)
    {
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("Redis")))
            return null;
        try
        {
            var c = await connection.Value;
            var value = await c.GetDatabase().StringGetAsync(key);
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<JsonElement>(value.ToString());
        }
        catch (RedisException) { return null; }
    }
    public async Task Set(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("Redis")))
            return;
        try
        {
            var c = await connection.Value;
            await c.GetDatabase().StringSetAsync(
                key,
                JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                TimeSpan.FromSeconds(30));
        }
        catch (RedisException) {/* SQL remains authoritative; cache failure must not lose attendance. */}
    }
    public void Dispose()
    {
        if (connection.IsValueCreated && connection.Value.IsCompletedSuccessfully)
            connection.Value.Result.Dispose();
    }
}
