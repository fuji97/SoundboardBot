using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundboardBot.ApiClient.Models;
using SoundboardBot.ApiClient.Utils;
using Supabase;
namespace SoundboardBot.ApiClient; 

public class SupabaseClient {
    private readonly ILogger<SupabaseClient> _logger;
    private readonly Client _client;
    
    public SupabaseClient(ILogger<SupabaseClient> logger, string url, string key) {
        _logger = logger;
        
        var options = new SupabaseOptions {
            AutoRefreshToken = true,
            AutoConnectRealtime = true
        };
        
        _client = new Client(url, key, options);
        _logger.LogDebug("Supabase client created");
    }

    public async Task<IEnumerable<Clip>> GetClips(string? filter = null) {
        _logger.LogInformation("Get clips from Supabase");

        var query = _client.From<Clip>();
        if (filter != null) {
            query.Where(x => x.Key.Contains(filter, StringComparison.InvariantCultureIgnoreCase) || x.Description.Contains(filter, StringComparison.InvariantCultureIgnoreCase));
        }
        
        return (await query.Get()).Models;
    }

    public async Task<Clip?> GetClip(string key) {
        _logger.LogInformation("Get clip with key '{Key}' from Supabase", key);
        
        var result = await _client.From<Clip>()
            .Where(x => x.Key == key)
            .Single();

        return result;
    }

    public async Task<byte[]> DownloadClip(string url) {
        _logger.LogInformation("Downloading clip with url name '{Url}'", url);
        var bytes = await _client.Storage.From("clips").Download(url);
        _logger.LogDebug("Download completed - size: {Size}", Utilities.BytesToString(bytes.Length));
        return bytes;
    }
}

public static class ApiClientExtensions {
    public static IServiceCollection AddSupabaseClient(this IServiceCollection services, string url, string key) {
        services.AddScoped<SupabaseClient>(x => new SupabaseClient(x.GetRequiredService<ILogger<SupabaseClient>>(), url, key));
        return services;
    }
}