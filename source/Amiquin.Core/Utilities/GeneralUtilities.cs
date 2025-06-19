namespace Amiquin.Core.Utilities;

public class GeneralUtilities
{
    public static string GetFormattedTimeSpan(TimeSpan timeSpan)
    {
        return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    public static string GetFormattedDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static bool IsLinux()
    {
        int p = (int)Environment.OSVersion.Platform;
        return (p == 4) || (p == 6) || (p == 128);
    }
}