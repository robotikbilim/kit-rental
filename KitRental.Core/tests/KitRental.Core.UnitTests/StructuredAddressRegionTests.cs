using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class StructuredAddressRegionTests
{
    [Fact]
    public void CustomerEditAndOrderSnapshotPreserveStructuredRegion()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Test Müşteri", "test@example.com");
        var address = customer.AddAddress("Ev", "Test Kullanıcısı", "05320000000", "Bilim Sokak 1", "34000",
            34, 332, "İstanbul", "Kadıköy");
        customer.UpdateAddress(address.Id, "Ev", "Yeni Kullanıcı", "05320000001", address.Line1, address.PostalCode);
        var snapshot = customer.SnapshotAddress(address.Id);
        var order = RentalOrder.Create(Guid.NewGuid(), "TEST-1", customer.Id,
            new RentalPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)), snapshot, DateTimeOffset.UtcNow);

        Assert.Equal("Bilim Sokak 1", order.DeliveryAddress.Line1);
        Assert.Equal(34, order.DeliveryAddress.CityId);
        Assert.Equal(332, order.DeliveryAddress.DistrictId);
        Assert.Equal("İstanbul", order.DeliveryAddress.City);
        Assert.Equal("Kadıköy", order.DeliveryAddress.District);

        customer.UpdateAddress(address.Id, "Ev", "Yeni Kullanıcı", "05320000001", "Yeni Sokak 2", "06000",
            6, 104, "Ankara", "Çankaya");
        Assert.Equal("İstanbul", order.DeliveryAddress.City);
        Assert.Equal("Bilim Sokak 1", order.DeliveryAddress.Line1);
        Assert.Equal("Ankara", customer.SnapshotAddress(address.Id).City);
    }

    [Fact]
    public void StudentContactEditPreservesRegionAndAnonymizeRemovesIt()
    {
        var cohort = RentalCohort.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Dönemi",
            new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), DateTimeOffset.UtcNow);
        var student = cohort.AddStudent("Ayşe Yılmaz", "05320000000", string.Empty, Guid.NewGuid());
        cohort.UpdateStudentAddressByToken(student.PublicAddressToken, "Bilim Sokak 1", null, null,
            DateTimeOffset.UtcNow, 34, 332, "İstanbul", "Kadıköy");
        cohort.UpdateStudent(student.Id, "Mehmet Kaya", "05320000001", student.AddressLine, student.ProductModelId);

        Assert.Equal("Bilim Sokak 1", student.AddressLine);
        Assert.Equal(34, student.CityId);
        Assert.Equal(332, student.DistrictId);
        Assert.Equal("İstanbul", student.City);
        Assert.Equal("Kadıköy", student.District);

        student.UnassignAndAnonymize();
        Assert.Null(student.CityId);
        Assert.Null(student.DistrictId);
        Assert.Equal(string.Empty, student.City);
        Assert.Equal(string.Empty, student.District);
        Assert.Equal(string.Empty, student.AddressLine);
    }

    [Fact]
    public void FaultShipmentRegionCanBeCorrectedOnlyBeforeProviderCreation()
    {
        var ticket = FaultTicket.Open(Guid.NewGuid(), "FAULT-1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Arıza", FaultSeverity.Medium, "Motor çalışmıyor", DateTimeOffset.UtcNow,
            "Veli", "05320000000", "Bilim Sokak 1", cityId: 34, districtId: 332, city: "İstanbul", district: "Kadıköy");
        var shipment = ticket.CreateKargonomiShipment(FaultKargonomiShipmentDirection.ToCustomer,
            ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress, DateTimeOffset.UtcNow,
            ticket.CityId, ticket.DistrictId, ticket.City, ticket.District);
        ticket.UpdatePublicDetails(ticket.Category, ticket.Description, "Veli", "05320000000", "Yeni Sokak 2",
            null, null, cityId: 6, districtId: 104, city: "Ankara", district: "Çankaya");
        shipment.UpdateRecipientBeforeCreation(ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress,
            ticket.CityId, ticket.DistrictId, ticket.City, ticket.District);

        Assert.Equal("Yeni Sokak 2", shipment.RecipientAddress);
        Assert.Equal(6, shipment.CityId);
        Assert.Equal(104, shipment.DistrictId);
        Assert.Equal("Ankara", shipment.City);
        Assert.Equal("Çankaya", shipment.District);

        shipment.MarkCreated(123, "draft", "Taslak", null, DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => shipment.UpdateRecipientBeforeCreation("Veli", "05320000000",
            "Başka Sokak 3", 35, 351, "İzmir", "Bornova"));
        Assert.Equal(6, shipment.CityId);
        Assert.Equal("Yeni Sokak 2", shipment.RecipientAddress);
    }

    [Fact]
    public void ReturnAndLocationStoreStreetAndRegionIndependently()
    {
        var actorId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var item = new KitReturnItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var request = KitReturnRequest.CreatePublic(Guid.NewGuid(), customerId, DateTimeOffset.UtcNow, actorId,
            [item], "Veli", "05320000000", "Bilim Sokak 1", null, null,
            cityId: 34, districtId: 332, city: "İstanbul", district: "Kadıköy");
        var location = KitLocationEvent.Create(Guid.NewGuid(), item.ProductUnitId, item.AssignmentId, item.OrderId,
            customerId, KitLocationEventSource.ReturnRequest, request.Id, request.RequesterName!, request.RequesterPhone!,
            request.ReturnAddress!, null, null, DateTimeOffset.UtcNow, actorId,
            request.CityId, request.DistrictId, request.City, request.District);

        Assert.Equal("Bilim Sokak 1", location.AddressLine);
        Assert.Equal(34, location.CityId);
        Assert.Equal(332, location.DistrictId);
        Assert.Equal("İstanbul", location.City);
        Assert.Equal("Kadıköy", location.District);
        Assert.Equal("Bilim Sokak 1", request.ReturnAddress);
    }
}
