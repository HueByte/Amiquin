using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Toggle;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.RunnableJobs;

/// <summary>
/// Individual server activity session job that handles engagement for a specific server.
/// This job adapts its behavior based on real-time activity levels.
/// </summary>
public class ActivitySessionJob
{
    private readonly ulong _guildId;
    private readonly ILogger<ActivitySessionJob> _logger;
    
    public ulong GuildId => _guildId;
    public string JobId => $"ActivitySession_{_guildId}";
    public DateTime CreatedAt { get; }
    public DateTime LastExecutedAt { get; private set; }
    public int ExecutionCount { get; private set; }
    public double LastActivityLevel { get; private set; }
    public int CurrentFrequencySeconds { get; private set; } = 8; // Start with fast frequency

    public ActivitySessionJob(ulong guildId, ILogger<ActivitySessionJob> logger)
    {
        _guildId = guildId;
        _logger = logger;
        CreatedAt = DateTime.UtcNow;
        LastExecutedAt = DateTime.MinValue;
    }

    /// <summary>
    /// Executes the activity session logic for this specific server
    /// </summary>
    public async Task ExecuteAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        try
        {
            ExecutionCount++;
            LastExecutedAt = DateTime.UtcNow;
            
            _logger.LogDebug("Executing ActivitySession for guild {GuildId} (execution #{Count})", _guildId, ExecutionCount);

            using var scope = serviceScopeFactory.CreateScope();
            var chatContextService = scope.ServiceProvider.GetRequiredService<IChatContextService>();
            var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();

            // Check if LiveJob is enabled for this server
            if (!await toggleService.IsEnabledAsync(_guildId, Constants.ToggleNames.EnableLiveJob))
            {
                _logger.LogDebug("LiveJob disabled for guild {GuildId}, skipping execution", _guildId);
                return;
            }

            // Get current activity level
            var currentActivity = chatContextService.GetCurrentActivityLevel(_guildId);
            LastActivityLevel = currentActivity;

            // Get context messages
            var contextMessages = chatContextService.GetContextMessages(_guildId);
            if (contextMessages.Length == 0)
            {
                _logger.LogDebug("No context messages for guild {GuildId}, skipping execution", _guildId);
                
                // Reduce frequency when there's no activity
                AdjustFrequency(0.1);
                return;
            }

            _logger.LogDebug("Guild {GuildId} activity: {Activity}, messages: {Count}", _guildId, currentActivity, contextMessages.Length);

            // Adjust frequency based on activity
            AdjustFrequency(currentActivity);

            // Check for bot mentions in recent messages (skip most recent to avoid duplicates with EventHandler)
            var recentMessages = contextMessages.Take(contextMessages.Length - 1).TakeLast(4).ToArray();
            var botMentioned = recentMessages.Any(msg => 
                msg.Contains("@Amiquin", StringComparison.OrdinalIgnoreCase) || 
                msg.Contains($"<@{discordClient.CurrentUser?.Id}>", StringComparison.OrdinalIgnoreCase));

            // Get engagement multiplier
            var engagementMultiplier = chatContextService.GetEngagementMultiplier(_guildId);
            
            // Calculate engagement probability
            var baseChance = CalculateBaseChance(currentActivity);
            var adjustedChance = baseChance * engagementMultiplier * currentActivity;
            
            // Force engagement for mentions or recent spikes
            if (botMentioned)
            {
                adjustedChance = 1.0; // 100% chance
                _logger.LogDebug("Bot mentioned in recent history for guild {GuildId}, forcing engagement", _guildId);
            }
            else
            {
                adjustedChance = Math.Min(adjustedChance, 0.85); // Cap at 85%
            }

            // Random engagement check
            var random = new Random();
            var shouldEngage = random.NextDouble() < adjustedChance;

            _logger.LogDebug("Guild {GuildId}: activity={Activity}, multiplier={Multiplier}, chance={Chance}%, engage={Engage}",
                _guildId, currentActivity, engagementMultiplier, adjustedChance * 100, shouldEngage);

            if (!shouldEngage)
            {
                _logger.LogDebug("Engagement check failed for guild {GuildId}", _guildId);
                return;
            }

            // Find the guild and execute engagement action
            var guild = discordClient.GetGuild(_guildId);
            if (guild == null)
            {
                _logger.LogWarning("Could not find guild {GuildId}", _guildId);
                return;
            }

            // Select and execute appropriate engagement action
            var actionChoice = SelectEngagementAction(currentActivity, random);
            var response = await ExecuteEngagementAction(chatContextService, _guildId, actionChoice);

            if (!string.IsNullOrEmpty(response))
            {
                _logger.LogInformation("ActivitySession executed action {Action} for guild {GuildName}: success", actionChoice, guild.Name);
            }
            else
            {
                _logger.LogWarning("ActivitySession failed to generate content for guild {GuildId} using action {Action}", _guildId, actionChoice);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ActivitySession for guild {GuildId}", _guildId);
        }
    }

