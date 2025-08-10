# Chat Service Architecture

## Overview

The Amiquin chat system is designed with a layered architecture that separates concerns between provider management, persona handling, and conversation optimization. This document outlines the structure and responsibilities of each component.

## Architecture Layers

```
┌─────────────────────────────────────────┐
│         PersonaChatService              │ ← Discord integration layer
├─────────────────────────────────────────┤
│         CoreChatService                 │ ← Provider orchestration & fallback
├─────────────────────────────────────────┤
│       ChatProviderFactory               │ ← Provider management
├─────────────────────────────────────────┤
│    LLM Provider Implementations         │ ← Provider-specific adapters
│  (OpenAILLMProvider, GrokLLMProvider)   │
└─────────────────────────────────────────┘
```

## Component Responsibilities

### 1. CoreChatService (IChatCoreService)

**Purpose**: Central orchestrator for LLM interactions, handling provider selection and routing.

**Key Responsibilities**:
- Provider selection based on configuration or explicit override
- Model selection within providers
- System message construction from base persona
- Direct LLM communication through provider implementations
- Token usage tracking and reporting

**Core Methods**:

#### CoreRequestAsync
```csharp
Task<ChatCompletionResponse> CoreRequestAsync(
    string prompt,
    string? customPersona = null,
    int tokenLimit = 1200,
    string? provider = null)
```
- **Purpose**: Stateless LLM request without conversation history
- **Use Case**: One-off requests, summarization, context generation
- **Message Structure**: 
  - System message: Base persona + optional custom persona
  - User message: The prompt
- **No session management or history**

#### ChatAsync
```csharp
Task<ChatCompletionResponse> ChatAsync(
    ulong instanceId,
    List<SessionMessage> messages,
    string? customPersona = null,
    string? sessionContext = null,
    string? provider = null)
```
- **Purpose**: Stateful conversation with history
- **Use Case**: Normal chat interactions
- **Message Structure**:
  - System message: Base persona + optional custom persona + optional session context
  - Conversation history: All provided messages
- **Semaphore management for concurrent requests per instance**

**System Message Construction**:
1. Always starts with base persona from Persona.md
2. Optionally appends custom persona (server-specific)
3. Optionally appends session context (conversation summary)

Example:
```
[Base Persona from Persona.md]
[Custom Persona if provided]

Previous conversation context:
[Session Context if provided]
```

### 2. PersonaChatService (IPersonaChatService)

**Purpose**: High-level chat service managing personas, sessions, and conversation optimization.

**Key Responsibilities**:
- Extract persona from server metadata
- Manage chat sessions (create, retrieve, update)
- Fetch message history from cache or database
- Track token usage and trigger optimization
- Store conversation exchanges
- Perform inline history optimization when needed

**Main Flow**:
```csharp
public async Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message)
{
    // 1. Get message history from cache/database
    var messages = await GetMessageHistoryAsync(instanceId);
    
    // 2. Get server-specific persona and session context
    var (serverPersona, sessionContext) = await GetServerContextAsync(instanceId);
    
    // 3. Add the new user message to history
    var userMessage = new SessionMessage { Role = "user", Content = message };
    messages.Add(userMessage);

    // 4. Select provider based on server configuration
    var provider = await GetServerProviderAsync(instanceId);
    
    // 5. Send to LLM with fallback support
    var response = await _coreChatService.ChatAsync(
        instanceId, messages, serverPersona, sessionContext, provider);

    // 6. Store the exchange in cache and database
    await StoreMessageExchangeAsync(instanceId, userId, botId, message, response.Content);

    // 7. Check if optimization is needed
    if (response.TotalTokens > _botOptions.MaxTokens * 0.8)
    {
        await OptimizeHistoryAsync(instanceId, messages, sessionContext);
    }

    return response.Content;
}
```

**Session Management**:
- Sessions are per-server (instanceId)
- Messages are stored with `IncludeInContext` flag
- Session contains optional `Context` field for summaries
- Token count tracked per session

### 3. History Optimization (Built into PersonaChatService)

**Purpose**: Manage conversation history to prevent token limit issues while preserving context.

**Optimization Strategy**:

#### Token Threshold Management
- **Trigger**: When total tokens > 80% of configured maximum (e.g., > 3200 tokens for 4000 max)
- **Target**: Keep last 10 message exchanges
- **Method**: Summarize old messages and store as session context

#### Optimization Process (Integrated)

1. **Identify Messages to Optimize**
   ```csharp
   var messagesToKeep = 10; // Keep last 10 exchanges
   var messagesToSummarize = messages.Take(Math.Max(0, messages.Count - messagesToKeep)).ToList();
   ```

2. **Generate Summary**
   ```csharp
   // Use CoreChatService for summarization
   var summaryPrompt = "Summarize this conversation history concisely, preserving key context (max 400 tokens):\n\n" + conversationText;
   var summaryResponse = await _coreChatService.CoreRequestAsync(summaryPrompt, tokenLimit: 400);
   ```

3. **Update Session Context**
   ```csharp
   // Combine with existing context or replace
   var newContext = string.IsNullOrWhiteSpace(existingContext)
       ? summaryResponse.Content
       : $"{existingContext}\n\n{summaryResponse.Content}";
   ```

4. **Self-Summarization**
   ```csharp
   // When context gets too large (>2000 characters)
   if (newContext.Length > 2000)
   {
       var consolidatePrompt = $"Consolidate these conversation summaries into one concise summary (max 400 tokens):\n\n{newContext}";
       var consolidatedResponse = await _coreChatService.CoreRequestAsync(consolidatePrompt, tokenLimit: 400);
       newContext = consolidatedResponse.Content;
   }
   ```

