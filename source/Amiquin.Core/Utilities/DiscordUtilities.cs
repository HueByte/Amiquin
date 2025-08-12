using Discord;

namespace Amiquin.Core.Utilities;

public static class DiscordUtilities
{
    /// <summary>
    /// Discord's maximum message content length
    /// </summary>
    public const int MaxMessageLength = 2000;

    /// <summary>
    /// Chunks a message into Discord-compatible segments (max 2000 characters each).
    /// Words are kept intact and won't be broken in the middle.
    /// </summary>
    /// <param name="message">The message to chunk</param>
    /// <param name="maxLength">Maximum length per chunk (default: 2000)</param>
    /// <returns>List of message chunks</returns>
    public static List<string> ChunkMessage(string message, int maxLength = MaxMessageLength)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new List<string>();

        // Use StringModifier's Chunkify method which preserves word boundaries
        return StringModifier.Chunkify(message, maxLength);
    }

    /// <summary>
    /// Creates embed-based chunks for longer messages
    /// </summary>
    public static IEnumerable<Embed> ChunkMessageAsEmbeds(string message, Func<string, int, int, Embed> embedBuilder)
    {
        var messageChunks = ChunkMessage(message);

        for (int i = 0; i < messageChunks.Count; i++)
        {
            yield return embedBuilder(messageChunks[i], i + 1, messageChunks.Count);
        }
    }

    /// <summary>
    /// Sends a message to a Discord channel, automatically chunking it if it exceeds the length limit.
    /// If chunked, adds part indicators (1/3, 2/3, etc.) to help users track the full response.
    /// </summary>
    /// <param name="channel">The Discord channel to send to</param>
    /// <param name="message">The message to send</param>
    /// <param name="messageReference">Optional message reference for replies</param>
    /// <param name="maxLength">Maximum length per chunk (default: 2000)</param>
    /// <returns>List of sent Discord messages</returns>
    public static async Task<List<IUserMessage>> SendChunkedMessageAsync(
        IMessageChannel channel,
        string message,
        MessageReference? messageReference = null,
        int maxLength = MaxMessageLength)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new List<IUserMessage>();

        var chunks = ChunkMessage(message, maxLength);
        var sentMessages = new List<IUserMessage>();

        for (int i = 0; i < chunks.Count; i++)
        {
            string chunkToSend;

            // Add part indicators if message is chunked (multiple parts)
            if (chunks.Count > 1)
            {
                var partIndicator = $" `({i + 1}/{chunks.Count})`";
                // Ensure the part indicator fits within the limit
                var availableLength = maxLength - partIndicator.Length;
                var chunk = chunks[i];

                if (chunk.Length > availableLength)
                {
                    // This shouldn't happen with proper chunking, but safety check
                    chunk = chunk.Substring(0, availableLength);
                }

                chunkToSend = chunk + partIndicator;
            }
            else
            {
                chunkToSend = chunks[i];
            }

            // Only use messageReference for the first chunk to avoid reply spam
            var reference = (i == 0) ? messageReference : null;
            var sentMessage = await channel.SendMessageAsync(chunkToSend, messageReference: reference);
            sentMessages.Add(sentMessage);

            // Small delay between chunks to avoid rate limiting and improve readability
            if (i < chunks.Count - 1)
            {
                await Task.Delay(500);
            }
        }

        return sentMessages;
    }
}