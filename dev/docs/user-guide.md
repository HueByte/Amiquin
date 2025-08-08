# User Guide

This guide explains how to use Amiquin Discord bot features.

## Getting Started

### Prerequisites
- Discord server with bot added
- OpenAI API key configured
- Bot running and online

### Basic Chat Commands

#### `/chat-new <message>`
Start or continue a conversation with the AI.

**Examples:**
```
/chat-new Hello, how are you?
/chat-new What's the weather like today?
/chat-new Can you help me write some code?
```

#### `/clear-chat`
Clear your conversation history with the bot.

This will reset your session and start fresh conversations.

#### `/chat-stats`
View statistics about your current conversation session.

Shows:
- Number of messages exchanged
- Session start time
- Last message time
- Estimated tokens used

## Chat Features

### Session Management
- **Per-user sessions**: Each user has their own conversation context
- **Per-channel isolation**: Conversations are separate in different channels
- **Persistent history**: Conversations continue across bot restarts
- **Automatic optimization**: Long conversations are intelligently summarized

### AI Capabilities
- **Natural conversation**: Contextual responses based on conversation history
- **Multiple models**: Support for different OpenAI models
- **Token optimization**: Automatic history management to stay within limits
- **Customizable persona**: Bot personality can be configured

## Advanced Features

### Long Messages
If the AI response exceeds Discord's 2000 character limit, it will be automatically split into multiple messages while preserving formatting.

### Error Handling
- Connection issues are handled gracefully
- Rate limiting is managed automatically
- Clear error messages for configuration issues

## Configuration

Users cannot directly configure the bot, but administrators can adjust:
- AI model selection
- System message/personality
- Token limits
- Session timeout settings

## Troubleshooting

### Bot Not Responding
1. Check if bot is online
2. Verify bot has necessary permissions
3. Ensure OpenAI API key is configured
4. Check server logs for errors

### Slow Responses
- May indicate high OpenAI API usage
- Consider using faster models (gpt-4o-mini)
- Check internet connectivity

### Memory Issues
- Use `/clear-chat` to reset session
- Long conversations may need periodic clearing
- Bot automatically optimizes history when needed