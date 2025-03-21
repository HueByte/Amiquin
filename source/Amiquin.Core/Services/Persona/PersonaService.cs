using System.Text;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Persona;

public class PersonaService : IPersonaService
{
    private readonly ILogger<PersonaService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatCoreService _coreChatService;
    private readonly INewsApiClient _newsApiClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;

    public PersonaService(ILogger<PersonaService> logger, IMessageCacheService messageCacheService, IChatCoreService chatService, INewsApiClient newsApiClient, IMemoryCache memoryCache, IChatSemaphoreManager chatSemaphoreManager)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _coreChatService = chatService;
        _newsApiClient = newsApiClient;
        _memoryCache = memoryCache;
        _chatSemaphoreManager = chatSemaphoreManager;
    }

    public async Task<string> GetPersonaAsync(ulong instanceId = 0)
    {
        if (instanceId == 0)
        {
            return await GetPersonaInternalAsync();
        }

        var channelSemaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(instanceId);
        await channelSemaphore.WaitAsync();

        string personaMessage = string.Empty;
        try
        {
            personaMessage = await GetPersonaInternalAsync();
        }
        finally
        {
            channelSemaphore.Release();
        }

        return personaMessage;
    }

    public async Task AddSummaryAsync(string updateMessage)
    {
        string? personaMessage = _memoryCache.Get<string>(Constants.CacheKeys.ComputedPersonaMessageKey);
        if (string.IsNullOrEmpty(personaMessage))
        {
            personaMessage = await GetPersonaInternalAsync();
        }

        personaMessage += $"This is your summary of recent conversations: {updateMessage}";
        _memoryCache.Set(Constants.CacheKeys.ComputedPersonaMessageKey, personaMessage, TimeSpan.FromDays(1));
    }

    private async Task<string> GetPersonaInternalAsync()
    {

        if (_memoryCache.TryGetValue(Constants.CacheKeys.ComputedPersonaMessageKey, out string? personaMessage))
        {
            if (!string.IsNullOrEmpty(personaMessage))
            {
                return personaMessage;
            }
        }

        personaMessage = await _messageCacheService.GetPersonaCoreMessageAsync();

        if (string.IsNullOrEmpty(personaMessage))
            personaMessage = $"I'm {Constants.BotMetadata.BotName}. Your AI assistant. {Constants.BotMetadata.Mood}";

        var computedMood = await GetComputedMoodAsync();
        personaMessage = personaMessage.Replace(Constants.BotMetadata.Mood, computedMood);

        _memoryCache.Set(Constants.CacheKeys.ComputedPersonaMessageKey, personaMessage, TimeSpan.FromDays(1));
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

            sb.AppendLine("I've got some news for you Amiquin, tell me how you feel about it, but instead of using \"I\", use \"Amiquin\". So basically write it in third person.");
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
}