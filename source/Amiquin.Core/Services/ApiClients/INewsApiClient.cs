using Amiquin.Core.Services.ApiClients.Responses;

namespace Amiquin.Core.Services.ApiClients;

public interface INewsApiClient
{
    Task<NewsApiResponse?> GetNewsAsync();
}