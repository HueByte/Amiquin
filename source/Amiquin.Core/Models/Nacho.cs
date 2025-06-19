using System.ComponentModel.DataAnnotations.Schema;
using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public class NachoPack : DbModel<int>
{
    public int NachoCount { get; set; }
    public string Username { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public DateTime NachoReceivedDate { get; set; }

    [ForeignKey("ServerId")]
    public ulong ServerId { get; set; }
    public ServerMeta? Server { get; set; }
}