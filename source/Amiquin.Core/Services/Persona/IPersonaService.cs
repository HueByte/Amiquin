namespace Amiquin.Core.Services.Persona;

public interface IPersonaService
{
    Task<string> GetPersonaAsync(ulong instanceId = 0);
    Task AddSummaryAsync(string updateMessage);
}