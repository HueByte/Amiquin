using Amiquin.Core.Models;
using Amiquin.Core.Options.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// xAI Grok implementation of the chat provider
/// Uses OpenAI-compatible API endpoint from xAI
/// </summary>
public class GrokProvider : IChatProvider
{
    private readonly ILogger<GrokProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GrokOptions _grokOptions;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public string ProviderName => "Grok";
    
    public int MaxContextTokens => _grokOptions.Model switch
    {
        "grok-2-1212" => 131072, // 128k context
        "grok-2-vision-1212" => 32768, // 32k context for vision model
        "grok-beta" => 131072, // 128k context
        _ => 32768
    };
    
    public GrokProvider(
        ILogger<GrokProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<GrokOptions> grokOptions)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _grokOptions = grokOptions.Value;
        
        // Configure JSON serialization
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
    
    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<SessionMessage> messages, 
        ChatCompletionOptions options)
    {
        try
        {
            var messageList = messages.ToList();
            _logger.LogDebug("Sending {Count} messages to Grok API", messageList.Count);
            
            // Build the request
            var grokMessages = messageList.Select(ConvertToGrokMessage).ToList();
            
            var requestBody = new GrokChatRequest
            {
                Model = options.Model ?? _grokOptions.Model,
                Messages = grokMessages,
                Temperature = options.Temperature,
                MaxTokens = options.MaxTokens,
                TopP = options.TopP,
                FrequencyPenalty = options.FrequencyPenalty,
                PresencePenalty = options.PresencePenalty,
                Stop = options.StopSequences,
                N = options.N,
                Stream = false,
                User = options.User
            };
            
            // Serialize request
            var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Sending request to Grok API: {Model}", requestBody.Model);
            
            // Get HTTP client and send request
            using var httpClient = _httpClientFactory.CreateClient("Grok");
            var response = await httpClient.PostAsync("chat/completions", httpContent);
            
            // Handle response
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Grok API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Grok API error: {response.StatusCode} - {responseContent}");
            }
            
            var grokResponse = JsonSerializer.Deserialize<GrokChatResponse>(responseContent, _jsonOptions);
            
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
                    { "provider", "Grok" },
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
    
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_grokOptions.ApiKey))
            {
                _logger.LogWarning("Grok API key is not configured");
                return false;
            }
            
            // Test with a simple models endpoint call
            using var httpClient = _httpClientFactory.CreateClient("Grok");
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
    private class GrokChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        [JsonPropertyName("messages")]
        public List<GrokMessage> Messages { get; set; } = new();
        
        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }
        
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
        
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
    
    private class GrokChatResponse
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

/// <summary>
/// Configuration options for Grok provider
/// </summary>
public class GrokOptions
{
    public const string SectionName = "Chat:Grok";
    
    /// <summary>
    /// xAI API key for Grok
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Base URL for the Grok API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.x.ai/v1/";
    
    /// <summary>
    /// Grok model to use (grok-beta, grok-2-1212, grok-2-vision-1212)
    /// </summary>
    public string Model { get; set; } = "grok-beta";
    
    /// <summary>
    /// Whether Grok is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Maximum retries for API calls
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Timeout in seconds for API calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}