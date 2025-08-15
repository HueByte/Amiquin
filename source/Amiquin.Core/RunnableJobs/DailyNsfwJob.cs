using Amiquin.Core.Abstraction;
using Amiquin.Core.Abstractions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Services.Toggle;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.RunnableJobs;

/// <summary>
/// Daily NSFW job that sends a gallery of NSFW images to servers with the feature enabled.
/// Runs once per day at a configured time.
/// </summary>
public class DailyNsfwJob : IRunnableJob
{
    private readonly ILogger<DailyNsfwJob> _logger;
    private readonly IDiscordClientWrapper _clientWrapper;
    private DateTime _lastRunDate = DateTime.MinValue;

    public DailyNsfwJob(ILogger<DailyNsfwJob> logger, IDiscordClientWrapper clientWrapper)
    {
        _logger = logger;
        _clientWrapper = clientWrapper;
    }

    // Run every 30 minutes to check if it's time for daily post
    public int FrequencyInSeconds { get; set; } = 1800;

    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Check if we should run (once per day at 12:00 UTC)
            if (_lastRunDate.Date == now.Date)
            {
                _logger.LogDebug("Daily NSFW job already ran today, skipping");
                return;
            }

            // Only run at specific hour (configurable, default 12:00 UTC)
            if (now.Hour != 12)
            {
                _logger.LogDebug("Not the scheduled hour for Daily NSFW job (current: {Hour}, target: 12)", now.Hour);
                return;
            }

            _logger.LogInformation("Starting Daily NSFW job execution");
            _lastRunDate = now;

            using var scope = serviceScopeFactory.CreateScope();
            var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();
            var serverRepository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
            var nsfwApiService = scope.ServiceProvider.GetRequiredService<INsfwApiService>();

            // Get all servers with EnableDailyNSFW toggle enabled
            var allServers = serverRepository.AsQueryable()
                .Where(s => s.IsActive && s.NsfwChannelId != null)
                .Select(s => new { s.Id, s.NsfwChannelId })
                .ToList();

            var eligibleServers = new List<(ulong ServerId, ulong ChannelId)>();

            foreach (var server in allServers)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // Check if both EnableNSFW and EnableDailyNSFW are enabled
                    var nsfwEnabled = await toggleService.IsEnabledAsync(server.Id, Constants.ToggleNames.EnableNSFW);
                    var dailyNsfwEnabled = await toggleService.IsEnabledAsync(server.Id, Constants.ToggleNames.EnableDailyNSFW);

                    if (nsfwEnabled && dailyNsfwEnabled && server.NsfwChannelId.HasValue)
                    {
                        eligibleServers.Add((server.Id, server.NsfwChannelId.Value));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking toggles for server {ServerId}", server.Id);
                }
            }

            _logger.LogInformation("Found {Count} servers eligible for daily NSFW content", eligibleServers.Count);

            if (eligibleServers.Count == 0)
            {
                return;
            }

            // Fetch images once for all servers
            var images = await nsfwApiService.GetDailyNsfwImagesAsync(5, 5);
            if (images.Count == 0)
            {
                _logger.LogWarning("No NSFW images fetched, skipping daily post");
                return;
            }

            // Process each server
            var successCount = 0;
            foreach (var (serverId, channelId) in eligibleServers)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var guild = _clientWrapper.GetGuild(serverId);
                    if (guild == null)
                    {
                        _logger.LogWarning("Guild {ServerId} not found in client cache", serverId);
                        continue;
                    }

                    var channel = guild.GetTextChannel(channelId);
                    if (channel == null)
                    {
                        _logger.LogWarning("NSFW channel {ChannelId} not found in guild {ServerId}", channelId, serverId);
                        continue;
                    }

                    // Check if channel is actually NSFW
                    if (!channel.IsNsfw)
                    {
                        _logger.LogWarning("Channel {ChannelId} in guild {ServerId} is not marked as NSFW, skipping", channelId, serverId);
                        continue;
                    }

                    // Build the gallery components
                    var components = BuildNsfwGalleryComponents(images);

                    await channel.SendMessageAsync(components: components, flags: MessageFlags.ComponentsV2);
                    successCount++;

                    _logger.LogInformation("Successfully sent daily NSFW gallery to guild {ServerId}", serverId);

                    // Small delay between servers to avoid rate limits
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending daily NSFW content to server {ServerId}", serverId);
                }
            }

            _logger.LogInformation("Daily NSFW job completed. Sent to {Success}/{Total} servers",
                successCount, eligibleServers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Daily NSFW job execution");
        }
    }

    private MessageComponent BuildNsfwGalleryComponents(List<NsfwImage> images)
    {
        var componentsBuilder = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                // Header
                container.WithTextDisplay("# 🔞 Daily NSFW Gallery");
                container.WithSeparator(spacing: SeparatorSpacingSize.Large);

                // Description
                container.WithTextDisplay($"Today's curated collection of **{images.Count}** images from various sources");
                container.WithSeparator();

                // Source information
                var sources = images.Select(i => i.Source).Distinct().ToList();
                var artists = images.Where(i => !string.IsNullOrWhiteSpace(i.Artist))
                    .Select(i => i.Artist!)
                    .Distinct()
                    .Take(5)
                    .ToList();

                var sourceText = $"**Sources:** {string.Join(", ", sources)}";
                if (artists.Any())
                {
                    sourceText += $"\n**Featured Artists:** {string.Join(", ", artists)}";
                    if (artists.Count == 5)
                    {
                        sourceText += " and more...";
                    }
                }

                container.WithTextDisplay(sourceText);
                container.WithSeparator();

                // Add media gallery with all images
                if (images.Any())
                {
                    var galleryBuilder = new MediaGalleryBuilder();
                    foreach (var img in images.Take(10)) // Discord limits gallery items
                    {
                        galleryBuilder.AddItem(img.Url);
                    }
                    container.WithMediaGallery(galleryBuilder);
                }

                // Footer
                container.WithTextDisplay("*Daily NSFW Gallery • Enjoy responsibly*");
            });

        // Add action buttons for interaction
        componentsBuilder.WithActionRow([
            new ButtonBuilder()
                .WithLabel("🔄 Refresh")
                .WithCustomId("nsfw_daily_refresh")
                .WithStyle(ButtonStyle.Primary),
            new ButtonBuilder()
                .WithLabel("🎲 Random")
                .WithCustomId("nsfw_daily_random")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        return componentsBuilder.Build();
    }
}