# Vubes Club mobil API

ASP.NET Core 10, EF Core ve PostgreSQL ile modüler API. Her entity ve istek/yanıt modeli ayrı C# dosyasındadır. Modüller Identity, Catalog, Booking, Payments, Community ve Field'dır. HTTP sözleşmesi Contracts/mobile.openapi.json içindedir.

## Yerel çalıştırma

PostgreSQL TCP üzerinden 127.0.0.1:5432 adresinde çalışır. Geliştirme bağlantısı appsettings.Development.json içinde vubes_mobile_dev veritabanını kullanır. İlk kurulumda bu veritabanı oluşturulmalıdır. Kullanıcı adı belirtilmezse yerel işletim sistemi rolü kullanılır.

Proje kökünden:

```bash
cd apps/backend-api
ASPNETCORE_ENVIRONMENT=Development ~/.dotnet/dotnet run -- --migrate --seed-demo
ASPNETCORE_ENVIRONMENT=Development ~/.dotnet/dotnet run --launch-profile VubesClub.Api
```

API http://localhost:5080 adresindedir. Debug iOS Simulator bu adresi kullanır. Fiziksel iPhone için VUBES_API_BASE_URL, telefonun erişebildiği API adresine ayarlanmalıdır. Release dağıtımında HTTPS API adresi gerekir.

Mevcut üyeler GSM ve OTP ile giriş yapar. Yeni üyeler altı adımlı başvuruda GSM doğrulamasını tamamlar; davet kodu isteğe bağlıdır. Telefonu doğrulanan başvuru `pending` durumunda `member` ve `application_entry` kayıtları oluşturur. Studio üyeler ekranı e-posta ve başvuru profilini gösterir; yalnız Studio üzerinden `active` yapılan üyeler giriş yapabilir.

SMS sağlayıcısı `ISmsCodes` adaptör sınırındadır. Geçici test OTP'si sunucuda `Identity:AllowFixedOtp=true` ile açılır; bu modda SMS gönderilmez ve her OTP challenge'ı `1111` ile doğrulanır. Bu ayarı gerçek kullanıcıların erişebildiği bir ortamda açmayın. Ayar kapalıyken yapılandırılmış bir sağlayıcı yoksa OTP endpoint'i `503 SMS_UNAVAILABLE` döndürür. `Security:OtpKey` en az 32 UTF-8 bayt olmalıdır.

## Örnek içerik ve görseller

SQL başlangıç verisi 10 etkinlik, 6 spor, 3 PT, ekipler, program oturumları, profil, geçmiş katılımlar, puanlar ve örnek canlı rota içerir. Görsel adresleri PostgreSQL'de /media yollarıdır. Gerçek dosyalar wwwroot/media üzerinden sunulur ve publish çıktısına kopyalanır. Karşılama ve harita görsellerinin adresleri herkese açık GET /api/v1/app/appearance ile alınır.

Seed tekrarlanabilir ve mevcut üyelikleri/katılımları çoğaltmaz. Development ortamında API başlangıcı örnek katalog tarihlerini bugüne taşır. Yalnız örnek etkinlik kimlikleri güncellenir. Canlı haritadaki beş temsili katılımcının konum zamanı geliştirme gösterimi için yenilenir. Ada'nın cihaz konumu kullanıcı paylaşımı açtığında yazılır. Gerçek konum ölçümleri bu örnek yenilemesine dahil değildir.

## Etkinlik başına ödeme

Development ortamında sunucu tarafındaki test ödeme adaptörü, koşullar kabul edildiğinde kalıcı ödeme kaydı üzerinden başarılı sonucu üretir. Kesin katılım ve katılım kartı aynı backend işlemiyle oluşturulur. Sağlayıcı anlaşmasından sonra IPaymentGateway için gerçek adaptör bağlanmalıdır. Üretim ortamında sağlayıcı yapılandırılana kadar tahsilat başlatma 503 döner. Ödeme sonucu doğrulaması istek ve doğrulanmış bildirim üzerinden yürür.

## Doğrulama

Tests/VubesClub.Verification.csproj, gerçek HTTP middleware ve ayrı PostgreSQL test şemasında kabul senaryolarını çalıştırır. Bağlantı yalnız adı _test ile biten veritabanını kabul eder. Her test çalışması kendi geçici şemasını temizler.

```bash
VUBES_VERIFY_POSTGRES='Host=127.0.0.1;Database=vubes_mobile_acceptance_1123_test' ~/.dotnet/dotnet run --project Tests/VubesClub.Verification.csproj -c Release
```

Giriş/davet, katalog ve SQL değişikliğinin yanıtı, kaydetme, takip, tanışma, değerlendirme, konum paylaşımı, oturum yenileme, eşzamanlı son kontenjan, ödeme bildirimi tekrarı ve geç ödeme iadesi sınanır.
