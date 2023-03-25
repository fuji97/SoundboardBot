using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoundboardBot.Discord;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder => {
        builder.AddCommandLine(args);
    })
    .ConfigureAppConfiguration((host, conf) => {
        conf.AddYamlFile("appsettings.yaml", true, true);
        conf.AddYamlFile($"appsettings.{host.HostingEnvironment.EnvironmentName}.yaml", true, true);
    })
    .ConfigureServices((host, services) => {
        var token = host.Configuration["Discord:Token"]!;

        services.AddDiscordBot(token);
    })
    .Build();

host.Run();
