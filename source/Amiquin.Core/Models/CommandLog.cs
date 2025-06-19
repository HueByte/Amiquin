using System.ComponentModel.DataAnnotations.Schema;
using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public class CommandLog : DbModel<int>
{
    public string Command { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public DateTime CommandDate { get; set; }
    public int Duration { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    [ForeignKey("ServerId")]
    public ulong ServerId { get; set; }
    public ServerMeta? Server { get; set; }
}