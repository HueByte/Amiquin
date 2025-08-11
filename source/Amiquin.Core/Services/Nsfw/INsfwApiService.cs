using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Nsfw;

public interface INsfwApiService
{
    Task<List<NsfwImage>> GetDailyNsfwImagesAsync(int waifuCount = 5, int otherCount = 5);
    Task<List<NsfwImage>> GetWaifuImagesAsync(int count = 5);
    Task<List<NsfwImage>> GetAlternativeNsfwImagesAsync(int count = 5);
}