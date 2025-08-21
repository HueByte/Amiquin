using Amiquin.Core.Options;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

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
    private readonly INewsApiClient _newsApiClient;
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
    /// <param name="newsApiClient">Client for accessing news API services.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed data.</param>
    /// <param name="chatSemaphoreManager">Manager for controlling concurrent chat operations.</param>
    /// <param name="serverMetaService">Service for managing server metadata.</param>
    /// <param name="botContextAccessor">Accessor for bot context information.</param>
    /// <param name="botOptions">Bot configuration options.</param>
    public PersonaService(ILogger<PersonaService> logger, IMessageCacheService messageCacheService, IChatCoreService chatService, INewsApiClient newsApiClient, IMemoryCache memoryCache, IChatSemaphoreManager chatSemaphoreManager, IServerMetaService serverMetaService, BotContextAccessor botContextAccessor, IOptions<BotOptions> botOptions)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _coreChatService = chatService;
        _newsApiClient = newsApiClient;
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
            var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverId.ToString());
            string? personaMessage = _memoryCache.Get<string>(cacheKey);
            if (string.IsNullOrEmpty(personaMessage))
            {
                personaMessage = await GetPersonaInternalAsync(serverId);
            }

            personaMessage += $"This is your summary of recent conversations: {updateMessage}";
            // Use the server-specific cache key consistently
            _memoryCache.Set(cacheKey, personaMessage, TimeSpan.FromDays(Constants.PersonaDefaults.PersonaCacheDurationDays));

            _logger.LogDebug("Added summary to persona for server {ServerId}: {Summary}", serverId, updateMessage);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<string> GetPersonaInternalAsync(ulong serverId)
    {
        var computedPersonaCacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverId.ToString());
        if (_memoryCache.TryGetValue(computedPersonaCacheKey, out string? personaMessage))
        {
            if (!string.IsNullOrEmpty(personaMessage))
            {
                return personaMessage;
            }
        }

        // Load base persona from Persona.md file
        string basePersona = await LoadBasePersonaAsync();

        // Get server-specific persona from metadata
        string? serverPersona = (await _serverMetaService.GetServerMetaAsync(serverId))?.Persona;

        // Combine base persona with server-specific persona
        if (!string.IsNullOrEmpty(serverPersona))
        {
            // Server persona exists - append it to base persona
            personaMessage = $"{basePersona}\n\n## Server-Specific Instructions\n{serverPersona}";
        }
        else
        {
            // No server persona - use base persona with default template
            personaMessage = $"{basePersona}\n\n{Constants.PersonaDefaults.DefaultPersonaTemplate}";
        }

        var computedMood = await GetComputedMoodAsync();
        personaMessage = ReplacePersonaKeywords(personaMessage, computedMood);

        _memoryCache.Set(computedPersonaCacheKey, personaMessage, TimeSpan.FromDays(Constants.PersonaDefaults.PersonaCacheDurationDays));
        _logger.LogInformation("Computed persona message: {personaMessage}", personaMessage);

        return personaMessage;
    }

    private async Task<string> LoadBasePersonaAsync()
    {
        const string basePersonaCacheKey = "BasePersona";

        // Check cache first
        if (_memoryCache.TryGetValue(basePersonaCacheKey, out string? cachedBasePersona))
        {
            if (!string.IsNullOrEmpty(cachedBasePersona))
            {
                return cachedBasePersona;
            }
        }

        try
        {
            // Try to load from Data/Messages/Persona.md file
            string personaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Messages", "Persona.md");
            if (File.Exists(personaFilePath))
            {
                string basePersona = await File.ReadAllTextAsync(personaFilePath);
                _memoryCache.Set(basePersonaCacheKey, basePersona, TimeSpan.FromDays(7)); // Cache for 7 days
                _logger.LogDebug("Loaded base persona from file: {Path}", personaFilePath);
                return basePersona;
            }
            else
            {
                _logger.LogWarning("Base persona file not found at {Path}, using default", personaFilePath);
                return "# System Message — Amiquin\n\nYou are Amiquin, a virtual clanmate AI assistant for Discord.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading base persona file");
            return "# System Message — Amiquin\n\nYou are Amiquin, a virtual clanmate AI assistant for Discord.";
        }
    }

    private async Task<string> GetComputedMoodAsync()
    {
        StringBuilder sb = new();
        try
        {
            sb.AppendLine(await GetInfoFromNewsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while computing mood");
        }

        return sb.ToString();
    }

    private async Task<string> GetInfoFromNewsAsync()
    {
        StringBuilder sb = new();
        try
        {
            var news = await _newsApiClient.GetNewsAsync();
            if (news is null || news.Data is null || news.Data.NewsList is null || news.Data.NewsList.Count == 0)
            {
                _logger.LogWarning("No news data received from API.");
                return Constants.PersonaDefaults.NewsMoodNotAvailableMessage;
            }

            var botName = GetBotName();
            sb.AppendLine($"{botName} just received some juicy news and is bursting with snarky curiosity. Express {botName}'s thoughts in the third person—never say \"I,\" only \"Amiquin.\" Keep the playful, sarcastic edge, but don't let it overshadow the message. Reflect how {botName} feels about the latest updates and respond accordingly.");
            foreach (var newsObj in news.Data.NewsList)
            {
                if (newsObj.NewsObj is null || string.IsNullOrEmpty(newsObj.NewsObj.Content))
                {
                    _logger.LogWarning("Skipping news item with missing content.");
                    continue;
                }

                _logger.LogInformation("News: {newsTitle}", newsObj.NewsObj.Title);
                sb.AppendLine(newsObj.NewsObj.Content);
            }

            var response = await _coreChatService.CoreRequestAsync(
                sb.ToString(),
                tokenLimit: Constants.PersonaDefaults.NewsPersonaTokenLimit);
            var personaOpinion = response.Content;
            _logger.LogInformation("Persona Opinion: {personaOpinion}", personaOpinion);

            return personaOpinion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while computing mood in GetInfoFromNewsAsync.");
            return Constants.PersonaDefaults.NewsProcessingErrorMessage;
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
    /// Replaces persona keywords with actual values in the persona message.
    /// </summary>
    /// <param name="message">The persona message template.</param>
    /// <param name="mood">The computed mood to replace.</param>
    /// <returns>The persona message with replaced keywords.</returns>
    private string ReplacePersonaKeywords(string message, string mood)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        string name = _botOptions.Name;
        string version = _botOptions.Version;

        return message
            .Replace(Constants.PersonaKeywordsCache.Mood, mood)
            .Replace(Constants.PersonaKeywordsCache.Name, name)
            .Replace(Constants.PersonaKeywordsCache.Version, version);
    }
}