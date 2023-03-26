using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoundboardBot.ApiClient;
using SoundboardBot.ApiClient.Models;
using SoundboardBot.Discord.Models.Configurations;
namespace SoundboardBot.Discord.Core; 

public class CacheService {
    private readonly CacheConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly SupabaseClient _client;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IOptions<CacheConfiguration> config, IMemoryCache cache, SupabaseClient client, ILogger<CacheService> logger) {
        _config = config.Value;
        _client = client;
        _logger = logger;
        _cache = cache;
    }
    
    public async ValueTask<IEnumerable<Clip>> GetClipsAsync() {
        return await _cache.GetOrCreateAsync("clips", async entry => {
            _logger.LogDebug("Clips cache miss, obtaining new data");
            entry.AbsoluteExpirationRelativeToNow = _config.Autocomplete;
            return await _client.GetClips();
        }) ?? Array.Empty<Clip>();
    }
    
    public async ValueTask<string> DownloadClipAsync(string url) {
        var fileName = (await _cache.GetOrCreateAsync(GetDownloadClipKey(url), async entry => {
            _logger.LogDebug("Clip file cache missing, downloading new file");
            entry.AbsoluteExpirationRelativeToNow = _config.Clips;
            return await DownloadAndCreateFile(url);
        }));

        if (File.Exists(fileName)) 
            return fileName;
        
        _logger.LogDebug("Cache hit but the file is missing, downloading new file");
        fileName = await DownloadAndCreateFile(url);
        _cache.Set(GetDownloadClipKey(url), fileName, _config.Clips);

        return fileName;
    }
    private async Task<string> DownloadAndCreateFile(string url) {
        var bytes = await _client.DownloadClip(url);
        return CreateTemporaryFile(url, bytes);
    }

    public void ClearCache() {
        _cache.Remove("clips");
        
        // Clear all download cache entries
        var tempPath = Path.Combine(Path.GetTempPath(), "SoundboardBot");
        var files = Directory.GetFiles(tempPath);
        foreach (var file in files) {
            File.Delete(file);
        }
        
        _logger.LogInformation("Cache cleared");
    }
    
    private static string GetDownloadClipKey(string url) => $"file/{url}";

    private static string CreateTemporaryFile(string filename, byte[] data) {
        var fileInfo = new FileInfo(Path.Combine(Path.GetTempPath(), "SoundboardBot", filename));

        fileInfo.Directory!.Create();
        File.WriteAllBytes(fileInfo.FullName, data);
        return fileInfo.FullName;
    }
}
