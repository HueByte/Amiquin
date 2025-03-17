using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public class Message : DbModel<string>
{
    public override string Id { get; set; } = default!;
    public string Content { get; set; } = default!;
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public ulong AuthorId { get; set; }
    public bool IsUser { get; set; }
    public DateTime CreatedAt { get; set; }

}