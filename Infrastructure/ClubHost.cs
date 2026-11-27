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
        builder.Services.AddSingleton<IMailSender, MailSender>();
        builder.Services.AddSingleton<ISmsCodes, OtpCodes>();
        builder.Services.AddSingleton<OtpProtector>();
        builder.Services.AddSingleton<AvatarStore>();
        if (builder.Environment.IsDevelopment())
            builder.Services.AddScoped<IPaymentGateway, DevelopmentPaymentGateway>();
        else if ((builder.Configuration["Payments:Mode"] ?? "placeholder") == "placeholder")
            builder.Services.AddScoped<IPaymentGateway, PlaceholderPaymentGateway>();
        else
            builder.Services.AddSingleton<IPaymentGateway, UnconfiguredGateway>();
        builder.Services.AddScoped<Caller>();
        builder.Services.AddScoped<IdentityService>();
        builder.Services.AddScoped<ProfileService>();
        builder.Services.AddScoped<CatalogQueries>();
        builder.Services.AddScoped<BookingService>();
        builder.Services.AddScoped<BookingMailer>();
        builder.Services.AddScoped<PaymentService>();
        builder.Services.AddScoped<FieldCheckInService>();
        builder.Services.AddScoped<CommunityService>();
        builder.Services.AddScoped<LiveMapService>();

        configure?.Invoke(builder);
        var app = builder.Build();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = app.Services.GetRequiredService<AvatarStore>().Files,
            RequestPath = "/media/members",
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            }
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=3600";
                context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            }
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

        app.MapGet("/development/event-terms/v1", () => Results.Text(
            "Vubes Club · Etkinlik katılım koşulları (v1)\n\n" +
            "Katılımınız seçtiğiniz etkinliğin gösterilen programı için kaydedilir. Ödeme altyapısı devreye alınana kadar " +
            "katılım ödeme alınmadan kesinleştirilir ve kart bilgisi alınmaz. Etkinlik girişinde e-postanızdaki veya " +
            "uygulamadaki QR kodunu standımızda okutmanız gerekir.",
            "text/plain; charset=utf-8"));

        app.MapGet("/health/live", () => Results.Ok());

        return app;
    }
}
