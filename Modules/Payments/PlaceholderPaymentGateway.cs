using VubesClub.Api.Infrastructure;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Payments;

/// <summary>
/// Ödeme sağlayıcısı (Tami) bağlanana kadar katılımı ödeme almadan kesinleştirir.
/// Kayıtlar "placeholder" sağlayıcısıyla işaretlenir; gerçek sağlayıcı geldiğinde bu sınıf kaldırılır.
/// </summary>
public sealed class PlaceholderPaymentGateway(ClubDb db) : DevelopmentPaymentGateway(db)
{
    public override string Name => "placeholder";
}
