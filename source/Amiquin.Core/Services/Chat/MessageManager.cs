using Amiquin.Core.Services.MessageCache;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Basic message manager implementation that integrates with the existing message cache service.
/// </summary>
public class MessageManager : IMessageManager
{
    private readonly IMessageCacheService _messageCacheService;

    public MessageManager(IMessageCacheService messageCacheService)
    {
        _messageCacheService = messageCacheService;
    }

    /// <summary>
    /// Gets the core persona message for the AI system.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the persona message as a string.</returns>
    public async Task<string> GetPersonaCoreMessageAsync()
    {
        return await _messageCacheService.GetPersonaCoreMessageAsync();
    }
}