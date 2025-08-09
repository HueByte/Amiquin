using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// OpenAI implementation of the chat provider
/// </summary>
public class OpenAIProvider : IChatProvider
{
    private readonly ILogger<OpenAIProvider> _logger;
    private readonly ChatClient _chatClient;
    private readonly ChatOptions _chatOptions;
    
    public string ProviderName => "OpenAI";
    
    public int MaxContextTokens => _chatOptions.Model switch
    {
        "gpt-4o" => 128000,
        "gpt-4o-mini" => 128000,
        "gpt-4-turbo" => 128000,
        "gpt-4" => 8192,
        "gpt-3.5-turbo" => 16385,
        _ => 4096
    };
    
    public OpenAIProvider(
        ILogger<OpenAIProvider> logger,
        ChatClient chatClient,
        IOptions<ChatOptions> chatOptions)
    {
        _logger = logger;
        _chatClient = chatClient;
        _chatOptions = chatOptions.Value;
    }
    
    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<SessionMessage> messages, 
        ChatCompletionOptions options)
    {
        try
        {
            // Convert our messages to OpenAI format
            var openAIMessages = messages.Select(ConvertToOpenAIMessage).ToList();
            
            // Map our options to OpenAI options
            var openAIOptions = new OpenAI.Chat.ChatCompletionOptions
            {
                MaxOutputTokenCount = options.MaxTokens,
                Temperature = options.Temperature,
                TopP = options.TopP,
                PresencePenalty = options.PresencePenalty,
                FrequencyPenalty = options.FrequencyPenalty
            };
            
            // Set stop sequences if provided
            if (options.StopSequences != null)
            {
                foreach (var stop in options.StopSequences)
                {
                    openAIOptions.StopSequences.Add(stop);
                }
            }
            
            // Make the API call
            var response = await _chatClient.CompleteChatAsync(openAIMessages, openAIOptions);
            
            // Convert response to our format
            return new ChatCompletionResponse
            {
                Content = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty,
                Role = "assistant",
                Model = response.Value.Model,
                PromptTokens = response.Value.Usage?.InputTokenCount,
                CompletionTokens = response.Value.Usage?.OutputTokenCount,
                TotalTokens = response.Value.Usage?.TotalTokenCount,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OpenAI chat completion");
            throw;
        }
    }
    
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Test with a simple message
            var testMessage = ChatMessage.CreateSystemMessage("Test");
            var response = await _chatClient.CompleteChatAsync(
                [testMessage], 
                new OpenAI.Chat.ChatCompletionOptions { MaxOutputTokenCount = 1 });
            
            return response != null && response.Value != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI provider availability check failed");
            return false;
        }
    }
    
    private ChatMessage ConvertToOpenAIMessage(SessionMessage message)
    {
        return message.Role.ToLowerInvariant() switch
        {
            "system" => ChatMessage.CreateSystemMessage(message.Content),
            "user" => ChatMessage.CreateUserMessage(message.Content),
            "assistant" => ChatMessage.CreateAssistantMessage(message.Content),
            _ => ChatMessage.CreateUserMessage(message.Content)
        };
    }
}