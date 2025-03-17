using Amiquin.Core.Services.Persona;

namespace Amiquin.Core.Services.Chat;

public class PersonaChatService : IPersonaChatService
{
    private readonly IChatCoreService _chatCoreService;
    private readonly IPersonaService _personaService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public PersonaChatService(IChatCoreService chatCoreService, IPersonaService personaService)
    {
        _chatCoreService = chatCoreService;
        _personaService = personaService;
    }

    public async Task<string> ChatAsync(ulong channelId, ulong userId, string message)
    {
        var persona = await _personaService.GetPersonaAsync(channelId);
        string result = string.Empty;
        await _semaphore.WaitAsync();
        try
        {
            result = await _chatCoreService.ChatAsync(channelId, userId, message, persona);
        }
        finally
        {
            _semaphore.Release();
        }

        return result;
    }

    public async Task<string> ExchangeMessageAsync(string message)
    {
        var persona = await _personaService.GetPersonaAsync();
        return await _chatCoreService.ExchangeMessageAsync(message, persona);
    }
}