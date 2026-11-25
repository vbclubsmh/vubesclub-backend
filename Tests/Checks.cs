using System.Net;

namespace VubesClub.Verification;

internal static class Checks
{
    public static void Require(bool condition, string explanation)
    {
        if (!condition) throw new InvalidOperationException(explanation);
    }

    public static Reply Expect(this Reply reply, HttpStatusCode expected)
    {
        Require(reply.Status == expected, $"Expected {(int)expected}, got {(int)reply.Status}: {reply.Body}");
        return reply;
    }

    public static void Passed(string name) => Console.WriteLine($"PASS  {name}");
}
