using System.Diagnostics;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Interactions;
using RunMode = Discord.Interactions.RunMode;
namespace SoundboardBot.Discord.Modules; 

public class VoiceModule : InteractionModuleBase {
    [SlashCommand("voice", "Join the voice channel.", runMode: RunMode.Async)] 
    public async Task JoinChannel(string path, IVoiceChannel? channel = null) {
        // Get the audio channel
        channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
        if (channel == null) { 
            await RespondAsync("User must be in a voice channel, or a voice channel must be passed as an argument."); 
            return; }

        // For the next step with transmitting audio, you would want to pass this Audio Client in to a service.
        var audioClient = await channel.ConnectAsync();

        await RespondAsync("Connected!");

        await SendAsync(audioClient, path);
        
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
    
    private async Task SendAsync(IAudioClient client, string path) {
        // Create FFmpeg using the previous example
        using var ffmpeg = CreateStream(path);
        await using var output = ffmpeg.StandardOutput.BaseStream;
        await using var discord = client.CreatePCMStream(AudioApplication.Mixed);
        
        try { await output.CopyToAsync(discord); }
        finally { await discord.FlushAsync(); }
    }
}
