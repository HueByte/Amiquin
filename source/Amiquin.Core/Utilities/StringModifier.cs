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
}