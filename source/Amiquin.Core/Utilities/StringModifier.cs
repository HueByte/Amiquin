using System.Text;

namespace Amiquin.Core.Utilities;

public class StringModifier
{
    public static string Anomify(string input, int anonimyPercentage = 50)
    {
        var output = new StringBuilder();

        int anomifyCount = (int)(input.Length * anonimyPercentage / 100);
        output.Append(input.Substring(0, anomifyCount));

        foreach (var c in input.Skip(anomifyCount))
        {
            if (char.IsDigit(c))
            {
                output.Append('0');
            }
            else if (char.IsLetter(c))
            {
                output.Append(char.IsUpper(c) ? 'X' : 'x');
            }
            else
            {
                output.Append(c);
            }
        }

        return output.ToString();
    }

    /// <summary>
    /// Splits the input string into chunks, each with at most <paramref name="maxLength"/> characters.
    /// Words are kept intact (not broken in the middle).
    /// </summary>
    /// <param name="input">The input string to split.</param>
    /// <param name="maxLength">The maximum allowed length of each chunk.</param>
    /// <returns>A list of string chunks.</returns>
    public static List<string> Chunkify(string input, int maxLength)
    {
        if (maxLength < 1)
            throw new ArgumentException("maxLength must be at least 1", nameof(maxLength));

        // Split the input string on spaces while removing any empty entries.
        string[] words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> chunks = new();
        StringBuilder currentChunk = new();

        foreach (string word in words)
        {
            // Determine if we need to add a space before appending the next word.
            int additionalSpace = currentChunk.Length == 0 ? 0 : 1;

            // If adding the word (plus a space if necessary) would exceed maxLength...
            if (currentChunk.Length + additionalSpace + word.Length > maxLength)
            {
                if (currentChunk.Length == 0)
                {
                    chunks.Add(word);
                }
                else
                {
                    chunks.Add(currentChunk.ToString());
                    // Start a new chunk with the current word.
                    currentChunk.Clear();
                    currentChunk.Append(word);
                }
            }
            else
            {
                if (currentChunk.Length > 0)
                    currentChunk.Append(' ');

                currentChunk.Append(word);
            }
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString());

        return chunks;
    }
}