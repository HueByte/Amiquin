using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChatSessionModel = Amiquin.Core.Models.ChatSession;

namespace Amiquin.Core.Services.ChatSession;

/// <summary>
/// Service implementation for managing chat sessions
/// </summary>
public class ChatSessionService : IChatSessionService
{
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatSessionService> _logger;

    public ChatSessionService(
        IChatSessionRepository chatSessionRepository,
        IConfiguration configuration,
        ILogger<ChatSessionService> logger)
    {
        _chatSessionRepository = chatSessionRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatSessionModel?> GetActiveServerSessionAsync(ulong serverId)
    {
        return await _chatSessionRepository.GetActiveSessionAsync(SessionScope.Server, serverId: serverId);
    }

    /// <inheritdoc/>
    public async Task<ChatSessionModel> GetOrCreateServerSessionAsync(ulong serverId, string defaultModel = "gpt-4o-mini", string defaultProvider = "OpenAI")
    {
        return await _chatSessionRepository.GetOrCreateActiveSessionAsync(SessionScope.Server, serverId: serverId, model: defaultModel, provider: defaultProvider);
    }

    /// <inheritdoc/>
    public async Task<int> UpdateServerSessionModelAsync(ulong serverId, string model, string provider)
    {
        _logger.LogInformation("Updating server {ServerId} sessions to use model {Model} from provider {Provider}", serverId, model, provider);

        var updatedCount = await _chatSessionRepository.UpdateSessionModelByScopeAsync(SessionScope.Server, model, provider, serverId: serverId);

        _logger.LogInformation("Updated {UpdatedCount} sessions for server {ServerId}", updatedCount, serverId);
        return updatedCount;
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, List<string>>> GetAvailableModelsAsync()
    {
        var result = new Dictionary<string, List<string>>();

        var llmConfig = _configuration.GetSection("LLM");
        var providersConfig = llmConfig.GetSection("Providers");

        foreach (var providerSection in providersConfig.GetChildren())
        {
            var providerName = providerSection.Key;
            var isEnabled = providerSection.GetValue<bool>("Enabled", false);

            if (!isEnabled) continue;

            var modelsSection = providerSection.GetSection("Models");
            var models = new List<string>();

            foreach (var modelSection in modelsSection.GetChildren())
            {
                models.Add(modelSection.Key);
            }

            if (models.Count > 0)
            {
                result[providerName] = models;
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateModelProviderAsync(string model, string provider)
    {
        var availableModels = await GetAvailableModelsAsync();

        return availableModels.ContainsKey(provider) && availableModels[provider].Contains(model);
    }
}
