using Amiquin.Core.Services.Persona;

namespace Amiquin.Core.Services.Chat;

public class PersonaChatService : IPersonaChatService
{
    private readonly IChatCoreService _chatCoreService;
    private readonly IPersonaService _personaService;

    public PersonaChatService(IChatCoreService chatCoreService, IPersonaService personaService)
    {
        _chatCoreService = chatCoreService;
        _personaService = personaService;
    }

    public async Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message)
    {
        var persona = await _personaService.GetPersonaAsync(instanceId);
        return await _chatCoreService.ChatAsync(instanceId, userId, botId, message, persona);
    }

    public async Task<string> ExchangeMessageAsync(string message)
    {
        var persona = await _personaService.GetPersonaAsync();
        return await _chatCoreService.ExchangeMessageAsync(message, persona);
    }
}