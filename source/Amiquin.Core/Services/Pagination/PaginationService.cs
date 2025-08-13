using Amiquin.Core.IRepositories;
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

    public async Task<MessageComponent> CreatePaginatedMessageAsync(
        IReadOnlyList<PaginationPage> pages,
        ulong userId,
        TimeSpan? timeout = null)
    {
        if (pages.Count == 0)
            throw new ArgumentException("Must provide at least one page", nameof(pages));

        var sessionId = CreateSessionId(userId);
        var timeoutSpan = timeout ?? TimeSpan.FromMinutes(15);

        // Serialize the pages to JSON for database storage
        var pagesData = pages.Select(p => new
        {
            Title = p.Title,
            Content = p.Content,
            Color = p.Color?.RawValue,
            Timestamp = p.Timestamp?.ToString("O"),
            ThumbnailUrl = p.ThumbnailUrl,
            ImageUrl = p.ImageUrl,
            Sections = p.Sections.Select(s => new { s.Title, s.Content, s.IconUrl, s.IsInline }).ToArray()
        }).ToArray();

        var pagesJson = JsonSerializer.Serialize(pagesData);

        var dbSession = Core.Models.PaginationSession.CreateSession(
            userId: userId,
            guildId: null,
            channelId: 0,
            messageId: 0,
            embedData: pagesJson,
            totalPages: pages.Count,
            timeout: timeoutSpan,
            contentType: "componentsv2"
        );
        dbSession.Id = sessionId;

        using var scope = _serviceScopeFactory.CreateScope();
        var paginationRepository = scope.ServiceProvider.GetRequiredService<IPaginationSessionRepository>();
        await paginationRepository.CreateAsync(dbSession);

        var component = CreatePaginatedComponentsV2(pages[0], sessionId, 0, pages.Count);

        _logger.LogDebug("Created pagination session {SessionId} for user {UserId} with {PageCount} pages",
            sessionId, userId, pages.Count);

        return component;
    }

    public async Task<MessageComponent> CreatePaginatedMessageFromEmbedsAsync(
        IReadOnlyList<Embed> embeds,
        ulong userId,
        TimeSpan? timeout = null)
    {
        var pages = embeds.Select(PaginationPage.FromEmbed).ToList();
        return await CreatePaginatedMessageAsync(pages, userId, timeout);
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

        // Deserialize pages from the database
        var pages = await DeserializePagesFromSession(session);
        var newComponent = CreatePaginatedComponentsV2(pages[newPage], sessionId, newPage, session.TotalPages);

        await component.ModifyOriginalResponseAsync(properties =>
        {
            properties.Components = newComponent;
            properties.Flags = MessageFlags.ComponentsV2;
            properties.Embed = null;
        });

        _logger.LogDebug("Updated pagination session {SessionId} from page {OldPage} to page {NewPage}",
            sessionId, oldPage + 1, newPage + 1);

        return true;
    }

    public string CreateSessionId(ulong userId)
    {
        return $"{userId}_{DateTime.UtcNow.Ticks}";
    }

    private MessageComponent CreatePaginatedComponentsV2(PaginationPage page, string sessionId, int currentPageIndex, int totalPages)
    {
        var componentsBuilder = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                // Add title if present
                if (!string.IsNullOrWhiteSpace(page.Title))
                {
                    container.WithTextDisplay($"# {page.Title}");
                }

                // Add main content
                if (!string.IsNullOrWhiteSpace(page.Content))
                {
                    container.WithTextDisplay(page.Content);
                }

                // Add sections
                foreach (var section in page.Sections)
                {
                    var sectionContent = !string.IsNullOrWhiteSpace(section.Title)
                        ? $"**{section.Title}**\n{section.Content}"
                        : section.Content;

                    container.WithTextDisplay(sectionContent);
                }

                // Add image links if present
                if (!string.IsNullOrWhiteSpace(page.ThumbnailUrl))
                {
                    container.WithTextDisplay($"**Thumbnail:** [View]({page.ThumbnailUrl})");
                }

                if (!string.IsNullOrWhiteSpace(page.ImageUrl))
                {
                    container.WithTextDisplay($"**Image:** [View]({page.ImageUrl})");
                }

                // Add pagination info and navigation buttons
                container.WithTextDisplay($"*Page {currentPageIndex + 1} of {totalPages}*");

                // Create navigation buttons
                var navSection = new SectionBuilder();

                // First page button
                navSection.WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "first"))
                    .WithLabel("⏪ First")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithDisabled(currentPageIndex == 0));

                // Previous page button
                navSection.WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "prev"))
                    .WithLabel("◀️ Previous")
                    .WithStyle(ButtonStyle.Primary)
                    .WithDisabled(currentPageIndex == 0));

                // Next page button
                navSection.WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "next"))
                    .WithLabel("Next ▶️")
                    .WithStyle(ButtonStyle.Primary)
                    .WithDisabled(currentPageIndex == totalPages - 1));

                // Last page button
                navSection.WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandlerService.GenerateCustomId(ComponentPrefix, sessionId, "last"))
                    .WithLabel("Last ⏩")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithDisabled(currentPageIndex == totalPages - 1));

                container.AddComponent(navSection);
            });

        return componentsBuilder.Build();
    }

    // Navigation component creation is now integrated into CreatePaginatedComponentsV2

    private async Task<List<PaginationPage>> DeserializePagesFromSession(Core.Models.PaginationSession session)
    {
        var pagesData = JsonSerializer.Deserialize<JsonElement[]>(session.EmbedData);
        var pages = new List<PaginationPage>();

        if (pagesData == null) return pages;

        foreach (var pageData in pagesData)
        {
            var page = new PaginationPage();

            if (pageData.TryGetProperty("Title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                page.Title = titleProp.GetString();

            if (pageData.TryGetProperty("Content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                page.Content = contentProp.GetString() ?? string.Empty;
            else if (pageData.TryGetProperty("Description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                page.Content = descProp.GetString() ?? string.Empty; // Backward compatibility

            if (pageData.TryGetProperty("Color", out var colorProp) && colorProp.ValueKind == JsonValueKind.Number)
                page.Color = new Color(colorProp.GetUInt32());

            if (pageData.TryGetProperty("Timestamp", out var timestampProp) && timestampProp.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(timestampProp.GetString(), out var timestamp))
                    page.Timestamp = timestamp;
            }

            if (pageData.TryGetProperty("ThumbnailUrl", out var thumbProp) && thumbProp.ValueKind == JsonValueKind.String)
                page.ThumbnailUrl = thumbProp.GetString();

            if (pageData.TryGetProperty("ImageUrl", out var imageProp) && imageProp.ValueKind == JsonValueKind.String)
                page.ImageUrl = imageProp.GetString();

            // Handle sections (new format)
            if (pageData.TryGetProperty("Sections", out var sectionsProp) && sectionsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in sectionsProp.EnumerateArray())
                {
                    var pageSection = new PageSection();
                    if (section.TryGetProperty("Title", out var sectTitleProp))
                        pageSection.Title = sectTitleProp.GetString() ?? string.Empty;
                    if (section.TryGetProperty("Content", out var sectContentProp))
                        pageSection.Content = sectContentProp.GetString() ?? string.Empty;
                    if (section.TryGetProperty("IconUrl", out var sectIconProp))
                        pageSection.IconUrl = sectIconProp.GetString();
                    if (section.TryGetProperty("IsInline", out var sectInlineProp))
                        pageSection.IsInline = sectInlineProp.GetBoolean();

                    page.Sections.Add(pageSection);
                }
            }
            // Handle old embed format for backward compatibility
            else
            {
                // Convert Author to section
                if (pageData.TryGetProperty("Author", out var authorProp) && authorProp.ValueKind == JsonValueKind.Object)
                {
                    var name = authorProp.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
                    var iconUrl = authorProp.TryGetProperty("IconUrl", out var iconProp) ? iconProp.GetString() : null;
                    if (name != null)
                    {
                        page.Sections.Add(new PageSection
                        {
                            Title = "Author",
                            Content = name,
                            IconUrl = iconUrl
                        });
                    }
                }

                // Convert Fields to sections
                if (pageData.TryGetProperty("Fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var field in fieldsProp.EnumerateArray())
                    {
                        var name = field.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
                        var value = field.TryGetProperty("Value", out var valueProp) ? valueProp.GetString() : null;
                        var inline = field.TryGetProperty("Inline", out var inlineProp) && inlineProp.GetBoolean();

                        if (name != null && value != null)
                        {
                            page.Sections.Add(new PageSection
                            {
                                Title = name,
                                Content = value,
                                IsInline = inline
                            });
                        }
                    }
                }
            }

            pages.Add(page);
        }

        return pages;
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