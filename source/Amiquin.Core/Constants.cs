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
        public const string EnableLiveJob = "EnableLiveJob";
        public const string EnableAIWelcome = "EnableAIWelcome";
        public const string EnableNSFW = "EnableNSFW";
        public const string EnableDailyNSFW = "EnableDailyNSFW";

        public static List<string> SystemExclusiveTogglesList = new()
        {
            SystemExclusiveToggles.EnableNews
        };

        public static List<string> Toggles = new()
        {
            EnableTTS,
            EnableJoinMessage,
            EnableChat,
            EnableLiveJob,
            EnableAIWelcome,
            EnableNSFW,
            EnableDailyNSFW
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

        // Provider-specific connection strings
        public const string AmiquinSqliteConnectionString = "AMQ_ConnectionStrings__Amiquin-Sqlite";
        public const string AmiquinMysqlConnectionString = "AMQ_ConnectionStrings__Amiquin-Mysql";
        #endregion
    }

    public class CacheKeys
    {
        #region Cache Keys
        public const string JoinMessageKey = "ServerJoinMessage";
        public const string CoreSystemMessageKey = "System";
        public const string ComputedSystemMessageKey = "ComputedSystem";
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
        public const string Gpt4oModel = "gpt-4o";
        public const string GrokBetaModel = "grok-beta";
        public const string Grok2Model = "grok-2-1212";
        public const string Grok2VisionModel = "grok-2-vision-1212";

        public static readonly List<string> SupportedModels = new()
        {
            Gpt4oMiniModel,
            Gpt4oModel,
            Gpt4Model,
            Gpt3Model,
            GrokBetaModel,
            Grok2Model,
            Grok2VisionModel
        };
        #endregion
    }

    public class SystemKeywordsCache
    {
        public const string Mood = "[$Mood$]";
        public const string Name = "[$Name$]";
        public const string Version = "[$Version$]";
    }

    public class Paths
    {
        #region Application Paths
        public static string Assets = Path.Join(AppContext.BaseDirectory, "Data", "Messages");
        public static string TTSBasePath = Path.Join(AppContext.BaseDirectory, "Data", "TTS");
        public static string TTSBaseOutputPath = Path.Join(TTSBasePath, "output");
        public static string ApplicationTempPath = Path.Combine(Path.GetTempPath(), "Amiquin");
        #endregion

        #region Data Paths
        public const string DefaultDataLogsPath = "Data/Logs";
        public const string DefaultDataMessagesPath = "Data/Messages";
        public const string DefaultDataSessionsPath = "Data/Sessions";
        public const string DefaultDataPluginsPath = "Data/Plugins";
        #endregion
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

    public class DefaultValues
    {
        #region Default Values
        public const string BotName = "Amiquin";
        public const string BotVersion = "1.0.0";
        public const string DefaultLogsPath = "Data/Logs";
        public const string UnknownValue = "Unknown";
        public const string UnknownServer = "Unknown Server";
        public const string UnknownUser = "Unknown";
        public const string NoneActivity = "None";
        public const string DefaultSQLiteDatabase = "data.db";
        public const string DefaultSQLiteConnectionString = "Data Source=Data/Database/amiquin.db";
        public const string DefaultMySQLConnectionString = "Server=localhost;Database=amiquin;User=root;Password=;";
        public const string InMemoryDatabase = "Data Source=:memory:";
        public const string ContainerEnvironmentVariable = "DOTNET_RUNNING_IN_CONTAINER";
        public const string ContainerEnvironmentValue = "true";
        #endregion
    }

    public class MigrationAssemblies
    {
        #region Migration Assembly Names
        public const string SQLite = "Amiquin.Sqlite";
        public const string MySQL = "Amiquin.MySql";
        #endregion
    }

    public class DatabaseIndexNames
    {
        #region Index Names
        public const string ChatSessionsScopeOwner = "IX_ChatSessions_Scope_Owner";
        public const string ChatSessionsActivity = "IX_ChatSessions_Activity";
        public const string ChatSessionsCreated = "IX_ChatSessions_Created";
        #endregion
    }

    public class Timeouts
    {
        #region Timeout Values (in seconds)
        public const int DatabaseOperationTimeoutSeconds = 30;
        public const int SystemMetricsTimeoutSeconds = 10;
        public const int JobCancellationTimeoutSeconds = 5;
        public const int DefaultCommandTimeoutSeconds = 120;
        public const int SemaphoreTimeoutSeconds = 30;
        #endregion

        #region Cache Timeouts (in minutes)
        public const int DefaultCacheTimeoutMinutes = 30;
        public const int MessageCacheTimeoutMinutes = 5;
        public const int SemaphoreCleanupIntervalMinutes = 60;
        #endregion
    }

    public class Limits
    {
        #region General Limits
        public const int EmbedDescriptionMaxLength = 2048;
        public const int MessageHistoryMinCount = 1;
        public const int MessageHistoryMaxCount = 100;
        public const int CacheDisplayThreshold = 10;
        public const int SlowOperationThresholdMs = 1000;
        #endregion

        #region Job Manager Limits
        public const int MaxHealthCheckIntervalSeconds = 3600; // 1 hour
        public const int MaxJobTimeoutSeconds = 3600; // 1 hour
        public const int MaxQueueTimeoutSeconds = 1800; // 30 minutes
        public const int MaxPendingJobsLimit = 1000;
        #endregion

        #region Model Context Limits
        public const int Grok2ContextLimit = 131072; // 128k context
        public const int Grok2VisionContextLimit = 32768; // 32k context for vision model
        public const int GrokBetaContextLimit = 131072; // 128k context
        #endregion

        #region Network and Protocol Limits
        public const int IPv4AddressFamily = 4;
        public const int IPv6AddressFamily = 6;
        public const int IPv6LocalAddressFamily = 128;
        #endregion
    }

    public class JobFrequencies
    {
        #region Default Job Frequencies (in seconds)
        public const int StatisticsCollectionFrequency = 300; // 5 minutes
        public const int CleanupServiceFrequency = 3600; // 1 hour
        public const int HealthCheckFrequency = 60; // 1 minute
        #endregion
    }

    public class SystemDefaults
    {
        #region System Configuration
        public const int NewsSystemTokenLimit = 500;
        public const int SystemCacheDurationDays = 1;
        public const string DefaultSystemTemplate = "You are [$Name$]. The AI assistant for discord.\n[$Mood$]";
        public const string NewsMoodNotAvailableMessage = "I couldn't find any news at the moment.";
        public const string NewsProcessingErrorMessage = "I'm having trouble processing the news right now.";
        #endregion
    }

    public class MessageCacheDefaults
    {
        #region Message Cache Configuration
        public const int MemoryCacheExpirationDays = 5;
        public const int DefaultModifyMessageTimeoutMinutes = 30;
        public const int DefaultMessageFetchCount = 40;
        #endregion
    }
}