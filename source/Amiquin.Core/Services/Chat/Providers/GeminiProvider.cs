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
/// Google Gemini implementation of the chat provider
/// </summary>
public class GeminiProvider : IChatProvider
{
    private readonly ILogger<GeminiProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiOptions _geminiOptions;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public string ProviderName => "Gemini";
    
    public int MaxContextTokens => _geminiOptions.Model switch
    {
        "gemini-1.5-pro-002" => 2097152, // 2M context
        "gemini-1.5-pro" => 2097152, // 2M context
        "gemini-1.5-flash-002" => 1048576, // 1M context
        "gemini-1.5-flash" => 1048576, // 1M context
        "gemini-1.5-flash-8b" => 1048576, // 1M context
        "gemini-pro" => 32768,
        _ => 8192
    };
    
    public GeminiProvider(
        ILogger<GeminiProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiOptions> geminiOptions)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _geminiOptions = geminiOptions.Value;
        
        // Configure JSON serialization
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
            _logger.LogDebug("Sending {Count} messages to Gemini API", messageList.Count);
            
            // Convert messages to Gemini format
            var geminiContents = ConvertToGeminiFormat(messageList);
            
            // Build request
            var requestBody = new GeminiRequest
            {
                Contents = geminiContents,
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = options.Temperature,
                    TopP = options.TopP,
                    MaxOutputTokens = options.MaxTokens,
                    StopSequences = options.StopSequences,
                    CandidateCount = options.N
                },
                SafetySettings = GetSafetySettings()
            };
            
            var model = options.Model ?? _geminiOptions.Model;
            var url = $"v1beta/models/{model}:generateContent?key={_geminiOptions.ApiKey}";
            
            // Serialize and send request
            var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Sending request to Gemini API: {Model}", model);
            
            using var httpClient = _httpClientFactory.CreateClient("Gemini");
            var response = await httpClient.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Gemini API error: {response.StatusCode} - {responseContent}");
            }
            
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, _jsonOptions);
            
            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
            {
                throw new InvalidOperationException("No candidates returned from Gemini API");
            }
            
            var candidate = geminiResponse.Candidates.First();
            var content = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            
            _logger.LogDebug("Received response from Gemini API: {FinishReason}", candidate.FinishReason);
            
            return new ChatCompletionResponse
            {
                Content = content,
                Role = "assistant",
                Model = model,
                PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount,
                CompletionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount,
                TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "provider", "Gemini" },
                    { "finishReason", candidate.FinishReason ?? "unknown" },
                    { "safetyRatings", candidate.SafetyRatings ?? new List<object>() }
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Gemini chat completion");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout during Gemini chat completion");
            throw new TimeoutException("Gemini API request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Gemini chat completion");
            throw;
        }
    }
    
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_geminiOptions.ApiKey))
                return false;
            
            var testUrl = $"v1beta/models?key={_geminiOptions.ApiKey}";
            using var httpClient = _httpClientFactory.CreateClient("Gemini");
            var response = await httpClient.GetAsync(testUrl);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini provider availability check failed");
            return false;
        }
    }
    
    private List<GeminiContent> ConvertToGeminiFormat(IEnumerable<SessionMessage> messages)
    {
        var contents = new List<GeminiContent>();
        var systemMessage = string.Empty;
        
        foreach (var message in messages)
        {
            // Gemini doesn't have a system role, so we prepend system messages to the first user message
            if (message.Role.ToLowerInvariant() == "system")
            {
                systemMessage += message.Content + "\n\n";
                continue;
            }
            
            var role = message.Role.ToLowerInvariant() == "assistant" ? "model" : "user";
            var content = message.Content;
            
            // If this is the first user message and we have a system message, prepend it
            if (role == "user" && !string.IsNullOrEmpty(systemMessage))
            {
                content = systemMessage + content;
                systemMessage = string.Empty;
            }
            
            contents.Add(new GeminiContent
            {
                Role = role,
                Parts = new List<GeminiPart>
                {
                    new GeminiPart { Text = content }
                }
            });
        }
        
        return contents;
    }
    
    private List<GeminiSafetySetting> GetSafetySettings()
    {
        var threshold = _geminiOptions.SafetyThreshold;
        
        return new List<GeminiSafetySetting>
        {
            new() { Category = "HARM_CATEGORY_HARASSMENT", Threshold = threshold },
            new() { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = threshold },
            new() { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = threshold },
            new() { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = threshold }
        };
    }
    
    // Request DTOs
    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();
        
        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
        
        [JsonPropertyName("safetySettings")]
        public List<GeminiSafetySetting>? SafetySettings { get; set; }
    }
    
    private class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }
    
    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
    
    private class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }
        
        [JsonPropertyName("topP")]
        public float? TopP { get; set; }
        
        [JsonPropertyName("topK")]
        public int? TopK { get; set; }
        
        [JsonPropertyName("maxOutputTokens")]
        public int? MaxOutputTokens { get; set; }
        
        [JsonPropertyName("stopSequences")]
        public List<string>? StopSequences { get; set; }
        
        [JsonPropertyName("candidateCount")]
        public int? CandidateCount { get; set; }
    }
    
    private class GeminiSafetySetting
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;
        
        [JsonPropertyName("threshold")]
        public string Threshold { get; set; } = string.Empty;
    }
    
    // Response DTOs
    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; } = new();
        
        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
        
        public class Candidate
        {
            [JsonPropertyName("content")]
            public GeminiContent? Content { get; set; }
            
            [JsonPropertyName("finishReason")]
            public string? FinishReason { get; set; }
            
            [JsonPropertyName("safetyRatings")]
            public List<object>? SafetyRatings { get; set; }
        }
        
        public class GeminiUsageMetadata
        {
            [JsonPropertyName("promptTokenCount")]
            public int PromptTokenCount { get; set; }
            
            [JsonPropertyName("candidatesTokenCount")]
            public int CandidatesTokenCount { get; set; }
            
            [JsonPropertyName("totalTokenCount")]
            public int TotalTokenCount { get; set; }
        }
    }
}

/// <summary>
/// Configuration options for Gemini provider
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Chat:Gemini";
    
    /// <summary>
    /// Google AI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Base URL for the Gemini API
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
    
    /// <summary>
    /// Gemini model to use (gemini-1.5-pro, gemini-1.5-flash, etc.)
    /// </summary>
    public string Model { get; set; } = "gemini-1.5-flash";
    
    /// <summary>
    /// Whether Gemini is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Safety threshold for content filtering
    /// Options: BLOCK_NONE, BLOCK_ONLY_HIGH, BLOCK_MEDIUM_AND_ABOVE, BLOCK_LOW_AND_ABOVE
    /// </summary>
    public string SafetyThreshold { get; set; } = "BLOCK_NONE";
    
    /// <summary>
    /// Timeout in seconds for API calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}