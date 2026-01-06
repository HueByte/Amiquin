using Amiquin.Core.Options;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Utilities;
using Amiquin.Core.Utilities.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Persona;

/// <summary>
/// Service implementation for managing server persona operations.
/// Handles persona creation, updates, caching, and AI-powered persona generation for Discord servers.
/// </summary>
public class PersonaService : IPersonaService
{
    private readonly ILogger<PersonaService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatCoreService _coreChatService;
    private readonly IMemoryCache _memoryCache;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private readonly IServerMetaService _serverMetaService;
    private readonly BotContextAccessor? _botContextAccessor;
    private readonly BotOptions _botOptions;

    /// <summary>
    /// Initializes a new instance of the PersonaService.
    /// </summary>
    /// <param name="logger">Logger instance for recording service operations.</param>
    /// <param name="messageCacheService">Service for managing message cache operations.</param>
    /// <param name="chatService">Core chat service for AI interactions.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed data.</param>
    /// <param name="chatSemaphoreManager">Manager for controlling concurrent chat operations.</param>
    /// <param name="serverMetaService">Service for managing server metadata.</param>
    /// <param name="botContextAccessor">Accessor for bot context information.</param>
    /// <param name="botOptions">Bot configuration options.</param>
    public PersonaService(
        ILogger<PersonaService> logger,
        IMessageCacheService messageCacheService,
        IChatCoreService chatService,
        IMemoryCache memoryCache,
        IChatSemaphoreManager chatSemaphoreManager,
        IServerMetaService serverMetaService,
        BotContextAccessor botContextAccessor,
        IOptions<BotOptions> botOptions)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _coreChatService = chatService;
        _memoryCache = memoryCache;
        _chatSemaphoreManager = chatSemaphoreManager;
        _serverMetaService = serverMetaService;
        _botContextAccessor = botContextAccessor;
        _botOptions = botOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<string> GetPersonaAsync(ulong serverId)
    {
        var channelSemaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(serverId);
        await channelSemaphore.WaitAsync();

        string personaMessage = string.Empty;
        try
        {
            personaMessage = await GetPersonaInternalAsync(serverId);
        }
        finally
        {
            channelSemaphore.Release();
        }

        return personaMessage;
    }

    /// <inheritdoc/>
    public async Task AddSummaryAsync(ulong serverId, string updateMessage)
    {
        var semaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(serverId);
        await semaphore.WaitAsync();

        try
        {
            var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedSystemMessageKey, serverId.ToString());
            string? personaMessage = _memoryCache.Get<string>(cacheKey);
            if (string.IsNullOrEmpty(personaMessage))
            {
                personaMessage = await GetPersonaInternalAsync(serverId);
            }

            personaMessage += $"This is your summary of recent conversations: {updateMessage}";
            // Use the server-specific cache key consistently
            _memoryCache.Set(cacheKey, personaMessage, TimeSpan.FromDays(Constants.SystemDefaults.SystemCacheDurationDays));

            _logger.LogDebug("Added summary to persona for server {ServerId}: {Summary}", serverId, updateMessage);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<string> GetPersonaInternalAsync(ulong serverId)
    {
        var computedSystemCacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedSystemMessageKey, serverId.ToString());
        if (_memoryCache.TryGetValue(computedSystemCacheKey, out string? personaMessage))
        {
            if (!string.IsNullOrEmpty(personaMessage))
            {
                return personaMessage;
            }
        }

        // Load base system message from System.md file
        string baseSystem = await LoadBaseSystemAsync();

        // Get server-specific persona from metadata
        string? serverPersona = (await _serverMetaService.GetServerMetaAsync(serverId))?.Persona;

        // Combine base persona with server-specific persona
        if (!string.IsNullOrEmpty(serverPersona))
        {
            // Server persona exists - append it to base persona
            personaMessage = $"{baseSystem}\n\n## Server-Specific Instructions\n{serverPersona}";
        }
        else
        {
            // No server persona - use base persona with default template
            personaMessage = $"{baseSystem}\n\n{Constants.SystemDefaults.DefaultSystemTemplate}";
        }

        personaMessage = ReplaceSystemKeywords(personaMessage);

        _memoryCache.Set(computedSystemCacheKey, personaMessage, TimeSpan.FromDays(Constants.SystemDefaults.SystemCacheDurationDays));
        _logger.LogInformation("Computed persona message: {personaMessage}", personaMessage);

        return personaMessage;
    }

    private async Task<string> LoadBaseSystemAsync()
    {
        const string baseSystemCacheKey = Constants.CacheKeys.BaseSystemMessageKey;

        // Check cache first
        if (_memoryCache.TryGetTypedValue(baseSystemCacheKey, out string? cachedBaseSystem))
        {
            if (!string.IsNullOrEmpty(cachedBaseSystem))
            {
                return cachedBaseSystem;
            }
        }

        try
        {
            // Try to load from Data/Messages/System.md file
            string systemFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Messages", "System.md");
            if (File.Exists(systemFilePath))
            {
                string baseSystem = await File.ReadAllTextAsync(systemFilePath);
                _memoryCache.SetAbsolute(baseSystemCacheKey, baseSystem, TimeSpan.FromDays(7)); // Cache for 7 days
                _logger.LogDebug("Loaded base system message from file: {Path}", systemFilePath);
                return baseSystem;
            }
            else
            {
                _logger.LogWarning("Base system file not found at {Path}, using default", systemFilePath);
                return "# System Message — Amiquin\n\nYou are Amiquin, a virtual clanmate AI assistant for Discord.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading base system file");
            return "# System Message — Amiquin\n\nYou are Amiquin, a virtual clanmate AI assistant for Discord.";
        }
    }

    /// <summary>
    /// Gets the bot name, preferring BotContextAccessor if available and initialized, falling back to BotOptions
    /// </summary>
    /// <returns>The bot name to use</returns>
    private string GetBotName()
    {
        try
        {
            // Try to get name from BotContextAccessor if it's initialized
            if (_botContextAccessor?.IsInitialized == true)
            {
                return _botContextAccessor.BotName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get bot name from BotContextAccessor, using fallback");
        }

        // Fall back to BotOptions name
        return _botOptions.Name;
    }

    /// <summary>
    /// Replaces system keywords with actual values in the system message.
    /// </summary>
    /// <param name="message">The system message template.</param>
    /// <returns>The system message with replaced keywords.</returns>
    private string ReplaceSystemKeywords(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        string name = _botOptions.Name;
        string version = _botOptions.Version;

        return message
            .Replace(Constants.SystemKeywordsCache.Mood, string.Empty) // Mood no longer used
            .Replace(Constants.SystemKeywordsCache.Name, name)
            .Replace(Constants.SystemKeywordsCache.Version, version);
    }
}
