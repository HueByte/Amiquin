using Discord;

namespace Amiquin.Core.Utilities;

public static class DiscordUtilities
{
    public static IEnumerable<Embed> ChunkMessage(string message, Func<string, int, int, Embed> embedBuilder)
    {
        var messageChunks = StringModifier.Chunkify(message, 2000);

        foreach (var chunk in messageChunks)
        {
            int chunkIndex = messageChunks.IndexOf(chunk) + 1;
            yield return embedBuilder(chunk, chunkIndex, messageChunks.Count);
        }
    }
}