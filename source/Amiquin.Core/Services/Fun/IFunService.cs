using Discord;
using Discord.WebSocket;
using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Fun;

/// <summary>
/// Service for handling fun commands and interactions.
/// </summary>
public interface IFunService
{
    /// <summary>
    /// Gets or generates a user's random "size" statistic.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="serverId">Discord server ID.</param>
    /// <returns>The user's size in centimeters.</returns>
    Task<int> GetOrGenerateDickSizeAsync(ulong userId, ulong serverId);

    /// <summary>
    /// Generates an image displaying a hex color.
    /// </summary>
    /// <param name="hexColor">Hex color code (with or without #).</param>
    /// <returns>Stream containing the color image.</returns>
    Task<Stream> GenerateColorImageAsync(string hexColor);

    /// <summary>
    /// Generates a random color palette.
    /// </summary>
    /// <param name="count">Number of colors in the palette (default 5).</param>
    /// <returns>List of hex color codes.</returns>
    List<string> GenerateColorPalette(int count = 5);

    /// <summary>
    /// Generates a color palette using proper color theory rules.
    /// </summary>
    /// <param name="harmonyType">Type of color harmony (complementary, triadic, etc.).</param>
    /// <param name="baseHue">Base hue for the palette (0-360), or null for random.</param>
    /// <returns>Palette information with colors and metadata.</returns>
    Task<ColorPalette> GenerateColorTheoryPaletteAsync(ColorHarmonyType harmonyType, float? baseHue = null);

    /// <summary>
    /// Generates a visual preview image of a color palette.
    /// </summary>
    /// <param name="colors">List of hex color codes to display.</param>
    /// <returns>Stream containing the palette preview image.</returns>
    Task<Stream> GeneratePaletteImageAsync(List<string> colors);

    /// <summary>
    /// Creates an interactive palette interface using Discord Components V2 with media gallery.
    /// </summary>
    /// <param name="palette">The color palette to display.</param>
    /// <param name="userId">User ID for the interaction.</param>
    /// <returns>Components V2 message component and file attachments for the interactive interface with color images.</returns>
    Task<(Discord.MessageComponent components, IEnumerable<FileAttachment> attachments)> CreateInteractivePaletteAsync(ColorPalette palette, ulong userId);

    /// <summary>
    /// Generates representative images for each color in a palette.
    /// </summary>
    /// <param name="palette">The color palette to generate images for.</param>
    /// <returns>Dictionary mapping color hex codes to image streams.</returns>
    Task<Dictionary<string, Stream>> GeneratePaletteColorImagesAsync(ColorPalette palette);

    /// <summary>
    /// Gets a random GIF URL for an interaction type.
    /// </summary>
    /// <param name="interactionType">Type of interaction (bite, kiss, etc.).</param>
    /// <returns>URL of the GIF.</returns>
    Task<string?> GetInteractionGifAsync(string interactionType);

    /// <summary>
    /// Gets a random NSFW GIF URL for a specific type.
    /// </summary>
    /// <param name="serverId">Discord server ID to check NSFW toggle.</param>
    /// <param name="nsfwType">Type of NSFW content (waifu, neko, etc.).</param>
    /// <returns>URL of the NSFW GIF, or null if NSFW is disabled or request fails.</returns>
    Task<string?> GetNsfwGifAsync(ulong serverId, string nsfwType);

    /// <summary>
    /// Checks if NSFW content is enabled for a server.
    /// </summary>
    /// <param name="serverId">Discord server ID to check.</param>
    /// <returns>True if NSFW is enabled, false otherwise.</returns>
    Task<bool> IsNsfwEnabledAsync(ulong serverId);

    /// <summary>
    /// Gets the list of available NSFW types.
    /// </summary>
    /// <returns>List of available NSFW content types.</returns>
    List<string> GetAvailableNsfwTypes();

    /// <summary>
    /// Gives a nacho to Amiquin from a user.
    /// </summary>
    /// <param name="userId">Discord user ID of the giver.</param>
    /// <param name="serverId">Discord server ID.</param>
    /// <returns>Total nachos the user has given.</returns>
    Task<int> GiveNachoAsync(ulong userId, ulong serverId);

    /// <summary>
    /// Generates a dynamic AI response for when a user gives Amiquin a nacho.
    /// </summary>
    /// <param name="userId">Discord user ID of the giver.</param>
    /// <param name="serverId">Discord server ID.</param>
    /// <param name="channelId">Discord channel ID for context.</param>
    /// <param name="userName">Name of the user giving the nacho.</param>
    /// <param name="totalNachos">Total nachos the user has given.</param>
    /// <returns>A personalized response message.</returns>
    Task<string> GenerateNachoResponseAsync(ulong userId, ulong serverId, ulong channelId, string userName, int totalNachos);

    /// <summary>
    /// Gets the top nacho givers for a server.
    /// </summary>
    /// <param name="serverId">Discord server ID.</param>
    /// <param name="limit">Number of top users to return.</param>
    /// <returns>List of embed fields for the leaderboard.</returns>
    Task<List<EmbedFieldBuilder>> GetNachoLeaderboardAsync(ulong serverId, int limit = 10);

    /// <summary>
    /// Gets total nachos received by Amiquin on a server.
    /// </summary>
    /// <param name="serverId">Discord server ID.</param>
    /// <returns>Total nachos received.</returns>
    Task<int> GetTotalNachosAsync(ulong serverId);
}