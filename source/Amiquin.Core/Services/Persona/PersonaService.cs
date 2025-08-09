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
    private readonly BotContextAccessor _botContextAccessor;
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
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverId.ToString());
        string? personaMessage = _memoryCache.Get<string>(cacheKey);
        if (string.IsNullOrEmpty(personaMessage))
        {
            personaMessage = await GetPersonaInternalAsync(serverId);
        }

        personaMessage += $"This is your summary of recent conversations: {updateMessage}";
        _memoryCache.Set(Constants.CacheKeys.ComputedPersonaMessageKey, personaMessage, TimeSpan.FromDays(1));
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

        personaMessage = (await _serverMetaService.GetServerMetaAsync(serverId))?.Persona;
        if (string.IsNullOrEmpty(personaMessage))
        {
            personaMessage = $"You are {Constants.PersonaKeywordsCache.Name}. The AI assistant for discord.\n{Constants.PersonaKeywordsCache.Mood}";
        }

        var computedMood = await GetComputedMoodAsync();
        personaMessage = personaMessage.Replace(Constants.PersonaKeywordsCache.Mood, computedMood);

        _memoryCache.Set(computedPersonaCacheKey, personaMessage, TimeSpan.FromDays(1));
        _logger.LogInformation("Computed persona message: {personaMessage}", personaMessage);

        return personaMessage;
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
                return "I couldn't find any news at the moment.";
            }

            sb.AppendLine($"{_botContextAccessor.BotName} just received some juicy news and is bursting with snarky curiosity. Express {_botContextAccessor.BotName}’s thoughts in the third person—never say “I,” only “Amiquin.” Keep the playful, sarcastic edge, but don’t let it overshadow the message. Reflect how {_botContextAccessor.BotName} feels about the latest updates and respond accordingly.");
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

            var personaOpinion = await _coreChatService.ExchangeMessageAsync(sb.ToString(), tokenLimit: 500);
            _logger.LogInformation("Persona Opinion: {personaOpinion}", personaOpinion);

            return personaOpinion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while computing mood in GetInfoFromNewsAsync.");
            return "I'm having trouble processing the news right now.";
        }
    }

    private string ReplacePersonaKeywords(string message, string mood)
    {
        string name = _botOptions.Name;
        string version = _botOptions.Version;

        return message
            .Replace(Constants.PersonaKeywordsCache.Mood, mood)
            .Replace(Constants.PersonaKeywordsCache.Name, name)
            .Replace(Constants.PersonaKeywordsCache.Version, version);
    }
}