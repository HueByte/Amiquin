using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public interface IChatCoreService
{
    Task<ChatCompletion> ChatAsync(ulong instanceId, List<ChatMessage> messageHistory, ChatMessage? personaMessage = null);
    Task<string> ExchangeMessageAsync(string message, ChatMessage? developerPersonaChatMessage = null, int tokenLimit = 1200);
}