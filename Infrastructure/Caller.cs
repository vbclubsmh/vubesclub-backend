using System.Security.Cryptography;
using System.Text;

namespace VubesClub.Api.Infrastructure;

public sealed class Caller
{
    public Guid MemberId { get; internal set; }
    public Guid SessionId { get; internal set; }
    public string Membership { get; internal set; } = "";
    public DateTime Expires { get; internal set; }
}
