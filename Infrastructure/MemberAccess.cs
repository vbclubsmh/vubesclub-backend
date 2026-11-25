using System.Security.Cryptography;
using System.Text;

namespace VubesClub.Api.Infrastructure;

public sealed record MemberAccess(bool ActiveRequired);
