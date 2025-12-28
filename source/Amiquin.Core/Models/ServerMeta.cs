using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public class ServerMeta : DbModel<ulong>
{
    public string ServerName { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;

    public List<Toggle>? Toggles { get; set; }
    public List<Models.Message>? Messages { get; set; }
    public List<Models.CommandLog>? CommandLogs { get; set; }
    public List<NachoPack>? NachoPacks { get; set; }
    public ulong? PrimaryChannelId { get; set; }
    public string? PreferredProvider { get; set; }
    public string? PreferredModel { get; set; }
    public ulong? NsfwChannelId { get; set; }
}