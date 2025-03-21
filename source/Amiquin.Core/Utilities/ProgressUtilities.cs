using System.Text;

namespace Amiquin.Core.Utilities;

public class ProgressUtilities
{
    public static double GetCompletionPercentage(int current, int total)
    {
        return (double)current / total;
    }

    public static string GenerateConsoleProgressBar(double completion, int barLength = 50)
    {
        int progressLength = (int)(completion * barLength);
        int emptyLength = barLength - progressLength;

        StringBuilder progressBar = new();

        progressBar.Append('[');
        progressBar.Append('=', progressLength);
        progressBar.Append('>');
        progressBar.Append(' ', emptyLength);
        progressBar.Append(']');

        return progressBar.ToString();
    }

    public static string GenerateNachoProgressBar(double completion, int barLength = 50)
    {
        int progressLength = (int)(completion * barLength);
        int emptyLength = barLength - progressLength;

        StringBuilder progressBar = new();

        var emptyEmoteAndAmiquin = string.Join(string.Empty, Enumerable.Repeat(Constants.Emoji.Blank, progressLength - 1)) + Constants.Emoji.AmiquinR;
        var nachoquinEmote = string.Join(string.Empty, Enumerable.Repeat(Constants.Emoji.Nacho, emptyLength));

        progressBar.Append('[');
        progressBar.Append(emptyEmoteAndAmiquin);
        progressBar.Append(nachoquinEmote);
        progressBar.Append(']');

        return progressBar.ToString();
    }
}