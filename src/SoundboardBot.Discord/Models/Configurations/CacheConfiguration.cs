namespace SoundboardBot.Discord.Models.Configurations; 

public class CacheConfiguration {
    public const string Cache = "Cache";
    
    public TimeSpan Autocomplete { get; set; }
    public TimeSpan Clips { get; set; }
}
