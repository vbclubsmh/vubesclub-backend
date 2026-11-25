using System.Security.Cryptography;
using System.Text;

namespace VubesClub.Api.Infrastructure;

public static class AccessExtensions
{
    public static RouteGroupBuilder ForMembers(this RouteGroupBuilder group, bool active = true) =>
        group.WithMetadata(new MemberAccess(active));
}
