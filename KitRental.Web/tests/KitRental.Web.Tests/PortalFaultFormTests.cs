using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KitRental.Web.Mvc.Controllers;
using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Tests;

public sealed class PortalFaultFormTests
{
    [Fact]
    public async Task OpeningFaultFormPrefillsCityDistrictAndStreetFromKitContext()
    {
        using var handler = new FaultContextHandler();
        using var client = CreateClient(handler);
        var controller = new CustomerPortalController(client.ApiClient);

        var result = Assert.IsType<ViewResult>(await controller.NewFault(handler.AssignmentId,
            TestContext.Current.CancellationToken));
        var page = Assert.IsType<PortalFaultRequestPageViewModel>(result.Model);

        Assert.Equal(6, page.Form.CityId);
        Assert.Equal(60, page.Form.DistrictId);
        Assert.Equal("Ankara", page.Form.City);
        Assert.Equal("Çankaya", page.Form.District);
        Assert.Equal("Önceki Sokak 1", page.Form.ReporterAddress);
    }

    [Fact]
    public async Task RejectedSavePreservesEditedFieldsAndSendsRegionSeparatelyFromStreet()
    {
        using var handler = new FaultContextHandler();
        using var client = CreateClient(handler);
        var controller = new CustomerPortalController(client.ApiClient);
        var model = CreateForm(handler.AssignmentId);

        var result = Assert.IsType<ViewResult>(await controller.NewFault(model,
            TestContext.Current.CancellationToken));
        var page = Assert.IsType<PortalFaultRequestPageViewModel>(result.Model);

        Assert.Equal("Yeni Sokak 2", handler.SubmittedAddress);
        Assert.Equal(34, handler.SubmittedCityId);
        Assert.Equal(340, handler.SubmittedDistrictId);
        Assert.Same(model, page.Form);
        Assert.Equal("İstanbul", page.Form.City);
        Assert.Equal("Kadıköy", page.Form.District);
        Assert.Equal("Yeni Sokak 2", page.Form.ReporterAddress);
        Assert.False(controller.ModelState.IsValid);
    }

    [Theory]
    [InlineData(null, 340)]
    [InlineData(34, null)]
    public async Task MissingRegionSelectionDoesNotSubmitFault(int? city, int? district)
    {
        using var handler = new FaultContextHandler();
        using var client = CreateClient(handler);
        var controller = new CustomerPortalController(client.ApiClient);
        var model = CreateForm(handler.AssignmentId);
        model.CityId = city;
        model.DistrictId = district;

        Assert.IsType<ViewResult>(await controller.NewFault(model, TestContext.Current.CancellationToken));

        Assert.Null(handler.SubmittedAddress);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task StreetLengthDoesNotIncludeSeparateCityAndDistrictLabels()
    {
        using var handler = new FaultContextHandler();
        using var client = CreateClient(handler);
        var controller = new CustomerPortalController(client.ApiClient);
        var model = CreateForm(handler.AssignmentId);
        model.ReporterAddress = new string('a', 1000);

        Assert.IsType<ViewResult>(await controller.NewFault(model, TestContext.Current.CancellationToken));

        Assert.Equal(new string('a', 1000), handler.SubmittedAddress);
        Assert.Equal(34, handler.SubmittedCityId);
        Assert.Equal(new string('a', 1000), model.ReporterAddress);
    }

    private static PortalFaultRequestViewModel CreateForm(Guid assignmentId) => new()
    {
        AssignmentId = assignmentId,
        ReporterName = "Test Kullanıcısı",
        ReporterPhone = "05320000000",
        CityId = 34, DistrictId = 340,
        City = "İstanbul",
        District = "Kadıköy",
        ReporterAddress = "Yeni Sokak 2",
        Description = "Kitin motoru çalışmıyor."
    };

    private static ApiClientFixture CreateClient(FaultContextHandler handler) => new(handler);

    private sealed class ApiClientFixture : IDisposable
    {
        private readonly HttpClient _client;

        public ApiClientFixture(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://kit.test") };
            ApiClient = new KitRentalApiClient(_client, new HttpContextAccessor());
        }

        public KitRentalApiClient ApiClient { get; }

        public void Dispose() => _client.Dispose();
    }

    private sealed class FaultContextHandler : HttpMessageHandler
    {
        public Guid AssignmentId { get; } = Guid.NewGuid();
        public string? SubmittedAddress { get; private set; }
        public int? SubmittedCityId { get; private set; }
        public int? SubmittedDistrictId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                using var body = await JsonDocument.ParseAsync(
                    await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                SubmittedAddress = body.RootElement.GetProperty("reporterAddress").GetString();
                SubmittedCityId = body.RootElement.GetProperty("cityId").GetInt32();
                SubmittedDistrictId = body.RootElement.GetProperty("districtId").GetInt32();
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(new ProblemDetails { Detail = "Test kayıt hatası." })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PortalFaultFormContextViewModel(AssignmentId, "Test Kiti", "TEST-1",
                    "Önceki Kullanıcı", "05320000001", "Önceki Sokak 1", 6, 60, "Ankara", "Çankaya"))
            };
        }
    }
}
