using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(KitRentalDbContext))]
    [Migration("20260907143000_SeedRedKitFaultGuides")]
    public partial class SeedRedKitFaultGuides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @redKitProductModelId uniqueidentifier;
                DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();

                SELECT TOP (1) @redKitProductModelId = Id
                FROM ProductModels
                WHERE LOWER(Sku) IN (N'red-kit', N'redkit', N'red_kit')
                   OR LOWER(Name) IN (N'red-kit', N'red kit')
                   OR LOWER(Name) LIKE N'%red%kit%'
                   OR LOWER(COALESCE(ImageUrl, N'')) LIKE N'%red-kit%'
                ORDER BY
                    CASE
                        WHEN LOWER(Sku) = N'red-kit' THEN 0
                        WHEN LOWER(ImageUrl) LIKE N'%red-kit%' THEN 1
                        WHEN LOWER(Name) IN (N'red-kit', N'red kit') THEN 2
                        ELSE 3
                    END,
                    Name;

                IF @redKitProductModelId IS NOT NULL
                BEGIN
                    DELETE FROM FaultGuideEntries
                    WHERE ProductModelId = @redKitProductModelId
                       OR (
                            ProductModelId IS NULL
                            AND Title IN (
                                N'DHT11',
                                N'LDR',
                                N'PIR',
                                N'Ultrasonik Sensör',
                                N'POT',
                                N'Buton',
                                N'RGB LED',
                                N'LED',
                                N'LED / PWM',
                                N'Buzzer',
                                N'LCD'
                            )
                       );

                    INSERT INTO FaultGuideEntries
                        (Id, ProductModelId, Title, Problem, Solution, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
                    VALUES
                        ('91bc29af-7476-4434-9e22-958a9a02f08f', @redKitProductModelId, N'DHT11',
                         N'Sıcaklık ve nem değeri okunmuyor veya hatalı okunuyor.',
                         N'DHT11 sinyal pininin kodda doğru tanımlandığını kontrol edin. Gerekli DHT kütüphanesini yükleyin ve sensörü örnek DHT koduyla test edin.',
                         10, 1, @now, @now),
                        ('ecfa7d58-d364-4f22-aef5-cd20e870f8c0', @redKitProductModelId, N'LDR',
                         N'Ortam ışığı değişmesine rağmen okunan değer değişmiyor veya beklenenden farklı değişiyor.',
                         N'LDR''nin sinyal bağlantısını ve kodda kullanılan analog pini kontrol edin. analogRead() ile değeri seri monitörde test edin.',
                         20, 1, @now, @now),
                        ('fb698be9-247f-482e-86d3-f1bc3b37eb07', @redKitProductModelId, N'PIR',
                         N'Hareket algılanmıyor veya sürekli hareket algılanıyor.',
                         N'PIR sinyal pinini doğru tanımlayın. Sensörün başlangıçta birkaç saniye beklemesine izin verin ve hareket algılama mantığını kodda doğru kurun.',
                         30, 1, @now, @now),
                        ('8c6cd0d4-621b-4509-a5e6-79687d5cd3e3', @redKitProductModelId, N'Ultrasonik Sensör',
                         N'Mesafe değeri okunmuyor veya hatalı ölçülüyor.',
                         N'Trig ve Echo pinlerini kodla eşleştirin. VCC ve GND bağlantılarını kontrol edin ve sensörü örnek mesafe koduyla test edin.',
                         40, 1, @now, @now),
                        ('36f261b5-1a41-4352-b873-2b9cd9b34b1d', @redKitProductModelId, N'POT',
                         N'Potansiyometre çevrildiğinde okunan değer değişmiyor.',
                         N'Kart üzerindeki potansiyometrenin sinyal çıkışını kullanılan analog pine bağlayın. Kodda aynı analog pini okuyun ve değerin seri monitörde değiştiğini kontrol edin.',
                         50, 1, @now, @now),
                        ('91ee25ba-89d6-48ec-aa42-cd0975a89ec5', @redKitProductModelId, N'Buton',
                         N'Butona basıldığında beklenen işlem gerçekleşmiyor.',
                         N'Butonun bağlı olduğu pini doğru tanımlayın. INPUT / INPUT_PULLUP kullanımını projeye uygun şekilde düzenleyin.',
                         60, 1, @now, @now),
                        ('2e320fbf-b65c-41fb-83c7-f71c711351dc', @redKitProductModelId, N'RGB LED',
                         N'Renkler oluşmuyor, bazı renkler çalışmıyor veya parlaklık kontrolü yapılamıyor.',
                         N'RGB pinlerinin doğru tanımlandığını kontrol edin. PWM gereken projelerde PWM destekleyen pinleri kullanın.',
                         70, 1, @now, @now),
                        ('70e74afe-8d2a-42c0-af80-5ae61163561a', @redKitProductModelId, N'LED',
                         N'LED yanmıyor veya beklenen şekilde çalışmıyor.',
                         N'LED''in bağlı olduğu pin ile koddaki pin tanımını eşleştirin. İlgili LED''i basit bir aç/kapa koduyla test edin.',
                         80, 1, @now, @now),
                        ('4e2725c3-8ecd-41c3-9993-823a143a40a4', @redKitProductModelId, N'LED / PWM',
                         N'Potansiyometre ile LED parlaklığı değişmiyor.',
                         N'LED''in PWM destekleyen bir pine bağlı olduğundan emin olun. PWM desteklemeyen data pinlerinde potansiyometre ile parlaklık kontrolü yapılamaz.',
                         90, 1, @now, @now),
                        ('f69e8a6b-a134-4f00-a33a-6a20f781af24', @redKitProductModelId, N'Buzzer',
                         N'Buzzer ses çıkarmıyor veya beklenen şekilde çalışmıyor.',
                         N'Buzzer pinini doğru tanımlayın ve tone() veya kullanılan yöntemin doğru pin üzerinde çalıştığını kontrol edin. Basit bir buzzer test koduyla deneyin.',
                         100, 1, @now, @now),
                        ('bb323159-de78-4486-953f-bc32a6a497ac', @redKitProductModelId, N'LCD',
                         N'LCD''de yazılar görünmüyor veya ekran çalışmıyor sanılıyor.',
                         N'Öncelikle LCD kontrast ayarını uygun seviyeye getirin. Ardından bağlantıları ve LCD kodunu kontrol edin. Kontrast yanlış ayardaysa ekran arızalı sanılabilir.',
                         110, 1, @now, @now);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM FaultGuideEntries
                WHERE Id IN (
                    '91bc29af-7476-4434-9e22-958a9a02f08f',
                    'ecfa7d58-d364-4f22-aef5-cd20e870f8c0',
                    'fb698be9-247f-482e-86d3-f1bc3b37eb07',
                    '8c6cd0d4-621b-4509-a5e6-79687d5cd3e3',
                    '36f261b5-1a41-4352-b873-2b9cd9b34b1d',
                    '91ee25ba-89d6-48ec-aa42-cd0975a89ec5',
                    '2e320fbf-b65c-41fb-83c7-f71c711351dc',
                    '70e74afe-8d2a-42c0-af80-5ae61163561a',
                    '4e2725c3-8ecd-41c3-9993-823a143a40a4',
                    'f69e8a6b-a134-4f00-a33a-6a20f781af24',
                    'bb323159-de78-4486-953f-bc32a6a497ac'
                );
                """);
        }
    }
}
