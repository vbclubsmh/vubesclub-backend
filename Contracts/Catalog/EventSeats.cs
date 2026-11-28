namespace VubesClub.Api.Contracts;

/// Etkinlik kontenjanı. `Taken`, kesinleşen katılımlar ile süresi dolmamış yer ayırmalarını sayar.
public sealed record EventSeats(int Capacity, int Taken, int Remaining);
