using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Toggle;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    
    // Semaphores to prevent concurrent executions per guild
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSemaphores = new();

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
        // Get or create semaphore for this guild to prevent concurrent executions
        var semaphore = _guildSemaphores.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        
        // Try to acquire the semaphore - if another execution is running, skip this one
        if (!await semaphore.WaitAsync(100, cancellationToken)) // 100ms timeout
        {
            _logger.LogDebug("Activity session already running for guild {GuildId}, skipping", guildId);
            return false;
        }
        
        try
        {
            return await ExecuteActivitySessionInternalAsync(guildId, adjustFrequencyCallback, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    /// <summary>
    /// Internal implementation of activity session execution
    /// </summary>
    private async Task<bool> ExecuteActivitySessionInternalAsync(ulong guildId, Action<double>? adjustFrequencyCallback = null, CancellationToken cancellationToken = default)
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

            // Calculate engagement probability with activity dampening for high activity
            var baseChance = CalculateBaseChance(currentActivity);
            
            // Reduce the impact of activity multiplier in high-activity scenarios to prevent over-engagement
            var activityMultiplier = currentActivity >= 1.5 
                ? Math.Min(currentActivity * 0.5, 1.0) // Dampen high activity impact
                : currentActivity;
            
            var adjustedChance = baseChance * engagementMultiplier * activityMultiplier;

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

            // Select and execute appropriate engagement action with retry mechanism
            var maxRetries = 3;
            for (int retryAttempt = 0; retryAttempt < maxRetries; retryAttempt++)
            {
                try
                {
                    var actionChoice = SelectEngagementAction(currentActivity, random, botMentioned);
                    _logger.LogDebug("Attempting action {Action} for guild {GuildName} (attempt {Attempt}/{MaxRetries})", 
                        actionChoice, guild.Name, retryAttempt + 1, maxRetries);

                    var response = await ExecuteEngagementAction(_chatContextService, guildId, actionChoice);

                    if (!string.IsNullOrEmpty(response))
                    {
                        _logger.LogInformation("ActivitySession executed action {Action} for guild {GuildName}: success", 
                            actionChoice, guild.Name);
                        
                        // Clear context messages after successful engagement
                        _chatContextService.ClearContextMessages(guildId);
                        
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Action {Action} returned empty response for guild {GuildId} (attempt {Attempt}/{MaxRetries})", 
                            actionChoice, guildId, retryAttempt + 1, maxRetries);
                        
                        // If this was the last attempt, exit the loop
                        if (retryAttempt == maxRetries - 1)
                        {
                            break;
                        }

                        // Wait before retry (100ms * attempt number)
                        await Task.Delay(100 * (retryAttempt + 1), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("ActivitySession cancelled for guild {GuildId}", guildId);
                    throw; // Re-throw cancellation to maintain proper cancellation behavior
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error executing engagement action for guild {GuildId} (attempt {Attempt}/{MaxRetries}): {Error}", 
                        guildId, retryAttempt + 1, maxRetries, ex.Message);
                    
                    // If this was the last attempt, continue to return false
                    if (retryAttempt == maxRetries - 1)
                    {
                        break;
                    }

                    // Wait before retry with exponential backoff
                    var delayMs = (int)Math.Pow(2, retryAttempt) * 200; // 200ms, 400ms, 800ms
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            _logger.LogWarning("ActivitySession failed to generate content for guild {GuildId} after {MaxRetries} attempts", 
                guildId, maxRetries);
            return false;
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
            <= 0.3 => 0.25,    // 25% base chance for low activity
            <= 0.5 => 0.35,    // 35% base chance for below normal
            <= 1.0 => 0.40,    // 40% base chance for normal activity (reduced from 45%)
            <= 1.5 => 0.25,    // 25% base chance for high activity (reduced from 55% to prevent over-engagement)
            _ => 0.15           // 15% base chance for very high activity (reduced from 65% to prevent spam)
        };
    }

    /// <summary>
    /// Selects engagement action based on activity level
    /// </summary>
    private static int SelectEngagementAction(double activityLevel, Random random, bool botMentioned = false)
    {
        // When bot is mentioned in context, 90% chance of adaptive response
        if (botMentioned)
        {
            var mentionActions = new[] { 7, 7, 7, 7, 7, 7, 7, 7, 7, 5 }; // 90% adaptive response, 10% increase engagement
            return mentionActions[random.Next(mentionActions.Length)];
        }

        // HIGH ACTIVITY (>=1.5): Chat is very active - heavily favor adaptive responses
        if (activityLevel >= 1.5)
        {
            var highActivityActions = new[] { 7, 7, 7, 7, 7, 7, 7, 7, 5, 6, 1, 7 }; // 75% adaptive response, 25% other actions
            return highActivityActions[random.Next(highActivityActions.Length)];
        }

        // MODERATE-HIGH ACTIVITY (>=1.0): Good activity - favor adaptive with some variety
        if (activityLevel >= 1.0)
        {
            var moderateHighActions = new[] { 7, 7, 7, 7, 7, 7, 7, 5, 6, 1, 3, 7 }; // 66% adaptive, 34% other
            return moderateHighActions[random.Next(moderateHighActions.Length)];
        }

        // MODERATE ACTIVITY (>=0.7): Normal activity - balanced but adaptive-heavy
        if (activityLevel >= 0.7)
        {
            var moderateActions = new[] { 7, 7, 7, 7, 7, 7, 1, 5, 6, 0, 3, 7 }; // 58% adaptive, 42% other
            return moderateActions[random.Next(moderateActions.Length)];
        }

        // LOW ACTIVITY (<0.7): Chat is quiet - still favor adaptive but mix with topic starters
        var lowActivityActions = new[] { 7, 7, 7, 7, 7, 0, 0, 2, 6, 1, 4, 7 }; // 50% adaptive, 50% topic starters and engagement
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