    /// <summary>
    /// Adjusts execution frequency based on activity level
    /// </summary>
    private void AdjustFrequency(double activityLevel)
    {
        var newFrequency = activityLevel switch
        {
            <= 0.1 => 30,       // Very low activity: every 30 seconds
            <= 0.3 => 20,       // Low activity: every 20 seconds  
            <= 0.7 => 15,       // Normal activity: every 15 seconds
            <= 1.3 => 10,       // High activity: every 10 seconds
            <= 1.5 => 8,        // Very high activity: every 8 seconds
            _ => 6              // Extreme activity: every 6 seconds
        };

        if (newFrequency != CurrentFrequencySeconds)
        {
            var oldFrequency = CurrentFrequencySeconds;
            CurrentFrequencySeconds = newFrequency;
            _logger.LogDebug("Adjusted frequency for guild {GuildId}: {OldFreq}s → {NewFreq}s (activity: {Activity})", 
                _guildId, oldFrequency, newFrequency, activityLevel);
        }
    }

    /// <summary>
    /// Calculates base engagement chance based on activity level
    /// </summary>
    private static double CalculateBaseChance(double activityLevel)
    {
        return activityLevel switch
        {
            <= 0.3 => 0.1,     // 10% base chance for low activity
            <= 0.5 => 0.15,    // 15% base chance for below normal
            <= 1.0 => 0.25,    // 25% base chance for normal activity
            <= 1.5 => 0.35,    // 35% base chance for high activity
            _ => 0.45           // 45% base chance for very high activity
        };
    }

    /// <summary>
    /// Selects engagement action based on activity level
    /// Actions: 0=StartTopic, 1=AskQuestion, 2=ShareInteresting, 3=ShareFunny, 4=ShareUseful, 5=IncreaseEngagement, 6=ShareOpinion, 7=AdaptiveResponse
    /// </summary>
    private static int SelectEngagementAction(double activityLevel, Random random)
    {
        // HIGH ACTIVITY (>=1.5): Chat is very active - participate in conversations
        if (activityLevel >= 1.5)
        {
            var highActivityActions = new[] { 5, 5, 6, 6, 7, 1, 1, 3 }; // Heavily favor participation + opinions + adaptive + questions + humor
            return highActivityActions[random.Next(highActivityActions.Length)];
        }
        
        // MODERATE-HIGH ACTIVITY (>=1.0): Good activity - mix of participation and questions
        if (activityLevel >= 1.0)
        {
            var moderateHighActions = new[] { 5, 6, 7, 1, 1, 3, 2 }; // More questions + opinions + adaptive + some participation
            return moderateHighActions[random.Next(moderateHighActions.Length)];
        }
        
        // MODERATE ACTIVITY (>=0.7): Normal activity - balanced approach
        if (activityLevel >= 0.7)
        {
            var moderateActions = new[] { 1, 3, 5, 6, 7, 2, 4, 0 }; // Balanced mix with new actions
            return moderateActions[random.Next(moderateActions.Length)];
        }
        
        // LOW ACTIVITY (<0.7): Chat is quiet - start topics and share content
        var lowActivityActions = new[] { 0, 0, 7, 7, 2, 2, 4, 6, 1 }; // Heavy topic starters + adaptive responses + content sharing + opinions
        return lowActivityActions[random.Next(lowActivityActions.Length)];
    }

    /// <summary>
    /// Executes a specific engagement action
    /// </summary>
    private static async Task<string?> ExecuteEngagementAction(IChatContextService chatContextService, ulong guildId, int actionChoice)
    {
        return actionChoice switch
        {
            0 => await chatContextService.StartTopicAsync(guildId),
            1 => await chatContextService.AskQuestionAsync(guildId),
            2 => await chatContextService.ShareInterestingContentAsync(guildId),
            3 => await chatContextService.ShareFunnyContentAsync(guildId),
            4 => await chatContextService.ShareUsefulContentAsync(guildId),
            5 => await chatContextService.IncreaseEngagementAsync(guildId),
            6 => await chatContextService.ShareOpinionAsync(guildId),
            7 => await chatContextService.AdaptiveResponseAsync(guildId),
            _ => null
        };
    }
}