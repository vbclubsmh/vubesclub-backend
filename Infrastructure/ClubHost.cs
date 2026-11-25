using VubesClub.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Modules.Booking;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Modules.Community;
using VubesClub.Api.Modules.Field;
using VubesClub.Api.Modules.Identity;
using VubesClub.Api.Modules.Payments;

namespace VubesClub.Api.Infrastructure;

public static class ClubHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connection = builder.Configuration.GetConnectionString("Club")
            ?? throw new InvalidOperationException("ConnectionStrings:Club is required.");

        builder.Services.AddDbContext<ClubDb>(options => options.UseNpgsql(connection));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISmsCodes, DevelopmentSmsCodes>();
        builder.Services.AddSingleton<OtpProtector>();
        if (builder.Environment.IsDevelopment())
            builder.Services.AddScoped<IPaymentGateway, DevelopmentPaymentGateway>();
        else
            builder.Services.AddSingleton<IPaymentGateway, UnconfiguredGateway>();
        builder.Services.AddScoped<Caller>();
        builder.Services.AddScoped<IdentityService>();
        builder.Services.AddScoped<CatalogQueries>();
        builder.Services.AddScoped<BookingService>();
        builder.Services.AddScoped<PaymentService>();
        builder.Services.AddScoped<CommunityService>();
        builder.Services.AddScoped<LiveMapService>();

        configure?.Invoke(builder);
        var app = builder.Build();

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public,max-age=3600"
        });
        app.UseRouting();
        app.UseClubAccess();
        var api = app.MapGroup("/api/v1");
        api.MapGet("/app/appearance", () => new AppAppearance(
            new Media("/media/Dusk.jpg", "Vubes Club"),
            new Media("/media/DiscoverTerrain.png", "Etkinlik haritası"),
            new Media("/media/Terrace.png", "Üyelik başvurusu"),
            new Media("/media/Wordmark.png", "Vubes Club")));
        api.MapIdentity();
        api.MapCatalog();
        api.MapBooking();
        api.MapCommunity();
        api.MapField();

        app.MapPost("/integrations/payments/{provider}/webhooks", async (string provider, HttpRequest request,
            IPaymentGateway gateway, PaymentService service, CancellationToken ct) =>
        {
            if (!gateway.IsConfigured || !string.Equals(gateway.Name, provider, StringComparison.Ordinal))
                throw ApiFault.Missing();
            var notice = await gateway.VerifyNotificationAsync(request, ct);
            await service.Apply(notice, ct);
            return Results.Ok();
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/development/event-terms/v1", () => Results.Text(
                "Vubes Club geliştirme ortamı · Örnek etkinlik katılım koşulları (v1)\n\n" +
                "Bu ortamda ödeme simülasyonu kullanılır. Kart bilgisi alınmaz ve ücret tahsil edilmez. " +
                "Onayınız seçtiğiniz etkinliğin gösterilen programı ve toplam tutarı için test kaydı oluşturur.",
                "text/plain; charset=utf-8"));
        }

        app.MapGet("/health/live", () => Results.Ok());

        return app;
    }
}
