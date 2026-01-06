using Amiquin.Core.Abstractions;
using Amiquin.Core.Configuration;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Sleep;
using Amiquin.Core.Services.Toggle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.ActivitySession;

/// <summary>
/// Service for executing activity session logic for Discord servers.
/// Integrates with the initiative system for natural, human-like engagement.
/// </summary>
public class ActivitySessionService : IActivitySessionService
{
    private readonly ILogger<ActivitySessionService> _logger;
    private readonly IChatContextService _chatContextService;
    private readonly IToggleService _toggleService;
    private readonly IDiscordClientWrapper _discordClient;
    private readonly ISleepService _sleepService;
    private readonly InitiativeOptions _initiativeOptions;

    // Semaphores to prevent concurrent executions per guild
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSemaphores = new();

    // Track last usage to clean up unused semaphores
    private readonly ConcurrentDictionary<ulong, DateTime> _lastSemaphoreUsage = new();

    public ActivitySessionService(
        ILogger<ActivitySessionService> logger,
        IChatContextService chatContextService,
        IToggleService toggleService,
        IDiscordClientWrapper discordClient,
        ISleepService sleepService,
        IOptions<InitiativeOptions> initiativeOptions)
    {
        _logger = logger;
        _chatContextService = chatContextService;
        _toggleService = toggleService;
        _discordClient = discordClient;
        _sleepService = sleepService;
        _initiativeOptions = initiativeOptions.Value;
    }

