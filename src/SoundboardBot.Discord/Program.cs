using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoundboardBot.ApiClient;
using SoundboardBot.Discord;
using SoundboardBot.Discord.Core;
using SoundboardBot.Discord.Models.Configurations;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder => {
        builder.AddCommandLine(args);
    })
    .ConfigureAppConfiguration((host, conf) => {
        conf.AddYamlFile("appsettings.yaml", true, true);
        conf.AddYamlFile($"appsettings.{host.HostingEnvironment.EnvironmentName}.yaml", true, true);
    })
    .ConfigureServices((host, services) => {
        var discordToken = host.Configuration["Discord:Token"]!;
        var supabaseUrl = host.Configuration["Supabase:Url"]!;
        var supabaseSecret = host.Configuration["Supabase:Secret"]!;

        services.AddMemoryCache();
        services.AddScoped<CacheService>();
        services.AddSupabaseClient(supabaseUrl, supabaseSecret);
        services.AddDiscordBot(discordToken);
        
        services.Configure<CacheConfiguration>(host.Configuration.GetSection(CacheConfiguration.Cache));
    })
    .Build();

await host.RunAsync();
