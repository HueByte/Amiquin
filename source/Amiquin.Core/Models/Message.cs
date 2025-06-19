using System.ComponentModel.DataAnnotations.Schema;
using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public class Message : DbModel<string>
{
    public string Content { get; set; } = default!;
    public ulong GuildId { get; set; }
    public ulong AuthorId { get; set; }
    public bool IsUser { get; set; }
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ServerId")]
    public ulong ServerId { get; set; }
    public ServerMeta? Server { get; set; }

}