using Amiquin.Core.Models;
using Amiquin.Core.Options.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// xAI Grok provider implementation using the new LLM configuration system
/// </summary>
public class GrokLLMProvider : LLMProviderBase
{
    private readonly JsonSerializerOptions _jsonOptions;
    
    public override string ProviderName => "Grok";
    
    public GrokLLMProvider(
        ILogger logger,
        IHttpClientFactory httpClientFactory,
        LLMProviderOptions config,
        LLMOptions globalConfig)
        : base(logger, httpClientFactory, config, globalConfig)
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
    
    public override async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<SessionMessage> messages, 
        ChatCompletionOptions options)
    {
        if (!ValidateConfiguration())
        {
            throw new InvalidOperationException($"{ProviderName} is not properly configured");
        }
        
        try
        {
            var messageList = messages.ToList();
            _logger.LogDebug("Sending {Count} messages to Grok API", messageList.Count);
            
            // Build the request
            var requestBody = new GrokRequest
            {
                Model = options.Model ?? _config.DefaultModel,
                Messages = messageList.Select(ConvertToGrokMessage).ToList(),
                MaxTokens = options.MaxTokens,
                Temperature = options.Temperature,
                TopP = options.TopP,
                N = options.N,
                Stream = false,
                User = options.User
            };
            
            // Note: Grok 4 doesn't support frequency/presence penalties or stop sequences
            var modelName = requestBody.Model?.ToLowerInvariant();
            if (modelName != null && !modelName.StartsWith("grok-4"))
            {
                requestBody.FrequencyPenalty = options.FrequencyPenalty;
                requestBody.PresencePenalty = options.PresencePenalty;
                requestBody.Stop = options.StopSequences;
            }
            
            // Serialize request
            var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Sending request to Grok API: {Model}", requestBody.Model);
            
            // Send request
            using var httpClient = CreateHttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            
            var response = await httpClient.PostAsync("chat/completions", httpContent);
            
            // Handle response
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Grok API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Grok API error: {response.StatusCode} - {responseContent}");
            }
            
            var grokResponse = JsonSerializer.Deserialize<GrokResponse>(responseContent, _jsonOptions);
            
            if (grokResponse?.Choices == null || !grokResponse.Choices.Any())
            {
                throw new InvalidOperationException("No choices returned from Grok API");
            }
            
            var choice = grokResponse.Choices.First();
            
            _logger.LogDebug("Received response from Grok API: {FinishReason}", choice.FinishReason);
            
            return new ChatCompletionResponse
            {
                Content = choice.Message?.Content ?? string.Empty,
                Role = choice.Message?.Role ?? "assistant",
                Model = grokResponse.Model,
                PromptTokens = grokResponse.Usage?.PromptTokens,
                CompletionTokens = grokResponse.Usage?.CompletionTokens,
                TotalTokens = grokResponse.Usage?.TotalTokens,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "provider", ProviderName },
                    { "finishReason", choice.FinishReason ?? "unknown" },
                    { "id", grokResponse.Id ?? string.Empty }
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Grok chat completion");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout during Grok chat completion");
            throw new TimeoutException("Grok API request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Grok chat completion");
            throw;
        }
    }
    
    public override async Task<bool> IsAvailableAsync()
    {
        if (!ValidateConfiguration())
        {
            return false;
        }
        
        try
        {
            using var httpClient = CreateHttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            
            var response = await httpClient.GetAsync("models");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Grok provider is available");
                return true;
            }
            
            _logger.LogWarning("Grok provider check failed with status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Grok provider availability check failed");
            return false;
        }
    }
    
    private GrokMessage ConvertToGrokMessage(SessionMessage message)
    {
        return new GrokMessage
        {
            Role = message.Role.ToLowerInvariant() switch
            {
                "system" => "system",
                "user" => "user",
                "assistant" => "assistant",
                _ => "user"
            },
            Content = message.Content
        };
    }
    
    // Request/Response DTOs
    private class GrokRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        [JsonPropertyName("messages")]
        public List<GrokMessage> Messages { get; set; } = new();
        
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
        
        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }
        
        [JsonPropertyName("top_p")]
        public float? TopP { get; set; }
        
        [JsonPropertyName("frequency_penalty")]
        public float? FrequencyPenalty { get; set; }
        
        [JsonPropertyName("presence_penalty")]
        public float? PresencePenalty { get; set; }
        
        [JsonPropertyName("stop")]
        public List<string>? Stop { get; set; }
        
        [JsonPropertyName("n")]
        public int? N { get; set; }
        
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
        
        [JsonPropertyName("user")]
        public string? User { get; set; }
    }
    
    private class GrokMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
    
    private class GrokResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("object")]
        public string? Object { get; set; }
        
        [JsonPropertyName("created")]
        public long Created { get; set; }
        
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = new();
        
        [JsonPropertyName("usage")]
        public GrokUsage? Usage { get; set; }
        
        public class Choice
        {
            [JsonPropertyName("index")]
            public int Index { get; set; }
            
            [JsonPropertyName("message")]
            public GrokMessage? Message { get; set; }
            
            [JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }
        
        public class GrokUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }
            
            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }
            
            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }
    }
}