5. **Clean Up**
   ```csharp
   // Update session context in database
   await chatSessionRepository.UpdateSessionContextAsync(session.Id, newContext, newContext.Length / 4);
   
   // Clear old messages from cache
   _messageCache.ClearOldMessages(instanceId, messagesToKeep);
   ```

### 4. ChatProviderFactory (IChatProviderFactory)

**Purpose**: Manage and instantiate LLM provider implementations.

**Key Responsibilities**:
- Register available LLM providers (OpenAI, Grok, Gemini)
- Instantiate providers from dependency injection container
- Provide fallback logic for provider selection
- Track provider availability

**Core Methods**:
```csharp
public interface IChatProviderFactory
{
    IChatProvider GetProvider(string providerName);
    IChatProvider GetDefaultProvider();
    IEnumerable<string> GetAvailableProviders();
    bool IsProviderAvailable(string providerName);
}
```

**Provider Registration**:
```csharp
_providerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
{
    { "OpenAI", typeof(OpenAILLMProvider) },
    { "Gemini", typeof(GeminiLLMProvider) },
    { "Grok", typeof(GrokLLMProvider) }
};
```

## Data Models

### SessionMessage
```csharp
public class SessionMessage
{
    public string Id { get; set; }
    public string Role { get; set; } // "user", "assistant", "system"
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IncludeInContext { get; set; } // Include in next request
}
```

### ChatSession
```csharp
public class ChatSession
{
    public string Id { get; set; }
    public ulong ServerId { get; set; }
    public string? Context { get; set; } // Conversation summary
    public int ContextTokenCount { get; set; }
    public List<SessionMessage> Messages { get; set; }
    public DateTime LastActivity { get; set; }
}
```

### ChatCompletionResponse
```csharp
public class ChatCompletionResponse
{
    public string Content { get; set; }
    public string Provider { get; set; }
    public string Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
}
```

## Token Management

### Token Limits
- **Maximum Tokens**: 8192 (configurable)
- **Optimization Trigger**: 80% of maximum
- **Target After Optimization**: 25% of maximum
- **Context Size Limit**: 25% of maximum
- **Summary Token Limit**: 400 tokens

### Token Counting
- Use tokenizer library for accurate counts
- Fallback: Estimate 4 characters per token
- Track tokens per message, session, and context

## Provider Management

### Provider Selection Priority
1. Explicit provider parameter
2. Server-specific configuration
3. Global default provider
4. Fallback chain if primary fails

### Provider Implementations
Each provider must implement:
```csharp
public interface IChatProvider
{
    Task<ChatCompletionResponse> ChatAsync(
        List<SessionMessage> messages, 
        ChatCompletionOptions options);
    Task<bool> IsAvailableAsync();
}
```

**ChatCompletionOptions**:
```csharp
public class ChatCompletionOptions
{
    public int? MaxTokens { get; set; }
    public float? Temperature { get; set; }
    // Additional provider-specific options
}
```

## Configuration

### Example Configuration
```json
{
  "Bot": {
    "MaxTokens": 4000
  },
  "LLM": {
    "GlobalSystemMessage": "You are Amiquin, a helpful AI assistant.",
    "GlobalTemperature": 0.7,
    "DefaultProvider": "OpenAI",
    "EnableFallback": true,
    "FallbackOrder": ["OpenAI", "Gemini", "Grok"],
    "Providers": {
      "OpenAI": {
        "Enabled": true,
        "ApiKey": "sk-...",
        "Model": "gpt-4o-mini",
        "BaseUrl": "https://api.openai.com/v1"
      },
      "Grok": {
        "Enabled": true,
        "ApiKey": "xai-...",
        "Model": "grok-beta",
        "BaseUrl": "https://api.x.ai/v1"
      },
      "Gemini": {
        "Enabled": false,
        "ApiKey": "...",
        "Model": "gemini-1.5-flash",
        "BaseUrl": "https://generativelanguage.googleapis.com/v1beta"
      }
    }
  }
}
```

## Usage Examples

### Simple Exchange (No History)
```csharp
// Direct call to CoreChatService
var response = await _coreChat.CoreRequestAsync(
    "What is the weather like?",
    customPersona: "You are a helpful weather assistant");
```

### Full Conversation (With History)
```csharp
// Through PersonaChatService
var response = await _personaChat.ChatAsync(
    instanceId: 12345,
    userId: 67890,
    botId: 11111,
    message: "Tell me about the previous topic");
// Automatically includes history and manages optimization
```

### Manual Optimization
```csharp
// Trigger optimization manually
if (tokenCount > threshold)
{
    var result = await _optimizer.OptimizeMessageHistory(
        currentTokenCount: tokenCount,
        messages: conversationHistory,
        personaMessage: systemMessage);
}
```

## Error Handling

### Provider Failures
- Automatic fallback to next available provider
- Log failures for monitoring
- Return user-friendly error messages

### Token Limit Exceeded
- Trigger immediate optimization
- If optimization fails, truncate oldest messages
- Always preserve most recent exchanges

### Session Management
- Auto-create sessions if not exists
- Clean up old sessions periodically
- Handle concurrent access with semaphores

## Future Enhancements

1. **Streaming Responses**: Support for real-time token streaming
2. **Multi-Modal Support**: Images, files, and other media
3. **Advanced Context Management**: Topic-based summarization
4. **Provider Health Checks**: Automatic provider availability monitoring
5. **Cost Tracking**: Per-server and per-user usage tracking
6. **Custom Tokenizers**: Provider-specific token counting
7. **Conversation Branching**: Support for multiple conversation threads
8. **Context Templates**: Predefined context structures for specific use cases