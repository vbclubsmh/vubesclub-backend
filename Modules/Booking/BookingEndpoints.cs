using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Payments;

namespace VubesClub.Api.Modules.Booking;

public static class BookingEndpoints
{
    public static void MapBooking(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("").ForMembers();
        routes.MapPost("/booking-holds", async (CreateHold body, HttpRequest request, BookingService service, CancellationToken ct) =>
        {
            var result = await service.Create(body, Key(request), ct);
            return Results.Json(result.Hold, statusCode: result.Created ? 201 : 200);
        });
        routes.MapGet("/booking-holds/{holdId:guid}", async (Guid holdId, BookingService service, PaymentService payments, CancellationToken ct) =>
        {
            await payments.ReconcileOwned(holdId, ct);
            return await service.Read(holdId, ct);
        });
        routes.MapPost("/booking-holds/{holdId:guid}/payments", async (Guid holdId, StartPayment body,
            HttpRequest request, PaymentService payments, CancellationToken ct) =>
        {
            var result = await payments.Start(holdId, body, Key(request), ct);
            return Results.Json(result.Payment, statusCode: result.Created ? 201 : 200);
        });
        routes.MapGet("/me/bookings", (string bucket, string? cursor, int? limit, BookingService service, CancellationToken ct) =>
            service.List(bucket, cursor, limit, ct));
        routes.MapGet("/me/bookings/{bookingId:guid}/pass", (Guid bookingId, BookingService service, CancellationToken ct) =>
            service.Pass(bookingId, ct));
    }

    private static string Key(HttpRequest request)
    {
        var keys = request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrWhiteSpace(keys[0]) || keys[0]!.Length > 150)
            throw ApiFault.Invalid("Idempotency-Key başlığı gerekli.");
        return keys[0]!;
    }
}
