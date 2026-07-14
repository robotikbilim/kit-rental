using System.Security.Cryptography;
using System.Text;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Support;
using KitRental.Core.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitRental.Core.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    private static readonly Guid DemoActorId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public static async Task SeedCoreDemoDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KitRentalDbContext>();

        var componentDefinitions = new[]
        {
            new ComponentSeed("Arduino Uno R3", "CMP-ARD-UNO", "adet", 10, "/images/catalog/board.svg"),
            new ComponentSeed("ESP32 Wi-Fi Geliştirme Kartı", "CMP-ESP32", "adet", 8, "/images/catalog/board.svg"),
            new ComponentSeed("DC Redüktörlü Motor 6V", "CMP-MOTOR-6V", "adet", 20, "/images/catalog/motor.svg"),
            new ComponentSeed("SG90 Mikro Servo Motor", "CMP-SERVO-SG90", "adet", 8, "/images/catalog/motor.svg"),
            new ComponentSeed("HC-SR04 Ultrasonik Sensör", "CMP-HCSR04", "adet", 10, "/images/catalog/sensor.svg"),
            new ComponentSeed("3'lü Kızılötesi Çizgi Sensörü", "CMP-IR-3", "adet", 12, "/images/catalog/sensor.svg"),
            new ComponentSeed("L298N Çift Motor Sürücü", "CMP-L298N", "adet", 10, "/images/catalog/board.svg"),
            new ComponentSeed("830 Nokta Breadboard", "CMP-BREAD-830", "adet", 12, "/images/catalog/parts.svg"),
            new ComponentSeed("Jumper Kablo Erkek-Erkek", "CMP-JUMPER-MM", "adet", 100, "/images/catalog/parts.svg"),
            new ComponentSeed("5mm Kırmızı LED", "CMP-LED-RED", "adet", 100, "/images/catalog/parts.svg"),
            new ComponentSeed("220 Ohm Direnç", "CMP-RES-220", "adet", 200, "/images/catalog/parts.svg"),
            new ComponentSeed("4'lü AA Pil Yuvası", "CMP-BAT-AA4", "adet", 15, "/images/catalog/parts.svg"),
            new ComponentSeed("65mm Robot Tekerleği", "CMP-WHEEL-65", "adet", 30, "/images/catalog/mechanical.svg"),
            new ComponentSeed("2WD Akrilik Robot Şasisi", "CMP-CHASSIS-2WD", "adet", 12, "/images/catalog/mechanical.svg"),
            new ComponentSeed("Metal Sarhoş Teker", "CMP-CASTER", "adet", 15, "/images/catalog/mechanical.svg"),
            new ComponentSeed("USB A-B Programlama Kablosu", "CMP-USB-AB", "adet", 15, "/images/catalog/parts.svg"),
            new ComponentSeed("Aktif Buzzer 5V", "CMP-BUZZER", "adet", 20, "/images/catalog/parts.svg"),
            new ComponentSeed("10K Potansiyometre", "CMP-POT-10K", "adet", 20, "/images/catalog/parts.svg"),
            new ComponentSeed("DHT11 Sıcaklık ve Nem Sensörü", "CMP-DHT11", "adet", 10, "/images/catalog/sensor.svg"),
            new ComponentSeed("Mini LDR Işık Sensörü", "CMP-LDR", "adet", 15, "/images/catalog/sensor.svg")
        };
        var locationDefinitions = new[]
        {
            new LocationSeed("ELEK-A-01", "Elektronik Deposu", "A", "01", "01"),
            new LocationSeed("ELEK-A-02", "Elektronik Deposu", "A", "01", "02"),
            new LocationSeed("ELEK-B-01", "Elektronik Deposu", "B", "02", "01"),
            new LocationSeed("ELEK-B-02", "Elektronik Deposu", "B", "02", "02"),
            new LocationSeed("MEK-C-01", "Mekanik Deposu", "C", "01", "01"),
            new LocationSeed("MEK-C-02", "Mekanik Deposu", "C", "01", "02"),
            new LocationSeed("ATOLYE-A-01", "Montaj Atölyesi", "A", "03", "01"),
            new LocationSeed("ATOLYE-A-02", "Montaj Atölyesi", "A", "03", "02")
        };

        var components = (await db.Components.ToListAsync(cancellationToken)).ToDictionary(item => item.Sku);
        foreach (var definition in componentDefinitions.Where(item => !components.ContainsKey(item.Sku)))
        {
            var component = Component.Create(StableGuid($"component:{definition.Sku}"), definition.Name, definition.Sku,
                definition.Unit, definition.MinimumStock, definition.ImageUrl);
            db.Components.Add(component);
            components[component.Sku] = component;
        }
        var locations = (await db.StorageLocations.ToListAsync(cancellationToken)).ToDictionary(item => item.Code);
        foreach (var definition in locationDefinitions.Where(item => !locations.ContainsKey(item.Code)))
        {
            var location = StorageLocation.Create(StableGuid($"location:{definition.Code}"), definition.Code,
                definition.Warehouse, definition.Aisle, definition.Rack, definition.Shelf);
            db.StorageLocations.Add(location);
            locations[location.Code] = location;
        }
        await db.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < componentDefinitions.Length; index++)
        {
            var definition = componentDefinitions[index];
            var component = components[definition.Sku];
            var firstLocation = locations[locationDefinitions[index % 6].Code];
            var secondLocation = locations[locationDefinitions[6 + index % 2].Code];
            var total = definition.Sku == "CMP-SERVO-SG90" ? 5m : definition.MinimumStock + 18 + index * 3;
            await AddOpeningStockAsync(db, component, firstLocation, Math.Ceiling(total * 0.7m), now, cancellationToken);
            await AddOpeningStockAsync(db, component, secondLocation, Math.Floor(total * 0.3m), now, cancellationToken);
        }

        var kitDefinitions = new[]
        {
            new KitSeed("Robotik Kodlamaya Başlangıç Kiti", "KIT-START-01", "Arduino ile temel elektronik, sensör ve kodlama uygulamaları için sınıf seti.",
                [("CMP-ARD-UNO",1), ("CMP-BREAD-830",1), ("CMP-JUMPER-MM",20), ("CMP-LED-RED",5), ("CMP-RES-220",5), ("CMP-BUZZER",1), ("CMP-USB-AB",1)]),
            new KitSeed("Çizgi İzleyen Robot Kiti", "KIT-LINE-01", "Sensör verisi, motor kontrolü ve algoritma geliştirme eğitim kiti.",
                [("CMP-ARD-UNO",1), ("CMP-IR-3",1), ("CMP-MOTOR-6V",2), ("CMP-L298N",1), ("CMP-WHEEL-65",2), ("CMP-CHASSIS-2WD",1), ("CMP-CASTER",1), ("CMP-BAT-AA4",1)]),
            new KitSeed("Engelden Kaçan Robot Kiti", "KIT-AVOID-01", "Ultrasonik mesafe ölçümüyle otonom yön değiştirme uygulamaları.",
                [("CMP-ARD-UNO",1), ("CMP-HCSR04",1), ("CMP-MOTOR-6V",2), ("CMP-L298N",1), ("CMP-WHEEL-65",2), ("CMP-CHASSIS-2WD",1), ("CMP-CASTER",1), ("CMP-BAT-AA4",1)]),
            new KitSeed("IoT Akıllı Ev Eğitim Kiti", "KIT-IOT-HOME", "ESP32 ile kablosuz izleme, sıcaklık, nem ve ışık otomasyonu.",
                [("CMP-ESP32",1), ("CMP-DHT11",1), ("CMP-LDR",1), ("CMP-BREAD-830",1), ("CMP-JUMPER-MM",15), ("CMP-LED-RED",2), ("CMP-RES-220",2), ("CMP-BUZZER",1)]),
            new KitSeed("Servo Radar Proje Kiti", "KIT-RADAR-01", "Servo tarama ve ultrasonik sensörle çevre haritalama projesi.",
                [("CMP-ARD-UNO",1), ("CMP-HCSR04",1), ("CMP-SERVO-SG90",1), ("CMP-BREAD-830",1), ("CMP-JUMPER-MM",12), ("CMP-USB-AB",1)]),
            new KitSeed("Mini Sumo Robot Kiti", "KIT-SUMO-01", "Mekanik tasarım, rakip algılama ve hızlı motor kontrolü için yarışma kiti.",
                [("CMP-ESP32",1), ("CMP-IR-3",2), ("CMP-MOTOR-6V",2), ("CMP-L298N",1), ("CMP-WHEEL-65",2), ("CMP-CHASSIS-2WD",1), ("CMP-CASTER",1), ("CMP-BAT-AA4",1)])
        };
        var products = (await db.ProductModels.ToListAsync(cancellationToken)).ToDictionary(item => item.Sku);
        foreach (var definition in kitDefinitions)
        {
            if (!products.TryGetValue(definition.Sku, out var product))
            {
                product = ProductModel.Create(StableGuid($"kit:{definition.Sku}"), definition.Name, definition.Sku,
                    definition.Description, "/images/catalog/kit.svg");
                db.ProductModels.Add(product);
                products[product.Sku] = product;
            }
            if (!await db.BillsOfMaterials.AnyAsync(item => item.ProductModelId == product.Id, cancellationToken))
            {
                var lines = definition.Lines.Select(line => (components[line.Sku].Id, (decimal)line.Quantity));
                db.BillsOfMaterials.Add(BillOfMaterials.Create(StableGuid($"bom:{definition.Sku}:1"), product.Id, 1, lines));
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        await RobotlukCatalogSeeder.SeedAsync(db, cancellationToken);

        var rentalCustomers = new[]
        {
            ("TACEV Kadıköy Eğitim Merkezi", "kadikoy@tacev.demo", "0216 555 10 10", "Eğitim Mah. Atölye Sok. No: 12", "Kadıköy", "İstanbul"),
            ("Bilim Çocuk Akademisi", "operasyon@bilimcocuk.demo", "0312 555 20 20", "Üniversiteler Mah. Bilim Cad. No: 8", "Çankaya", "Ankara"),
            ("Nilüfer Robotik Atölyesi", "atolye@nilufer.demo", "0224 555 30 30", "23 Nisan Mah. Teknoloji Cad. No: 4", "Nilüfer", "Bursa"),
            ("İzmir Gençlik Bilim Merkezi", "bilim@izmir.demo", "0232 555 40 40", "Kazımdirik Mah. Kampüs Sok. No: 21", "Bornova", "İzmir")
        };
        var customerEntities = new List<Customer>();
        foreach (var definition in rentalCustomers)
        {
            var customer = await db.Customers.Include(item => item.Addresses)
                .SingleOrDefaultAsync(item => item.Email == definition.Item2, cancellationToken);
            if (customer is null)
            {
                customer = Customer.Create(StableGuid($"customer:{definition.Item2}"), definition.Item1, definition.Item2);
                customer.AddAddress("Eğitim merkezi", definition.Item1, definition.Item3, definition.Item4,
                    definition.Item5, definition.Item6, "34000");
                db.Customers.Add(customer);
            }
            customerEntities.Add(customer);
        }
        await db.SaveChangesAsync(cancellationToken);

        var kitIndex = 0;
        foreach (var product in kitDefinitions.Select(definition => products[definition.Sku]).OrderBy(item => item.Sku))
        {
            for (var unitIndex = 1; unitIndex <= 3; unitIndex++)
            {
                var serial = $"KR-{product.Sku.Replace("KIT-", string.Empty)}-26-{unitIndex:000}";
                if (await db.ProductUnits.AnyAsync(item => item.SerialNumber == serial, cancellationToken)) continue;
                var createdAt = now.AddMonths(-8).AddDays(kitIndex * 3 + unitIndex);
                var unit = ProductUnit.Create(StableGuid($"unit:{serial}"), product.Id, serial, $"QR-{serial}", DemoActorId, createdAt);
                db.ProductUnits.Add(unit);
                if (unitIndex == 2 && kitIndex < 4)
                    SeedRental(db, unit, product, customerEntities[kitIndex], createdAt.AddMonths(6), false, kitIndex == 0);
                else if (unitIndex == 3 && kitIndex < 3)
                    SeedRental(db, unit, product, customerEntities[(kitIndex + 1) % customerEntities.Count], createdAt.AddMonths(2), true, kitIndex == 1);
            }
            kitIndex++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void SeedRental(KitRentalDbContext db, ProductUnit unit, ProductModel product, Customer customer,
        DateTimeOffset start, bool completed, bool includeFault)
    {
        var address = customer.Addresses.First();
        var period = new RentalPeriod(DateOnly.FromDateTime(start.DateTime), DateOnly.FromDateTime(start.AddMonths(2).DateTime));
        var order = RentalOrder.Create(StableGuid($"order:{unit.SerialNumber}:{start:yyyyMMdd}"),
            $"KD-{StableGuid($"order-number:{unit.SerialNumber}"):N}"[..20], customer.Id, period,
            customer.SnapshotAddress(address.Id), start);
        var line = order.AddLine(product.Id, 1);
        order.Submit(DemoActorId, start); order.Approve(DemoActorId, start.AddHours(1));
        var assignment = RentalAssignment.Create(StableGuid($"assignment:{unit.SerialNumber}:{start:yyyyMMdd}"), line.Id,
            customer.Id, unit.Id, period, start, DemoActorId);
        unit.Reserve(DemoActorId, start.AddHours(1));
        order.StartPreparation(DemoActorId, start.AddHours(2)); unit.StartPreparation(DemoActorId, start.AddHours(2));
        order.MarkReadyToShip(DemoActorId, start.AddHours(3)); order.Dispatch(DemoActorId, start.AddHours(4));
        unit.Dispatch(DemoActorId, start.AddHours(4)); order.ConfirmDelivery(DemoActorId, start.AddDays(1));
        unit.ConfirmDelivery(DemoActorId, start.AddDays(1)); order.ActivateRental(DemoActorId, start.AddDays(1)); assignment.Activate();
        db.RentalOrders.Add(order); db.RentalAssignments.Add(assignment);
        if (includeFault)
        {
            var fault = FaultTicket.Open(StableGuid($"fault:{unit.SerialNumber}"), $"ARZ-{unit.SerialNumber[^6..]}",
                customer.Id, order.Id, assignment.Id, unit.Id, "Sensör bağlantısı", FaultSeverity.Medium,
                "Ultrasonik sensör zaman zaman ölçüm vermiyor.", start.AddDays(12));
            fault.ChangeStatus(FaultStatus.Investigating, DemoActorId, start.AddDays(13), "Uzaktan bağlantı kontrolleri yapıldı.");
            fault.ChangeStatus(FaultStatus.Resolved, DemoActorId, start.AddDays(14), "Sensör kablosu değiştirilerek sorun giderildi.");
            db.FaultTickets.Add(fault);
        }
        if (!completed) return;
        var returned = start.AddMonths(2);
        order.RequestReturn(DemoActorId, returned); order.StartReturnShipment(DemoActorId, returned.AddHours(2));
        unit.StartReturn(DemoActorId, returned.AddHours(2)); order.ReceiveReturn(DemoActorId, returned.AddDays(1));
        unit.ReceiveForInspection(DemoActorId, returned.AddDays(1));
        unit.CompleteInspection(ProductUnitStatus.Available, DemoActorId, returned.AddDays(2), "Eksiksiz ve çalışır durumda depoya alındı.");
        order.Complete(DemoActorId, returned.AddDays(2)); assignment.Complete();
    }

    private static async Task AddOpeningStockAsync(
        KitRentalDbContext db,
        Component component,
        StorageLocation location,
        decimal quantity,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0 || await db.ComponentStocks.AnyAsync(
                item => item.ComponentId == component.Id && item.StorageLocationId == location.Id, cancellationToken))
            return;
        var stock = ComponentStock.Create(StableGuid($"stock:{component.Sku}:{location.Code}"), component.Id, location.Id);
        stock.Apply(quantity);
        db.ComponentStocks.Add(stock);
        db.StockMovements.Add(StockMovement.Create(StableGuid($"movement:{component.Sku}:{location.Code}"),
            component.Id, location.Id, StockMovementType.Receipt, quantity, "Demo açılış stoğu",
            DemoActorId, occurredAt));
    }

    private static Guid StableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private sealed record ComponentSeed(string Name, string Sku, string Unit, decimal MinimumStock, string ImageUrl);
    private sealed record LocationSeed(string Code, string Warehouse, string Aisle, string Rack, string Shelf);
    private sealed record KitSeed(string Name, string Sku, string Description, IReadOnlyCollection<(string Sku, int Quantity)> Lines);
}
