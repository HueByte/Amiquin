using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Pagination;

/// <summary>
/// Service for handling paginated embeds with Discord components
/// </summary>
public class PaginationService : IPaginationService
{
    private readonly ILogger<PaginationService> _logger;
    private readonly IPaginationSessionRepository _paginationRepository;

    public PaginationService(ILogger<PaginationService> logger, IPaginationSessionRepository paginationRepository)
    {
        _logger = logger;
        _paginationRepository = paginationRepository;
    }

    public async Task<(Embed Embed, MessageComponent Component)> CreatePaginatedMessageAsync(
        IReadOnlyList<Embed> embeds, 
        ulong userId, 
        TimeSpan? timeout = null)
    {
        if (embeds.Count == 0)
            throw new ArgumentException("Must provide at least one embed", nameof(embeds));

        var sessionId = CreateSessionId(userId);
        var timeoutSpan = timeout ?? TimeSpan.FromMinutes(15); // Extended timeout for database persistence

        // Serialize the embeds to JSON for database storage
        var embedsData = embeds.Select(e => new
        {
            Title = e.Title,
            Description = e.Description,
            Color = e.Color?.RawValue,
            Timestamp = e.Timestamp?.ToString("O"),
            ThumbnailUrl = e.Thumbnail?.Url,
            ImageUrl = e.Image?.Url,
            Author = e.Author?.Name != null ? new { Name = e.Author.Value.Name, IconUrl = e.Author.Value.IconUrl, Url = e.Author.Value.Url } : null,
            Footer = e.Footer?.Text != null ? new { Text = e.Footer.Value.Text, IconUrl = e.Footer.Value.IconUrl } : null,
            Fields = e.Fields.Select(f => new { Name = f.Name, Value = f.Value, Inline = f.Inline }).ToArray()
        }).ToArray();

        var embedJson = JsonSerializer.Serialize(embedsData);

        // Note: We'll need to update this with message/channel info after the Discord message is sent
        // For now, we'll create with placeholder values and update later if needed
        var dbSession = Core.Models.PaginationSession.CreateSession(
            userId: userId,
            guildId: null, // Will be updated if needed
            channelId: 0,  // Will be updated after message is sent
            messageId: 0,  // Will be updated after message is sent
            embedData: embedJson,
            totalPages: embeds.Count,
            timeout: timeoutSpan,
            contentType: "debug"
        );
        dbSession.Id = sessionId;

        await _paginationRepository.CreateAsync(dbSession);

        var embed = CreateEmbedWithPageInfo(embeds[0], 1, embeds.Count);
        var component = CreateNavigationComponent(sessionId, 0, embeds.Count);

        _logger.LogDebug("Created pagination session {SessionId} for user {UserId} with {PageCount} pages", 
            sessionId, userId, embeds.Count);

        return (embed, component);
    }

    public async Task<bool> HandleInteractionAsync(SocketMessageComponent component)
    {
        var customId = component.Data.CustomId;
        
        // Check if this is a pagination interaction
        if (!customId.StartsWith("page_"))
            return false;

        var parts = customId.Split('_');
        if (parts.Length < 3)
            return false;

        var sessionId = parts[1];
        var action = parts[2];

        var session = await _paginationRepository.GetByIdAsync(sessionId);
        if (session == null || session.IsExpired)
        {
            await component.RespondAsync("This pagination session has expired.", ephemeral: true);
            return true;
        }

        // Check if the user is authorized to interact with this pagination
        if (component.User.Id != session.UserId)
        {
            await component.RespondAsync("You are not authorized to interact with this pagination.", ephemeral: true);
            return true;
        }

        var oldPage = session.CurrentPage;
        var newPage = action switch
        {
            "first" => 0,
            "prev" => Math.Max(0, session.CurrentPage - 1),
            "next" => Math.Min(session.TotalPages - 1, session.CurrentPage + 1),
            "last" => session.TotalPages - 1,
            _ => session.CurrentPage
        };

        if (newPage == oldPage)
        {
            await component.DeferAsync();
            return true;
        }

        // Update the session in the database
        session.UpdatePage(newPage);
        await _paginationRepository.UpdateAsync(session);

        // Deserialize embeds from the database
        var embeds = await DeserializeEmbedsFromSession(session);
        var embed = CreateEmbedWithPageInfo(embeds[newPage], newPage + 1, session.TotalPages);
        var newComponent = CreateNavigationComponent(sessionId, newPage, session.TotalPages);

        await component.UpdateAsync(properties =>
        {
            properties.Embed = embed;
            properties.Components = newComponent;
        });

        _logger.LogDebug("Updated pagination session {SessionId} from page {OldPage} to page {NewPage}", 
            sessionId, oldPage + 1, newPage + 1);

        return true;
    }

    public string CreateSessionId(ulong userId)
    {
        return $"{userId}_{DateTime.UtcNow.Ticks}";
    }

