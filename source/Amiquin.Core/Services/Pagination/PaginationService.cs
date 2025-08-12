using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.ComponentHandler;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Amiquin.Core.Services.Pagination;

/// <summary>
/// Service for handling paginated embeds with Discord components
/// </summary>
public class PaginationService : IPaginationService
{
    private readonly ILogger<PaginationService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IComponentHandlerService _componentHandlerService;

    /// <summary>
    /// The component prefix used for pagination components.
    /// </summary>
    public const string ComponentPrefix = "page";

    public PaginationService(ILogger<PaginationService> logger, IServiceScopeFactory serviceScopeFactory, IComponentHandlerService componentHandlerService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _componentHandlerService = componentHandlerService;
        
        // Register this service as the handler for pagination components
        _componentHandlerService.RegisterHandler(ComponentPrefix, HandlePaginationInteractionAsync);
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

        using var scope = _serviceScopeFactory.CreateScope();
        var paginationRepository = scope.ServiceProvider.GetRequiredService<IPaginationSessionRepository>();
        await paginationRepository.CreateAsync(dbSession);

        var embed = CreateEmbedWithPageInfo(embeds[0], 1, embeds.Count);
        var component = CreateNavigationComponent(sessionId, 0, embeds.Count);

        _logger.LogDebug("Created pagination session {SessionId} for user {UserId} with {PageCount} pages",
            sessionId, userId, embeds.Count);

        return (embed, component);
    }

    public async Task<bool> HandleInteractionAsync(SocketMessageComponent component)
    {
        // This method is now handled through the component handler service
        // but we keep it for backward compatibility
        var context = _componentHandlerService.ParseCustomId(component.Data.CustomId);
        if (context?.Prefix != ComponentPrefix)
            return false;

        return await HandlePaginationInteractionAsync(component, context);
    }

    /// <summary>
    /// Handles pagination component interactions through the new component handler system.
    /// </summary>
    /// <param name="component">The component interaction.</param>
    /// <param name="context">The parsed component context.</param>
    /// <returns>True if the interaction was handled.</returns>
    private async Task<bool> HandlePaginationInteractionAsync(SocketMessageComponent component, ComponentContext context)
    {
        // Parse parameters: sessionId and action
        if (context.Parameters.Length < 2)
        {
            _logger.LogDebug("Invalid pagination parameters: expected sessionId and action");
            return false;
        }

        var sessionId = context.GetParameter(0);
        var action = context.GetParameter(1);

        if (sessionId == null || action == null)
        {
            _logger.LogDebug("Missing sessionId or action in pagination interaction");
            return false;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var paginationRepository = scope.ServiceProvider.GetRequiredService<IPaginationSessionRepository>();
        var session = await paginationRepository.GetByIdAsync(sessionId);

        _logger.LogInformation("User {UserId} interacted with pagination session {SessionId}", component.User.Id, sessionId);
        _logger.LogInformation("Pagination action: {Action}", action);
        _logger.LogInformation("Session details: {Session}", JsonSerializer.Serialize(session));

        if (session == null || session.IsExpired)
        {
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "This pagination session has expired.");
            return true;
        }

        // Note: Removed user authorization check as requested - buttons are not user-scoped

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
            return true;
        }

        // Update the session in the database
        session.UpdatePage(newPage);
        await paginationRepository.UpdateAsync(session);

        // Deserialize embeds from the database
        var embeds = await DeserializeEmbedsFromSession(session);
        var embed = CreateEmbedWithPageInfo(embeds[newPage], newPage + 1, session.TotalPages);
        var newComponent = CreateNavigationComponent(sessionId, newPage, session.TotalPages);

        await component.ModifyOriginalResponseAsync(properties =>
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
            .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "first"))
            .WithLabel("⏪")
            .WithStyle(ButtonStyle.Secondary)
            .WithDisabled(currentPage == 0));

        // Previous page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "prev"))
            .WithLabel("◀️")
            .WithStyle(ButtonStyle.Primary)
            .WithDisabled(currentPage == 0));

        // Next page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "next"))
            .WithLabel("▶️")
            .WithStyle(ButtonStyle.Primary)
            .WithDisabled(currentPage == totalPages - 1));

        // Last page button
        buttons.Add(new ButtonBuilder()
            .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "last"))
            .WithLabel("⏩")
            .WithStyle(ButtonStyle.Secondary)
            .WithDisabled(currentPage == totalPages - 1));

        var actionRow = new ActionRowBuilder();
        foreach (var button in buttons)
        {
            actionRow.AddComponent(button);
        }

        return new ComponentBuilder()
            .WithRows(new List<ActionRowBuilder> { actionRow })
            .Build();
    }

    private async Task<List<Embed>> DeserializeEmbedsFromSession(Core.Models.PaginationSession session)
    {
        var embedsData = JsonSerializer.Deserialize<JsonElement[]>(session.EmbedData);
        var embeds = new List<Embed>();

        if (embedsData == null) return embeds;

        foreach (var embedData in embedsData)
        {
            var builder = new EmbedBuilder();

            if (embedData.TryGetProperty("Title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            {
                var title = titleProp.GetString();
                if (!string.IsNullOrEmpty(title))
                    builder.WithTitle(title);
            }

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
            using var scope = _serviceScopeFactory.CreateScope();
            var paginationRepository = scope.ServiceProvider.GetRequiredService<IPaginationSessionRepository>();
            var cleanedCount = await paginationRepository.CleanupExpiredAsync();
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