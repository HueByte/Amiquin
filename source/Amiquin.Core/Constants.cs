namespace Amiquin.Core;

public class Constants
{
    public class ToggleNames
    {
        #region Toggle Names
        public class SystemExclusiveToggles
        {
            public const string EnableNews = "EnableNews";
        }

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
        #endregion
    }

    public class Environment
    {
        #region General Environment Variables
        public const string BotToken = "AMQ_BOT_TOKEN";
        public const string OpenAiKey = "AMQ_OPEN_AI_KEY";
        public const string PrintLogo = "AMQ_PRINT_LOGO";
        public const string TTSModelName = "AMQ_TTS_MODEL_NAME";
        public const string PiperCommand = "AMQ_PIPER_COMMAND";
        public const string MessageFetchCount = "AMQ_MESSAGE_FETCH_COUNT";
        #endregion

        #region Bot Metadata 
        public const string BotName = "AMQ_BOT_NAME";
        public const string BotVersion = "AMQ_BOT_VERSION";
        #endregion

        #region Database Environment Variables
        public const string DatabaseMode = "AMQ_DATABASE_MODE";
        public const string LogsPath = "AMQ_LOGS_PATH";
        public const string SQLitePath = "AMQ_SQLITE_PATH";
        public const string DbConnectionString = "AMQ_DB_CONNECTION_STRING";
        public const string DbName = "AMQ_DB_NAME";
        public const string DbRootPassword = "AMQ_DB_ROOT_PASSWORD";
        public const string DbUserName = "AMQ_DB_USER";
        public const string DbUserPassword = "AMQ_DB_USER_PASSWORD";
        #endregion
    }

    public class CacheKeys
    {
        #region Cache Keys
        public const string JoinMessageKey = "ServerJoinMessage";
        public const string CorePersonaMessageKey = "Persona";
        public const string ComputedPersonaMessageKey = "ComputedPersona";
        public const string ServerTogglesCreated = "ServerTogglesCreated";
        public const string ServerToggles = "ServerToggles";
        public const string ServerMeta = "ServerMeta";
        public const string GlobalToggles = "GlobalToggles";
        #endregion
    }

    public class AI
    {
        #region AI Model Names
        public const string Gpt3Model = "gpt-3.5-turbo";
        public const string Gpt4Model = "gpt-4-turbo";
        public const string Gpt4oMiniModel = "gpt-4o-mini";
        public const string Gpt4oModel = "gpt-4o-turbo";
        #endregion
    }

    public class PersonaKeywordsCache
    {
        public const string Mood = "[$Mood$]";
        public const string Name = "[$Name$]";
        public const string Version = "[$Version$]";
    }

    public class Paths
    {
        public static string Assets = Path.Join(AppContext.BaseDirectory, "Messages");
        public static string TTSBasePath = Path.Join(AppContext.BaseDirectory, "TTS");
        public static string TTSBaseOutputPath = Path.Join(TTSBasePath, "output");
    }

    public class Emoji
    {
        #region Emoji Constants
        public const string SlugParty = "<a:slugparty:1352447645121118210>";
        public const string Amiquin = "<:amiquin:1352444818814664705>";
        public const string AmiquinR = "<:amiquinR:1352445227670962246>";
        public const string Blank = "<:blank:1352444144752001094>";
        public const string Nacho = "<:nachoquin:1352442298583089264>";
        #endregion
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