    private Embed CreateEmbedWithPageInfo(Embed originalEmbed, int currentPage, int totalPages)
    {
        var builder = new EmbedBuilder()
            .WithTitle(originalEmbed.Title)
            .WithDescription(originalEmbed.Description)
            .WithColor(originalEmbed.Color ?? Color.Default)
            .WithTimestamp(originalEmbed.Timestamp ?? DateTimeOffset.UtcNow)
            .WithFooter($"Page {currentPage}/{totalPages} • {originalEmbed.Footer?.Text ?? ""}");

        if (originalEmbed.Author.HasValue)
            builder.WithAuthor(originalEmbed.Author.Value.Name, originalEmbed.Author.Value.IconUrl, originalEmbed.Author.Value.Url);

        if (!string.IsNullOrEmpty(originalEmbed.Thumbnail?.Url))
            builder.WithThumbnailUrl(originalEmbed.Thumbnail.Value.Url);

        if (!string.IsNullOrEmpty(originalEmbed.Image?.Url))
            builder.WithImageUrl(originalEmbed.Image.Value.Url);

        foreach (var field in originalEmbed.Fields)
        {
            builder.AddField(field.Name, field.Value, field.Inline);
        }

        return builder.Build();
    }

    private MessageComponent CreateNavigationComponent(string sessionId, int currentPage, int totalPages)
    {
        var buttons = new List<ButtonBuilder>();

        // First page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId($"page_{sessionId}_first")
            .WithLabel("⏪")
            .WithStyle(ButtonStyle.Secondary)
            .WithDisabled(currentPage == 0));

        // Previous page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId($"page_{sessionId}_prev")
            .WithLabel("◀️")
            .WithStyle(ButtonStyle.Primary)
            .WithDisabled(currentPage == 0));

        // Next page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId($"page_{sessionId}_next")
            .WithLabel("▶️")
            .WithStyle(ButtonStyle.Primary)
            .WithDisabled(currentPage == totalPages - 1));

        // Last page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId($"page_{sessionId}_last")
            .WithLabel("⏩")
            .WithStyle(ButtonStyle.Secondary)
            .WithDisabled(currentPage == totalPages - 1));

        var actionRow = new ActionRowBuilder();
        foreach (var button in buttons)
        {
            actionRow.AddComponent(button.Build());
        }

        return new ComponentBuilder()
            .WithRows(new List<ActionRowBuilder> { actionRow })
            .Build();
    }

    private async Task<List<Embed>> DeserializeEmbedsFromSession(Core.Models.PaginationSession session)
    {
        var embedsData = JsonSerializer.Deserialize<JsonElement[]>(session.EmbedData);
        var embeds = new List<Embed>();

        foreach (var embedData in embedsData)
        {
            var builder = new EmbedBuilder();

            if (embedData.TryGetProperty("Title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                builder.WithTitle(titleProp.GetString());

            if (embedData.TryGetProperty("Description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                builder.WithDescription(descProp.GetString());

            if (embedData.TryGetProperty("Color", out var colorProp) && colorProp.ValueKind == JsonValueKind.Number)
                builder.WithColor(new Color(colorProp.GetUInt32()));

            if (embedData.TryGetProperty("Timestamp", out var timestampProp) && timestampProp.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(timestampProp.GetString(), out var timestamp))
                    builder.WithTimestamp(timestamp);
            }

            if (embedData.TryGetProperty("ThumbnailUrl", out var thumbProp) && thumbProp.ValueKind == JsonValueKind.String)
                builder.WithThumbnailUrl(thumbProp.GetString());

            if (embedData.TryGetProperty("ImageUrl", out var imageProp) && imageProp.ValueKind == JsonValueKind.String)
                builder.WithImageUrl(imageProp.GetString());

            if (embedData.TryGetProperty("Author", out var authorProp) && authorProp.ValueKind == JsonValueKind.Object)
            {
                var name = authorProp.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
                var iconUrl = authorProp.TryGetProperty("IconUrl", out var iconProp) ? iconProp.GetString() : null;
                var url = authorProp.TryGetProperty("Url", out var urlProp) ? urlProp.GetString() : null;
                if (name != null)
                    builder.WithAuthor(name, iconUrl, url);
            }

            if (embedData.TryGetProperty("Footer", out var footerProp) && footerProp.ValueKind == JsonValueKind.Object)
            {
                var text = footerProp.TryGetProperty("Text", out var textProp) ? textProp.GetString() : null;
                var iconUrl = footerProp.TryGetProperty("IconUrl", out var iconProp) ? iconProp.GetString() : null;
                if (text != null)
                    builder.WithFooter(text, iconUrl);
            }

            if (embedData.TryGetProperty("Fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var field in fieldsProp.EnumerateArray())
                {
                    var name = field.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
                    var value = field.TryGetProperty("Value", out var valueProp) ? valueProp.GetString() : null;
                    var inline = field.TryGetProperty("Inline", out var inlineProp) && inlineProp.GetBoolean();

                    if (name != null && value != null)
                        builder.AddField(name, value, inline);
                }
            }

            embeds.Add(builder.Build());
        }

        return embeds;
    }

    /// <summary>
    /// Cleanup expired pagination sessions from the database
    /// </summary>
    public async Task CleanupExpiredSessionsAsync()
    {
        try
        {
            var cleanedCount = await _paginationRepository.CleanupExpiredAsync();
            if (cleanedCount > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired pagination sessions", cleanedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired pagination sessions");
        }
    }
}