using System.ComponentModel.DataAnnotations.Schema;
using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public enum ToggleScope
{
    Server,
    Global
}

public class Toggle : DbModel<string>
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ToggleScope Scope { get; set; }

    [ForeignKey("ServerId")]
    public ulong ServerId { get; set; }
    public ServerMeta? Server { get; set; }
}