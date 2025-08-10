using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Job.Models;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Toggle;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.RunnableJobs;

public class LiveJob : IRunnableJob
{
    private readonly ILogger<LiveJob> _logger;
    private readonly IChatContextService _chatContextService;
    private readonly IJobService _jobService;
    private bool _isInitialized = false;

    public LiveJob(ILogger<LiveJob> logger, IChatContextService chatContextService, IJobService jobService)
    {
        _logger = logger;
        _chatContextService = chatContextService;
        _jobService = jobService;
    }

    public int FrequencyInSeconds { get; set; } = 30;

    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            _logger.LogInformation("LiveJob is already initialized, skipping initialization.");
            return;
        }

        var scope = serviceScopeFactory.CreateScope();
        var serverRepository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
        var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();

        var serverIds = serverRepository.AsQueryable().Select(s => s.Id).ToList();
        foreach (var serverId in serverIds)
        {
            var serverToggles = await toggleService.GetTogglesByServerId(serverId);
            if (!serverToggles.Any(t => t.Name == Constants.ToggleNames.EnableLiveJob && t.IsEnabled))
            {
                continue;
            }

            _logger.LogInformation("Running live job for server {ServerId} with toggles enabled", serverId);
            AmiquinJob job = new()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Live Job",
                Description = "Dynamically makes Amiquin take initiative in the server and interact with users.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                GuildId = serverId,
                Task = (serviceFactory, cancellationToken) => ExecuteLiveJobAsync(serviceFactory, new TrackedAmiquinJob { GuildId = serverId, Id = Guid.NewGuid().ToString() }, cancellationToken),
                Interval = TimeSpan.FromSeconds(FrequencyInSeconds)
            };

            _jobService.CreateDynamicJob(job);
        }

        _isInitialized = true;
    }

    Task ExecuteLiveJobAsync(IServiceScopeFactory serviceScopeFactory, TrackedAmiquinJob job, CancellationToken cancellationToken)
    {
        var scope = serviceScopeFactory.CreateScope();
        var chatContextService = scope.ServiceProvider.GetRequiredService<IChatContextService>();
        var chatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();
        var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();

        return ExecuteLiveJobInternalAsync(serviceScopeFactory, job, chatContextService, chatService, discordClient, cancellationToken);
    }

    private async Task ExecuteLiveJobInternalAsync(
        IServiceScopeFactory serviceScopeFactory,
        TrackedAmiquinJob job,
        IChatContextService chatContextService,
        IPersonaChatService chatService,
        DiscordShardedClient discordClient,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing live job for guild {GuildId}", job.GuildId);

            // Get context messages for this server
            var contextMessages = chatContextService.GetContextMessages(job.GuildId);

            if (contextMessages.Length == 0)
            {
                _logger.LogDebug("No context messages found for guild {GuildId}, skipping live job execution", job.GuildId);
                return;
            }

            _logger.LogInformation("Found {MessageCount} context messages for guild {GuildId}", contextMessages.Length, job.GuildId);

            // Get engagement multiplier (higher after mentions)
            var engagementMultiplier = chatContextService.GetEngagementMultiplier(job.GuildId);
            var baseChance = 0.3; // 30% base chance
            var adjustedChance = Math.Min(baseChance * engagementMultiplier, 0.8); // Cap at 80%

            // Random chance to engage (adjusted by engagement multiplier)
            var random = new Random();
            var shouldEngage = random.NextDouble() < adjustedChance;

            _logger.LogDebug("Engagement check for guild {GuildId}: multiplier={Multiplier}, chance={Chance}%, engage={ShouldEngage}",
                job.GuildId, engagementMultiplier, adjustedChance * 100, shouldEngage);

            if (!shouldEngage)
            {
                _logger.LogDebug("Random engagement check failed for guild {GuildId}", job.GuildId);
                return;
            }

            // Find the guild and a suitable channel to send message to
            var guild = discordClient.GetGuild(job.GuildId);
            if (guild == null)
            {
                _logger.LogWarning("Could not find guild {GuildId} for live job", job.GuildId);
                return;
            }

            // Find a suitable text channel (prefer general, random, or similar channels)
            var suitableChannel = FindSuitableChannel(guild);
            if (suitableChannel == null)
            {
                _logger.LogWarning("Could not find suitable channel in guild {GuildName} ({GuildId})", guild.Name, job.GuildId);
                return;
            }

            // Use the enhanced context-aware messaging with specific engagement actions
            string? response = null;

            // Choose a random engagement action based on context and randomness
            var actionChoice = random.Next(6);
            switch (actionChoice)
            {
                case 0:
                    response = await chatContextService.StartTopicAsync(job.GuildId);
                    _logger.LogDebug("Attempted StartTopicAsync for guild {GuildId}", job.GuildId);
                    break;
                case 1:
                    response = await chatContextService.AskQuestionAsync(job.GuildId);
                    _logger.LogDebug("Attempted AskQuestionAsync for guild {GuildId}", job.GuildId);
                    break;
                case 2:
                    response = await chatContextService.ShareInterestingContentAsync(job.GuildId);
                    _logger.LogDebug("Attempted ShareInterestingContentAsync for guild {GuildId}", job.GuildId);
                    break;
                case 3:
                    response = await chatContextService.ShareFunnyContentAsync(job.GuildId);
                    _logger.LogDebug("Attempted ShareFunnyContentAsync for guild {GuildId}", job.GuildId);
                    break;
                case 4:
                    response = await chatContextService.ShareUsefulContentAsync(job.GuildId);
                    _logger.LogDebug("Attempted ShareUsefulContentAsync for guild {GuildId}", job.GuildId);
                    break;
                case 5:
                    response = await chatContextService.IncreaseEngagementAsync(job.GuildId);
                    _logger.LogDebug("Attempted IncreaseEngagementAsync for guild {GuildId}", job.GuildId);
                    break;
            }

            if (!string.IsNullOrEmpty(response))
            {
                _logger.LogInformation("Successfully sent live engagement message (action {ActionType}) to guild {GuildName}",
                    actionChoice, guild.Name);
            }
            else
            {
                _logger.LogWarning("Failed to generate engagement content for guild {GuildId} using action {ActionType}",
                    job.GuildId, actionChoice);

                // Fallback to context-aware messaging if specific actions fail
                response = await chatContextService.SendContextAwareMessage(guild, suitableChannel);

                if (!string.IsNullOrEmpty(response))
                {
                    _logger.LogInformation("Fallback context-aware message sent successfully to guild {GuildName}", guild.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing live job for guild {GuildId}", job.GuildId);
        }
    }

    private static ISocketMessageChannel? FindSuitableChannel(SocketGuild guild)
    {
        // Priority order: general, random, chat, main, lounge
        var preferredNames = new[] { "general", "random", "chat", "main", "lounge", "discussion" };

        foreach (var name in preferredNames)
        {
            var channel = guild.TextChannels.FirstOrDefault(c =>
                c.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
                guild.CurrentUser.GetPermissions(c).SendMessages);
            if (channel != null)
                return channel;
        }

        // Fallback to any channel the bot can send messages to
        return guild.TextChannels.FirstOrDefault(c => guild.CurrentUser.GetPermissions(c).SendMessages);
    }

    private static string GenerateEngagementPrompt(string contextSummary)
    {
        var engagementTypes = new[]
        {
            $"Based on the recent conversation: '{contextSummary}', continue the discussion with an interesting perspective or question.",
            $"The chat mentions: '{contextSummary}'. Share a related thought, tip, or interesting fact that adds value to the conversation.",
            $"I noticed the conversation about: '{contextSummary}'. What's your take on this topic? Ask a thought-provoking question.",
            $"Following up on: '{contextSummary}'. Share something engaging or ask what others think about a related aspect.",
            "Start an interesting conversation topic that would engage the community members.",
            "Share an interesting fact, tip, or thought that might spark discussion.",
            "Ask a fun, engaging question that the community might enjoy discussing."
        };

        var random = new Random();
        var selectedPrompt = string.IsNullOrWhiteSpace(contextSummary)
            ? engagementTypes[^3..][random.Next(3)] // Use last 3 prompts if no context
            : engagementTypes[random.Next(Math.Min(4, engagementTypes.Length))]; // Use context-based prompts

        return $"{selectedPrompt} Keep it natural, friendly, and community-focused. Don't be overly promotional or artificial.";
    }
}