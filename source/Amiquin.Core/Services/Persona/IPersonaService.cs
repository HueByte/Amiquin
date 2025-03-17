namespace Amiquin.Core.Services.Persona;

public interface IPersonaService
{
    Task<string> GetPersonaAsync(ulong channelId = 0);
}