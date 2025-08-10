using Amiquin.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// Factory implementation for managing chat providers
/// </summary>
public class ChatProviderFactory : IChatProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatProviderFactory> _logger;
    private readonly ChatOptions _chatOptions;
    private readonly Dictionary<string, Type> _providerTypes;

    public ChatProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<ChatProviderFactory> logger,
        IOptions<ChatOptions> chatOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _chatOptions = chatOptions.Value;

        // Register provider types
        _providerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "OpenAI", typeof(OpenAILLMProvider) },
            { "Gemini", typeof(GeminiLLMProvider) },
            { "Grok", typeof(GrokLLMProvider) }
        };
    }

    public IChatProvider GetProvider(string providerName)
    {
        if (!_providerTypes.TryGetValue(providerName, out var providerType))
        {
            throw new NotSupportedException($"Provider '{providerName}' is not supported");
        }

        var provider = _serviceProvider.GetService(providerType) as IChatProvider;
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider '{providerName}' is not registered in DI container");
        }

        return provider;
    }

    public IChatProvider GetDefaultProvider()
    {
        // Get provider from configuration, default to OpenAI
        var providerName = _chatOptions.Model?.Split(':').FirstOrDefault() ?? "OpenAI";

        _logger.LogDebug("Getting default provider: {Provider}", providerName);
        return GetProvider(providerName);
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        return _providerTypes.Keys;
    }

    public bool IsProviderAvailable(string providerName)
    {
        try
        {
            var provider = GetProvider(providerName);
            return provider != null;
        }
        catch
        {
            return false;
        }
    }
}