    /// <summary>
    /// Executes activity session logic for a specific guild
    /// </summary>
    public async Task<bool> ExecuteActivitySessionAsync(ulong guildId, Action<double>? adjustFrequencyCallback = null, CancellationToken cancellationToken = default)
    {
        // Get or create semaphore for this guild to prevent concurrent executions
        var semaphore = _guildSemaphores.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

        // Update last usage timestamp
        _lastSemaphoreUsage[guildId] = DateTime.UtcNow;

        // Try to acquire the semaphore - if another execution is running, skip this one immediately
        if (!await semaphore.WaitAsync(0, cancellationToken)) // No timeout - immediate check
        {
            _logger.LogDebug("Activity session already running for guild {GuildId}, skipping", guildId);
            return false;
        }

        try
        {
            var result = await ExecuteActivitySessionInternalAsync(guildId, adjustFrequencyCallback, cancellationToken);

            // Cleanup unused semaphores occasionally
            if (DateTime.UtcNow.Minute % 10 == 0) // Every 10 minutes
            {
                CleanupUnusedSemaphores();
            }

            return result;
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

            // Check if initiative system is enabled globally
            if (!_initiativeOptions.Enabled)
            {
                _logger.LogDebug("Initiative system disabled globally, skipping execution for guild {GuildId}", guildId);
                return false;
            }

            // Check if LiveJob is enabled for this server
            if (!await _toggleService.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            {
                _logger.LogDebug("LiveJob disabled for guild {GuildId}, skipping execution", guildId);
                return false;
            }

            // Check initiative state (deep sleep, manual sleep, consecutive limits, etc.)
            var initiativeState = await _sleepService.GetInitiativeStateAsync(guildId);

            if (initiativeState.IsInDeepSleep)
            {
                _logger.LogDebug("Guild {GuildId} is in deep sleep, skipping initiative", guildId);
                adjustFrequencyCallback?.Invoke(0.05); // Very low frequency during deep sleep
                return false;
            }

            if (initiativeState.IsInManualSleep)
            {
                _logger.LogDebug("Guild {GuildId} is in manual sleep, skipping initiative", guildId);
                adjustFrequencyCallback?.Invoke(0.0);
                return false;
            }

            // Check cancellation token before continuing
            cancellationToken.ThrowIfCancellationRequested();

            // Get current activity level
            var currentActivity = _chatContextService.GetCurrentActivityLevel(guildId);

            // Get context messages
            var contextMessages = _chatContextService.GetContextMessages(guildId);

            // Check minimum context messages requirement
            if (contextMessages.Length < _initiativeOptions.Engagement.MinContextMessages)
            {
                _logger.LogDebug("Not enough context messages for guild {GuildId} ({Count}/{Min}), adjusting frequency",
                    guildId, contextMessages.Length, _initiativeOptions.Engagement.MinContextMessages);

                // Adjust frequency for low activity and return
                adjustFrequencyCallback?.Invoke(0.1);
                return false;
            }

            _logger.LogDebug("Guild {GuildId} activity: {Activity}, messages: {Count}, initiative state: waking={Waking}, consecutive={Consecutive}",
                guildId, currentActivity, contextMessages.Length, initiativeState.IsWakingUp, initiativeState.ConsecutiveInitiatives);

            // Adjust job frequency based on activity
            adjustFrequencyCallback?.Invoke(currentActivity);

            // Check for bot mentions in recent messages
            var recentMessages = contextMessages.Take(contextMessages.Length - 1).TakeLast(4).ToArray();
            var botMentioned = recentMessages.Any(msg =>
                msg.Contains("Amiquin", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("@Amiquin", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains($"<@{_discordClient.CurrentUser?.Id}>", StringComparison.OrdinalIgnoreCase));

            // Get initiative probability multiplier from sleep service (handles deep sleep, consecutive limits, timing, etc.)
            var initiativeProbabilityMultiplier = await _sleepService.GetInitiativeProbabilityMultiplierAsync(guildId);

            // If initiative is blocked (returns 0), skip unless bot was mentioned
            if (initiativeProbabilityMultiplier <= 0 && !botMentioned)
            {
                _logger.LogDebug("Initiative blocked for guild {GuildId} (multiplier: {Multiplier}), skipping",
                    guildId, initiativeProbabilityMultiplier);
                return false;
            }

            // Get engagement multiplier and cap it during high activity periods
            var rawEngagementMultiplier = _chatContextService.GetEngagementMultiplier(guildId);
            var engagementMultiplier = currentActivity switch
            {
                >= 2.0 => Math.Min(rawEngagementMultiplier, 1.2f), // Extremely high: cap at 1.2x
                >= 1.5 => Math.Min(rawEngagementMultiplier, 1.5f), // Very high: cap at 1.5x
                >= 1.0 => Math.Min(rawEngagementMultiplier, 2.0f), // High: cap at 2.0x
                _ => rawEngagementMultiplier                        // Normal: no cap
            };

            // Calculate engagement probability using initiative options
            var baseChance = CalculateBaseChance(currentActivity);

            // Apply activity-based multipliers from initiative options
            var activityThresholds = _initiativeOptions.Engagement.ActivityThresholds;
            double activityMultiplier;
            if (currentActivity <= activityThresholds.LowActivityThreshold)
            {
                activityMultiplier = activityThresholds.LowActivityMultiplier;
            }
            else if (currentActivity >= activityThresholds.HighActivityThreshold)
            {
                activityMultiplier = activityThresholds.HighActivityMultiplier;
            }
            else
            {
                // Linear interpolation between low and high thresholds
                activityMultiplier = 1.0;
            }

            // Combine all multipliers
            var adjustedChance = baseChance * engagementMultiplier * activityMultiplier * initiativeProbabilityMultiplier;

            // Force engagement for mentions
            if (botMentioned)
            {
                adjustedChance = 1.0; // 100% chance
                _logger.LogDebug("Bot mentioned in recent history for guild {GuildId}, forcing engagement", guildId);
            }
            else
            {
                // Apply max probability cap from initiative options
                adjustedChance = Math.Min(adjustedChance, _initiativeOptions.Engagement.MaxProbability);
            }

            // Random engagement check with human-like delay consideration
            // Use Random.Shared for thread-safe, properly seeded randomization
            var shouldEngage = Random.Shared.NextDouble() < adjustedChance;

            _logger.LogDebug("Guild {GuildId}: activity={Activity}, engagementMult={EngMult}, initiativeMult={InitMult}, chance={Chance}%, engage={Engage}",
                guildId, currentActivity, engagementMultiplier, initiativeProbabilityMultiplier, adjustedChance * 100, shouldEngage);

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

            // Add human-like delay before responding
            var delaySeconds = Random.Shared.Next(
                _initiativeOptions.Timing.MinResponseDelaySeconds,
                _initiativeOptions.Timing.MaxResponseDelaySeconds + 1);
            _logger.LogDebug("Adding human-like delay of {Delay} seconds before initiative action for guild {GuildId}",
                delaySeconds, guildId);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

            // Select and execute appropriate engagement action with retry mechanism
            var maxRetries = 3;
            for (int retryAttempt = 0; retryAttempt < maxRetries; retryAttempt++)
            {
                try
                {
                    var actionChoice = SelectEngagementAction(currentActivity, botMentioned);
                    _logger.LogDebug("Attempting action {Action} for guild {GuildName} (attempt {Attempt}/{MaxRetries})",
                        actionChoice, guild.Name, retryAttempt + 1, maxRetries);

                    var response = await ExecuteEngagementAction(_chatContextService, guildId, actionChoice);

                    if (!string.IsNullOrEmpty(response))
                    {
                        _logger.LogInformation("ActivitySession executed action {Action} for guild {GuildName}: success",
                            actionChoice, guild.Name);

                        // Record that an initiative action was taken
                        await _sleepService.RecordInitiativeActionAsync(guildId);

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
            <= 0.3 => 0.25,    // 25% base chance for low activity (no change)
            <= 0.5 => 0.30,    // 30% base chance for below normal (reduced from 35%)
            <= 1.0 => 0.20,    // 20% base chance for normal activity (reduced from 40%)
            <= 1.5 => 0.10,    // 10% base chance for high activity (reduced from 25%)
            <= 2.0 => 0.05,    // 5% base chance for very high activity (reduced from 15%)
            _ => 0.02           // 2% base chance for extremely high activity (reduced from 15%)
        };
    }

    /// <summary>
    /// Selects engagement action based on activity level and configured action weights
    /// </summary>
    private int SelectEngagementAction(double activityLevel, bool botMentioned = false)
    {
        // When bot is mentioned in context, 90% chance of adaptive response
        if (botMentioned)
        {
            return Random.Shared.NextDouble() < 0.9 ? 7 : 5; // 90% adaptive, 10% increase engagement
        }

        // Get weights from configuration
        var weights = _initiativeOptions.ActionWeights;

        // Adjust weights based on activity level
        var adjustedWeights = new Dictionary<int, float>
        {
            { 0, weights.StartTopic },        // StartTopic
            { 1, weights.AskQuestion },       // AskQuestion
            { 2, weights.ShareInteresting },  // ShareInteresting
            { 3, weights.ShareFunny },        // ShareFunny
            { 4, weights.ShareUseful },       // ShareUseful
            { 5, weights.IncreaseEngagement },// IncreaseEngagement
            { 6, weights.ShareOpinion },      // ShareOpinion
            { 7, weights.AdaptiveResponse }   // AdaptiveResponse
        };

        // Adjust weights based on activity level
        if (activityLevel >= 1.5)
        {
            // High activity: strongly favor adaptive responses, reduce topic starters
            adjustedWeights[7] *= 2.0f;  // Double adaptive weight
            adjustedWeights[0] *= 0.2f;  // Reduce topic starters
            adjustedWeights[2] *= 0.5f;  // Reduce sharing
            adjustedWeights[4] *= 0.3f;  // Reduce useful tips
        }
        else if (activityLevel >= 1.0)
        {
            // Moderate-high activity: favor adaptive
            adjustedWeights[7] *= 1.5f;
            adjustedWeights[0] *= 0.5f;
        }
        else if (activityLevel < 0.5)
        {
            // Low activity: favor topic starters and engagement boosters
            adjustedWeights[0] *= 1.5f;  // More topic starters
            adjustedWeights[5] *= 1.5f;  // More engagement
            adjustedWeights[1] *= 1.3f;  // More questions
            adjustedWeights[7] *= 0.7f;  // Less adaptive (nothing to adapt to)
        }

        // Calculate total weight
        var totalWeight = adjustedWeights.Values.Sum();

        // Random selection based on weights using thread-safe Random.Shared
        var roll = Random.Shared.NextDouble() * totalWeight;
        var cumulative = 0f;

        foreach (var kvp in adjustedWeights)
        {
            cumulative += kvp.Value;
            if (roll <= cumulative)
            {
                return kvp.Key;
            }
        }

        // Fallback to adaptive response
        return 7;
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

    /// <summary>
    /// Cleans up unused semaphores to prevent memory leaks
    /// </summary>
    private void CleanupUnusedSemaphores()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Clean up semaphores unused for 30 minutes
            var keysToRemove = new List<ulong>();

            foreach (var kvp in _lastSemaphoreUsage)
            {
                if (kvp.Value < cutoffTime)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_guildSemaphores.TryRemove(key, out var semaphore))
                {
                    semaphore.Dispose();
                    _lastSemaphoreUsage.TryRemove(key, out _);
                    _logger.LogDebug("Cleaned up unused semaphore for guild {GuildId}", key);
                }
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} unused semaphores", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during semaphore cleanup");
        }
    }
}