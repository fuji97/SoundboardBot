using Discord.Interactions;
namespace SoundboardBot.Discord.Modules; 

public class HelloWorldModule : InteractionModuleBase {
    [SlashCommand("hello", "Send an Hello World!")]
    public async Task Hello() {
        await RespondAsync("Hello World!");
    }
}
