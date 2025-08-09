using Amiquin.Core.Abstraction;
using System.ComponentModel.DataAnnotations.Schema;

namespace Amiquin.Core.Models;

public class Toggle : DbModel<string>
{
    public override string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ServerId")]
    public ulong ServerId { get; set; }
    public ServerMeta? Server { get; set; }
}