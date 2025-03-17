namespace Amiquin.Core.Models;

public class Message
{
    public Guid Id { get; set; }
    public string Content { get; set; } = default!;
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public ulong AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }

}