using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// OpenAI provider implementation using the new LLM configuration system
/// </summary>
public class OpenAILLMProvider : LLMProviderBase
{
    private readonly JsonSerializerOptions _jsonOptions;
    
    public override string ProviderName => "OpenAI";
    
    public OpenAILLMProvider(
        ILogger<OpenAILLMProvider> logger,
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
            _logger.LogDebug("Sending {Count} messages to OpenAI API", messageList.Count);
            
            // Build the request
            var requestBody = new OpenAIRequest
            {
                Model = options.Model ?? _config.DefaultModel,
                Messages = messageList.Select(ConvertToOpenAIMessage).ToList(),
                MaxTokens = options.MaxTokens,
                Temperature = options.Temperature,
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
            
            _logger.LogDebug("Sending request to OpenAI API: {Model}", requestBody.Model);
            
            // Send request
            using var httpClient = CreateHttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            
            var response = await httpClient.PostAsync("chat/completions", httpContent);
            
            // Handle response
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {responseContent}");
            }
            
            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, _jsonOptions);
            
            if (openAIResponse?.Choices == null || !openAIResponse.Choices.Any())
            {
                throw new InvalidOperationException("No choices returned from OpenAI API");
            }
            
            var choice = openAIResponse.Choices.First();
            
            _logger.LogDebug("Received response from OpenAI API: {FinishReason}", choice.FinishReason);
            
            return new ChatCompletionResponse
            {
                Content = choice.Message?.Content ?? string.Empty,
                Role = choice.Message?.Role ?? "assistant",
                Model = openAIResponse.Model,
                PromptTokens = openAIResponse.Usage?.PromptTokens,
                CompletionTokens = openAIResponse.Usage?.CompletionTokens,
                TotalTokens = openAIResponse.Usage?.TotalTokens,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "provider", ProviderName },
                    { "finishReason", choice.FinishReason ?? "unknown" },
                    { "id", openAIResponse.Id ?? string.Empty }
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during OpenAI chat completion");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout during OpenAI chat completion");
            throw new TimeoutException("OpenAI API request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OpenAI chat completion");
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
                _logger.LogInformation("OpenAI provider is available");
                return true;
            }
            
            _logger.LogWarning("OpenAI provider check failed with status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI provider availability check failed");
            return false;
        }
    }
    
    private OpenAIMessage ConvertToOpenAIMessage(SessionMessage message)
    {
        return new OpenAIMessage
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
    private class OpenAIRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        [JsonPropertyName("messages")]
        public List<OpenAIMessage> Messages { get; set; } = new();
        
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
    
    private class OpenAIMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
    
    private class OpenAIResponse
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
        public OpenAIUsage? Usage { get; set; }
        
        public class Choice
        {
            [JsonPropertyName("index")]
            public int Index { get; set; }
            
            [JsonPropertyName("message")]
            public OpenAIMessage? Message { get; set; }
            
            [JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }
        
        public class OpenAIUsage
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