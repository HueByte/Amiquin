namespace Amiquin.Core;

public class Constants
{
    public class ToggleNames
    {
        public class SystemExclusiveToggles
        {
            public const string EnableNews = "EnableNews";
        }

        public const string SystemTogglePrefix = "System::";
        public const string EnableTTS = "EnableTTS";
        public const string EnableJoinMessage = "EnableJoinMessage";
        public const string EnableChat = "EnableChat";

        public static List<string> SystemExlusiveToggles = new()
        {
            SystemExclusiveToggles.EnableNews
        };

        public static List<string> Toggles = new()
        {
            EnableTTS,
            EnableJoinMessage,
            EnableChat
        };
    }

    public class Environment
    {
        public const string BotToken = "AMQ_BOT_TOKEN";
        public const string OpenAiKey = "AMQ_OPEN_AI_KEY";
        public const string LogsPath = "AMQ_LOGS_PATH";
        public const string PrintLogo = "AMQ_PRINT_LOGO";
        public const string SQLitePath = "AMQ_SQLITE_PATH";
        public const string TTSModelName = "AMQ_TTS_MODEL_NAME";
        public const string PiperCommand = "AMQ_PIPER_COMMAND";
        public const string MessageFetchCount = "AMQ_MESSAGE_FETCH_COUNT";
    }

    public class CacheKeys
    {
        public const string JoinMessageKey = "ServerJoinMessage";
        public const string CorePersonaMessageKey = "Persona";
        public const string ComputedPersonaMessageKey = "ComputedPersona";
        public const string ServerTogglesCreated = "ServerTogglesCreated";
    }

    public class AI
    {
        public const string Gpt3Model = "gpt-3.5-turbo";
        public const string Gpt4Model = "gpt-4-turbo";
        public const string Gpt4oMiniModel = "gpt-4o-mini";
        public const string Gpt4oModel = "gpt-4o-turbo";
    }

    public class BotMetadata
    {
        public const string Mood = "[$Mood$]";
        public const string BotName = "Amiquin";
        public const string BotVersion = "1.0.0";
    }

    public class Paths
    {
        public static string MessageBasePath = Path.Join(AppContext.BaseDirectory, "Messages");
        public static string TTSBasePath = Path.Join(AppContext.BaseDirectory, "TTS");
        public static string TTSBaseOutputPath = Path.Join(TTSBasePath, "output");
    }

    public class Emoji
    {
        public const string SlugParty = "<a:slugparty:1352447645121118210>";
        public const string Amiquin = "<:amiquin:1352444818814664705>";
        public const string AmiquinR = "<:amiquinR:1352445227670962246>";
        public const string Blank = "<:blank:1352444144752001094>";
        public const string Nacho = "<:nachoquin:1352442298583089264>";
    }

    public class APIs
    {
        public class NewsApi
        {
            public const string Category = "all_news";
            public const int MaxLimit = 5;
            public const bool IncludeCard = true;
        }
    }
}