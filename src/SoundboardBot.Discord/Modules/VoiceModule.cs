using System.Diagnostics;
using Discord;
using Discord.Audio;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SoundboardBot.ApiClient;
using SoundboardBot.Discord.Core;
using SoundboardBot.Discord.Models.Configurations;
using RunMode = Discord.Interactions.RunMode;
namespace SoundboardBot.Discord.Modules; 

public class VoiceModule : InteractionModuleBase {
    private readonly SupabaseClient _client;
    private readonly CacheService _cache;
    private readonly ILogger<VoiceModule> _logger;

    public VoiceModule(SupabaseClient client, CacheService cache, ILogger<VoiceModule> logger) {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    [SlashCommand("clip", "Play the clip in the voice channel.", false, RunMode.Async)]
    public async Task PlayClip(
        [Summary("clip", "Clip to play."), Autocomplete(typeof(ClipsAutocompleteHandler))] string clip, 
        [Summary("channel", "Channel where to play the clip.")] IVoiceChannel? channel = null) {
        // Get the audio channel

        channel ??= (Context.User as IGuildUser)?.VoiceChannel;
        if (channel == null) { 
            _logger.LogWarning("User must be in a voice channel, or a voice channel must be passed as an argument");
            await FollowupAsync("User must be in a voice channel, or a voice channel must be passed as an argument."); 
            return; 
        }
        
        _logger.LogInformation("Playing clip '{Clip}' in channel '{Channel}'", clip, channel?.Name);

        await DeferAsync();
        
        var clipRes = await _client.GetClip(clip);
        if (clipRes == null) {
            _logger.LogWarning("Clip not found");
            await ModifyOriginalResponseMessageAsync("Clip not found");
            return;
        }

        var filePath = await _cache.DownloadClipAsync(clipRes.Url);

        // For the next step with transmitting audio, you would want to pass this Audio Client in to a service.
        var audioClient = await channel!.ConnectAsync(true);
        
        await ModifyOriginalResponseMessageAsync($"Playing clip '{clipRes.Key}'");

        await SendAsync(audioClient, filePath);
        
        await audioClient.StopAsync();
    }
    
    private Process? CreateStream(string path) {
        return Process.Start(new ProcessStartInfo {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });
    }
    
    private Process CreateStreamAsync(byte[] data) {
        var process = Process.Start(new ProcessStartInfo {
            FileName = "ffmpeg",
            Arguments = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true, 
            RedirectStandardInput = true,
            
        });
        
        if (process == null) 
            throw new Exception("Failed to start ffmpeg process.");
        
        Task.Run(async () => {
            await using var stdin = process.StandardInput.BaseStream;
            await stdin.WriteAsync(data);
            await stdin.FlushAsync();
            stdin.Close();
        });
        return process;
    }

    private async Task SendAsync(IAudioClient client, string path) {
        // Create FFmpeg using the previous example
        using var ffmpeg = CreateStream(path);
        
        if (ffmpeg == null) 
            throw new Exception("Failed to start ffmpeg process.");
        
        await using var output = ffmpeg.StandardOutput.BaseStream;
        await using var discord = client.CreatePCMStream(AudioApplication.Mixed);
        
        try { await output.CopyToAsync(discord); }
        finally { await discord.FlushAsync(); }
    }
    
    private async Task SendAsync(IAudioClient client, byte[] data) {
        // Create FFmpeg using the previous example
        using var ffmpeg = CreateStreamAsync(data);
        await using var output = ffmpeg.StandardOutput.BaseStream;
        await using var discord = client.CreatePCMStream(AudioApplication.Mixed);
        
        try { await output.CopyToAsync(discord); }
        finally { await discord.FlushAsync(); }
    }
    
    private async Task ModifyOriginalResponseMessageAsync(string message) {
        await ModifyOriginalResponseAsync(x => x.Content = message);
    }
}

public class ClipsAutocompleteHandler : AutocompleteHandler {
    private readonly CacheService _cache;
    
    public ClipsAutocompleteHandler(CacheService cache) {
        _cache = cache;
    }
    
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) {
        var current = autocompleteInteraction.Data.Current.Value.ToString();
        if (string.IsNullOrWhiteSpace(current)) 
            current = null;

        var results = (await _cache.GetClipsAsync());
        if (current != null) {
            results = results.Where(x => x.Key.Contains(current, StringComparison.OrdinalIgnoreCase));
        }


        // max - 25 suggestions at a time (API limit)
        return AutocompletionResult.FromSuccess(results
                .Take(25)
                .Select(x => new AutocompleteResult(x.Description, x.Key)));
    }
}