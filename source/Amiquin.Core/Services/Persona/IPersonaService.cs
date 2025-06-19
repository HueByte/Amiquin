namespace Amiquin.Core.Services.Persona;

public interface IPersonaService
{
    Task AddSummaryAsync(ulong serverId, string updateMessage);
    Task<string> GetPersonaAsync(ulong serverId);
}
