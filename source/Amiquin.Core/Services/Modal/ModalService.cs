using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Modal;

/// <summary>
/// Implementation of the modal service with registry-based routing.
/// </summary>
public class ModalService : IModalService
{
    private readonly ILogger<ModalService> _logger;
    private readonly ConcurrentDictionary<string, Func<SocketModal, ModalContext, Task<bool>>> _handlers = new();

    /// <summary>
    /// The separator used in custom IDs to separate prefix and parameters.
    /// </summary>
    public const string CustomIdSeparator = ":";

    public ModalService(ILogger<ModalService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleModalSubmissionAsync(SocketModal modal)
    {
        var context = ParseCustomId(modal.Data.CustomId);
        if (context == null)
        {
            _logger.LogDebug("Could not parse modal custom ID: {CustomId}", modal.Data.CustomId);
            return false;
        }

        if (!_handlers.TryGetValue(context.Prefix, out var handler))
        {
            _logger.LogDebug("No handler registered for modal prefix: {Prefix}", context.Prefix);
            return false;
        }

        try
        {
            _logger.LogDebug("Handling modal submission with prefix {Prefix} for user {UserId}",
                context.Prefix, modal.User.Id);

            var handled = await handler(modal, context);

            if (handled)
            {
                _logger.LogDebug("Modal submission {CustomId} handled successfully", modal.Data.CustomId);
            }

            return handled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modal submission {CustomId} with prefix {Prefix}",
                modal.Data.CustomId, context.Prefix);
            return false;
        }
    }

    /// <inheritdoc/>
    public void RegisterHandler(string prefix, Func<SocketModal, ModalContext, Task<bool>> handler)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace", nameof(prefix));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (!_handlers.TryAdd(prefix, handler))
        {
            _logger.LogWarning("Handler for modal prefix {Prefix} was already registered", prefix);
        }
        else
        {
            _logger.LogDebug("Registered modal handler for prefix: {Prefix}", prefix);
        }
    }

    /// <inheritdoc/>
    public ModalBuilder CreateModal(string prefix, string title, params string[] parameters)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace", nameof(prefix));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or whitespace", nameof(title));

        var customId = GenerateCustomId(prefix, parameters);

        return new ModalBuilder()
            .WithTitle(title)
            .WithCustomId(customId);
    }

    /// <inheritdoc/>
    public string GenerateCustomId(string prefix, params string[] parameters)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace", nameof(prefix));

        // Validate that parameters don't contain the separator
        if (parameters.Any(p => p?.Contains(CustomIdSeparator) == true))
            throw new ArgumentException($"Parameters cannot contain the separator '{CustomIdSeparator}'");

        var parts = new List<string> { prefix };
        parts.AddRange(parameters ?? Array.Empty<string>());

        var customId = string.Join(CustomIdSeparator, parts);

        // Discord custom IDs have a 100 character limit
        if (customId.Length > 100)
        {
            _logger.LogWarning("Generated modal custom ID exceeds Discord's 100 character limit: {Length} chars", customId.Length);
        }

        return customId;
    }

    /// <inheritdoc/>
    public ModalContext? ParseCustomId(string customId)
    {
        if (string.IsNullOrWhiteSpace(customId))
            return null;

        var parts = customId.Split(CustomIdSeparator, StringSplitOptions.None);
        if (parts.Length < 1)
            return null;

        var prefix = parts[0];
        var parameters = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        return new ModalContext(prefix, parameters, customId);
    }

    /// <inheritdoc/>
    public string? GetComponentValue(SocketModal modal, string customId)
    {
        var component = modal.Data.Components.FirstOrDefault(c => c.CustomId == customId);
        return component?.Value;
    }

    /// <summary>
    /// Gets all registered prefixes for debugging purposes.
    /// </summary>
    /// <returns>An enumerable of all registered prefixes.</returns>
    public IEnumerable<string> GetRegisteredPrefixes()
    {
        return _handlers.Keys;
    }
}