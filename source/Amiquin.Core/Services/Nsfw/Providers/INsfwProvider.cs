using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Nsfw.Providers;

/// <summary>
/// Interface for NSFW image providers
/// </summary>
public interface INsfwProvider
{
    /// <summary>
    /// The unique name of the provider
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the provider is currently available
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Fetches a single NSFW image from the provider
    /// </summary>
    /// <returns>An NSFW image or null if failed</returns>
    Task<NsfwImage?> FetchImageAsync();

    /// <summary>
    /// Fetches multiple NSFW images from the provider
    /// </summary>
    /// <param name="count">Number of images to fetch</param>
    /// <returns>List of NSFW images</returns>
    Task<List<NsfwImage>> FetchImagesAsync(int count);
}