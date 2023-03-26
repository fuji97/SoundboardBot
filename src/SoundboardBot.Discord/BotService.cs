using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundboardBot.Discord.Core;
namespace SoundboardBot.Discord; 

public class BotService : IHostedService {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BotService> _logger;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly CacheService _cache;
    private readonly string _token;
    private readonly Func<DiscordSocketClient, Task>? _configureDelegate;

    public BotService(IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        DiscordSocketClient client,
        InteractionService interactionService,
        CacheService cache,
        string token,
        Func<DiscordSocketClient, Task>? configureDelegate) {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _client = client;
        _interactionService = interactionService;
        _cache = cache;
        _token = token;
        _configureDelegate = configureDelegate;

        _logger = _loggerFactory.CreateLogger<BotService>();
    }

    private Func<LogMessage, Task> LogMessage(ILogger logger) {
        return (log) => {
            var logLevel = log.Severity switch {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => throw new ArgumentOutOfRangeException()
            };

            logger.Log(logLevel, log.Exception, "[{Source}]: {Message}", log.Source, log.Message);
            return Task.CompletedTask;
        };
    }

    private async Task RunBot(CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(_token)) {
            throw new InvalidOperationException("Token is null or empty.");
        }

        // Configure logging
        _client.Log += LogMessage(_loggerFactory.CreateLogger<DiscordSocketClient>());
        _interactionService.Log += LogMessage(_loggerFactory.CreateLogger<InteractionService>());
        
        // Clear cache
        _cache.ClearCache();

        // Add Interaction Service
        await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly()!, _serviceProvider.CreateScope().ServiceProvider);

        // Configure client
        if (_configureDelegate != null) 
            await _configureDelegate(_client);

        _client.InteractionCreated += async (x) => {
            var ctx = new SocketInteractionContext(_client, x);
            var scopedServiceProvider = _serviceProvider.CreateScope().ServiceProvider;
            await _interactionService.ExecuteCommandAsync(ctx, scopedServiceProvider );
        };

        // Start bot
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        _client.Ready += async () => {
            await _interactionService.RegisterCommandsGloballyAsync();
        };
    }
    
    public async Task StartAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Application started");

        await RunBot(cancellationToken);
    }
    public async Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Application stopped");
        
        await _client.StopAsync();
    }
}

public class BotServiceBuilder {
    public readonly string Token;
    public DiscordSocketConfig Config = new DiscordSocketConfig();
    public Func<DiscordSocketClient, Task>? ConfigureDelegate;
    
    public BotServiceBuilder(string token) {
        Token = token;
    }

    public BotServiceBuilder WithDiscordSocketConfig(DiscordSocketConfig config) {
        Config = config;
        return this;
    }
    
    public BotServiceBuilder ConfigureClient(Func<DiscordSocketClient, Task> configureDelegate) {
        ConfigureDelegate = configureDelegate;
        return this;
    }
    
    public BotServiceBuilder ConfigureClient(Action<DiscordSocketClient> configureDelegate) {
        ConfigureDelegate = client => {
            configureDelegate(client);
            return Task.CompletedTask;
        };
        return this;
    }
}

public static class BotServiceExtensions {
    public static IServiceCollection AddDiscordBot(this IServiceCollection host, string token, Action<BotServiceBuilder>? configure = null) {
        var builder = new BotServiceBuilder(token);

        configure?.Invoke(builder);

        Register(host, builder);

        return host;
    }
    
    public static async Task<IServiceCollection> AddDiscordBot(this IServiceCollection host, string token, Func<BotServiceBuilder, Task>? configure) {
        var builder = new BotServiceBuilder(token);

        if (configure != null) 
            await configure(builder);

        Register(host, builder);

        return host;
    }

    private static void Register(IServiceCollection services, BotServiceBuilder serviceBuilder) {
        services.AddSingleton<DiscordSocketConfig>(serviceBuilder.Config);
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<InteractionService>();

        services.AddHostedService<BotService>(s => CreateBotService(s, serviceBuilder.Token, serviceBuilder.ConfigureDelegate));
    }
    
    private static BotService CreateBotService(IServiceProvider services, string token, Func<DiscordSocketClient, Task>? configureDelegate = null) {
        var logger = services.GetRequiredService<ILoggerFactory>();
        var client = services.GetRequiredService<DiscordSocketClient>();
        var interactionService = services.GetRequiredService<InteractionService>();
        var cache = services.CreateScope().ServiceProvider.GetRequiredService<CacheService>();

        return new BotService(services, logger, client, interactionService, cache, token, configureDelegate);
    }
}

