using System.Security.Cryptography;
using System.Text;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace KitRental.Core.Infrastructure.Persistence;

internal static class RobotlukCatalogSeeder
{
    private static readonly ComponentSeed[] Components =
    [
        new("4X4 16 Buton Keypad Matrix Tuş Takımı", "RBL-CMP-KEYPAD-4X4", "/images/catalog/robotluk/keypad-4x4.jpg"),
        new("Kırmızı Anahtar Yuvarlak Aç Kapa Anahtar Switch", "RBL-CMP-SWITCH-RED", "/images/catalog/robotluk/switch-red.jpg"),
        new("Röle Modülü 5V Tek Kanal SCM Arduino", "RBL-CMP-RELAY-5V-1CH", "/images/catalog/robotluk/relay-5v-1ch.jpg"),
        new("Potansiyometre 10K RV09", "RBL-CMP-POT-RV09-10K", "/images/catalog/robotluk/pot-rv09-10k.jpg"),
        new("Potansiyometre 10K", "RBL-CMP-POT-10K", "/images/catalog/robotluk/pot-10k.jpg"),
        new("Buzzer Modülsüz 5V", "RBL-CMP-BUZZER-5V", "/images/catalog/robotluk/buzzer-5v.jpg"),
        new("DC180 16mm Işıklı Buton - Mavi", "RBL-CMP-BUTTON-DC180-BLUE", "/images/catalog/robotluk/button-dc180-blue.jpg"),
        new("10mm LED Yeşil", "RBL-CMP-LED-10-GREEN", "/images/catalog/robotluk/led-10-green.jpg"),
        new("10mm LED Sarı", "RBL-CMP-LED-10-YELLOW", "/images/catalog/robotluk/led-10-yellow.jpg"),
        new("10mm LED Mavi", "RBL-CMP-LED-10-BLUE", "/images/catalog/robotluk/led-10-blue.jpg"),
        new("10mm LED Kırmızı", "RBL-CMP-LED-10-RED", "/images/catalog/robotluk/led-10-red.jpg"),
        new("RGB LED 10mm Ortak Katot", "RBL-CMP-RGB-LED-10", "/images/catalog/robotluk/rgb-led-10.jpg"),
        new("LED Işık Yeşil 5mm", "RBL-CMP-LED-5-GREEN", "/images/catalog/robotluk/led-5-green.jpg"),
        new("LED Işık Kırmızı 5mm", "RBL-CMP-LED-5-RED", "/images/catalog/robotluk/led-5-red.jpg"),
        new("RGB LED Modülü KY016", "RBL-CMP-RGB-LED-KY016", "/images/catalog/robotluk/rgb-led-ky016.jpg")
    ];

    private static readonly ProductSeed[] Products =
    [
        new("Black-Kit: Arduino Tabanlı Kapsamlı Robotik Kodlama ve Maker Eğitim Seti", "RBL-BLACK-KIT",
            "https://www.robotluk.com/urun/black-kit", "/images/catalog/robotluk/black-kit.jpeg"),
        new("Blue-Kit: Makey Makey Tabanlı STEM ve Robotik Kodlama Eğitim Seti", "RBL-BLUE-KIT",
            "https://www.robotluk.com/urun/blue-kit", "/images/catalog/robotluk/blue-kit.png"),
        new("Green-Kit: micro:bit Tabanlı Robotik Kodlama Eğitim Seti", "RBL-GREEN-KIT",
            "https://www.robotluk.com/urun/green-kit", "/images/catalog/robotluk/green-kit.png"),
        new("red-Kit: Arduino Robotik Kodlama Eğitim Seti", "RBL-RED-KIT",
            "https://www.robotluk.com/urun/red-kit", "/images/catalog/robotluk/red-kit.png")
    ];

    public static async Task SeedAsync(KitRentalDbContext db, CancellationToken cancellationToken)
    {
        var componentSkus = (await db.Components.Select(item => item.Sku).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Components.Where(item => !componentSkus.Contains(item.Sku)))
        {
            db.Components.Add(Component.Create(
                StableGuid($"robotluk-component:{definition.Sku}"),
                definition.Name,
                definition.Sku,
                "adet",
                0,
                definition.ImageUrl));
        }

        var productSkus = (await db.ProductModels.Select(item => item.Sku).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Products.Where(item => !productSkus.Contains(item.Sku)))
        {
            db.ProductModels.Add(ProductModel.Create(
                StableGuid($"robotluk-product:{definition.Sku}"),
                definition.Name,
                definition.Sku,
                $"Robotluk Robotik Bilim kataloğundan içe aktarıldı. Kaynak: {definition.SourceUrl}",
                definition.ImageUrl));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid StableGuid(string value) => new(MD5.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record ComponentSeed(string Name, string Sku, string ImageUrl);
    private sealed record ProductSeed(string Name, string Sku, string SourceUrl, string ImageUrl);
}
