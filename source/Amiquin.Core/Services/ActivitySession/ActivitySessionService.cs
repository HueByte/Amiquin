using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Toggle;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.ActivitySession;

/// <summary>
/// Service for executing activity session logic for Discord servers
/// </summary>
public class ActivitySessionService : IActivitySessionService
{
    private readonly ILogger<ActivitySessionService> _logger;
    private readonly IChatContextService _chatContextService;
    private readonly IToggleService _toggleService;
    private readonly DiscordShardedClient _discordClient;

    public ActivitySessionService(
        ILogger<ActivitySessionService> logger,
        IChatContextService chatContextService,
        IToggleService toggleService,
        DiscordShardedClient discordClient)
    {
        _logger = logger;
        _chatContextService = chatContextService;
        _toggleService = toggleService;
        _discordClient = discordClient;
    }

    /// <summary>
    /// Executes activity session logic for a specific guild
    /// </summary>
    public async Task<bool> ExecuteActivitySessionAsync(ulong guildId, Action<double>? adjustFrequencyCallback = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing ActivitySession for guild {GuildId}", guildId);

            // Check cancellation token
            cancellationToken.ThrowIfCancellationRequested();

            // Check if LiveJob is enabled for this server
            if (!await _toggleService.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            {
                _logger.LogDebug("LiveJob disabled for guild {GuildId}, skipping execution", guildId);
                return false;
            }

            // Check cancellation token before continuing
            cancellationToken.ThrowIfCancellationRequested();

            // Get current activity level
            var currentActivity = _chatContextService.GetCurrentActivityLevel(guildId);

            // Get context messages
            var contextMessages = _chatContextService.GetContextMessages(guildId);
            if (contextMessages.Length == 0)
            {
                _logger.LogDebug("No context messages for guild {GuildId}, adjusting frequency", guildId);

                // Adjust frequency for low activity and return
                adjustFrequencyCallback?.Invoke(0.1);
                return false;
            }

            _logger.LogDebug("Guild {GuildId} activity: {Activity}, messages: {Count}", guildId, currentActivity, contextMessages.Length);

            // Adjust job frequency based on activity
            adjustFrequencyCallback?.Invoke(currentActivity);

            // Check for bot mentions in recent messages
            var recentMessages = contextMessages.Take(contextMessages.Length - 1).TakeLast(4).ToArray();
            var botMentioned = recentMessages.Any(msg =>
                msg.Contains("Amiquin", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("@Amiquin", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains($"<@{_discordClient.CurrentUser?.Id}>", StringComparison.OrdinalIgnoreCase));

            // Get engagement multiplier
            var engagementMultiplier = _chatContextService.GetEngagementMultiplier(guildId);

            // Calculate engagement probability
            var baseChance = CalculateBaseChance(currentActivity);
            var adjustedChance = baseChance * engagementMultiplier * currentActivity;

            // Force engagement for mentions
            if (botMentioned)
            {
                adjustedChance = 1.0; // 100% chance
                _logger.LogDebug("Bot mentioned in recent history for guild {GuildId}, forcing engagement", guildId);
            }
            else
            {
                adjustedChance = Math.Min(adjustedChance, 0.90); // Cap at 90% (increased from 85%)
            }

            // Random engagement check
            var random = new Random();
            var shouldEngage = random.NextDouble() < adjustedChance;

            _logger.LogDebug("Guild {GuildId}: activity={Activity}, multiplier={Multiplier}, chance={Chance}%, engage={Engage}",
                guildId, currentActivity, engagementMultiplier, adjustedChance * 100, shouldEngage);

            if (!shouldEngage)
            {
                _logger.LogDebug("Engagement check failed for guild {GuildId}", guildId);
                return false;
            }

            // Find the guild and execute engagement action
            var guild = _discordClient.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Could not find guild {GuildId}", guildId);
                return false;
            }

            // Check cancellation token before engagement action
            cancellationToken.ThrowIfCancellationRequested();

            // Select and execute appropriate engagement action
            var actionChoice = SelectEngagementAction(currentActivity, random);
            var response = await ExecuteEngagementAction(_chatContextService, guildId, actionChoice);

            if (!string.IsNullOrEmpty(response))
            {
                _logger.LogInformation("ActivitySession executed action {Action} for guild {GuildName}: success", actionChoice, guild.Name);
                return true;
            }
            else
            {
                _logger.LogWarning("ActivitySession failed to generate content for guild {GuildId} using action {Action}", guildId, actionChoice);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ActivitySession for guild {GuildId}", guildId);
            throw; // Re-throw to let calling code handle retries
        }
    }

    /// <summary>
    /// Calculates base engagement chance based on activity level
    /// </summary>
    private static double CalculateBaseChance(double activityLevel)
    {
        return activityLevel switch
        {
            <= 0.3 => 0.25,    // 25% base chance for low activity (increased from 10%)
            <= 0.5 => 0.35,    // 35% base chance for below normal (increased from 15%)
            <= 1.0 => 0.45,    // 45% base chance for normal activity (increased from 25%)
            <= 1.5 => 0.55,    // 55% base chance for high activity (increased from 35%)
            _ => 0.65           // 65% base chance for very high activity (increased from 45%)
        };
    }

    /// <summary>
    /// Selects engagement action based on activity level
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