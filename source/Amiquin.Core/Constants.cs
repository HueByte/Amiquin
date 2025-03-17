namespace Amiquin.Core;

public class Constants
{
    // Environment Variables
    public const string BotToken = "BOT_TOKEN";
    public const string OpenAiKey = "OPEN_AI_KEY";
    public const string LogsPath = "LOGS_PATH";
    public const string PrintLogo = "PRINT_LOGO";
    public const string SQLitePath = "SQLITE_PATH";

    // Message Cache Keys
    public const string JoinMessageKey = "ServerJoinMessage";
    public const string CorePersonaMessageKey = "Persona";
    public const string ComputedPersonaMessageKey = "ComputedPersona";

    // AI
    public const string Gpt4oMiniModel = "gpt-4o-mini";

    // General
    public const string Mood = "[$Mood$]";
    public const string BotName = "Amiquin";
    public const string BotVersion = "1.0.0";
    public static string MessageBasePath = Path.Join(AppContext.BaseDirectory, "Messages");

    // APIs
    public class NewsApi
    {
        public const string Category = "all_news";
        public const int MaxLimit = 5;
        public const bool IncludeCard = true;
    }
}