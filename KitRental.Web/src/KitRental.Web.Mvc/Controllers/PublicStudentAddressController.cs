using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[AllowAnonymous]
[Route("adres")]
public sealed class PublicStudentAddressController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Index(string token, CancellationToken cancellationToken)
    {
        var context = await apiClient.GetPublicStudentAddressContextAsync(token, cancellationToken);
        return context is null ? View("LinkExpired") : View(BuildForm(token, context));
    }

    [HttpPost("{token}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string token, PublicStudentAddressFormViewModel model,
        CancellationToken cancellationToken)
    {
        var context = await apiClient.GetPublicStudentAddressContextAsync(token, cancellationToken);
        if (context is null) return View("LinkExpired");
        model.Token = token;
        model.StudentName = context.StudentName;
        model.GuardianPhone = context.GuardianPhone;
        model.CustomerName = context.CustomerName;
        model.OrderNumber = context.OrderNumber;
        model.ProductName = context.ProductName;
        if (!ModelState.IsValid) return View(model);

        model.AddressLine = BuildAddressLine(model);
        if (model.AddressLine.Length > 1000)
        {
            ModelState.AddModelError(nameof(model.AddressLine), "Adres en fazla 1000 karakter olabilir.");
            return View(model);
        }
        var result = await apiClient.SavePublicStudentAddressAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Adres kaydedilemedi.");
            return View(model);
        }

        ViewData["SuccessTitle"] = "Adres Bilgisi Kaydedildi";
        ViewData["SuccessMessage"] = "Güncel adresiniz sipariş listesine işlendi.";
        return View("Success", model);
    }

    private static PublicStudentAddressFormViewModel BuildForm(string token,
        PublicStudentAddressContextViewModel context) => new()
    {
        Token = token,
        StudentName = context.StudentName,
        GuardianPhone = context.GuardianPhone,
        CustomerName = context.CustomerName,
        OrderNumber = context.OrderNumber,
        ProductName = context.ProductName,
        AddressLine = context.AddressLine ?? string.Empty,
        Latitude = context.Latitude,
        Longitude = context.Longitude
    };

    private static string BuildAddressLine(PublicStudentAddressFormViewModel model)
    {
        var address = model.AddressLine.Trim();
        var city = model.City.Trim();
        var district = model.District.Trim();
        var location = string.Join(" / ", new[] { city, district }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(location) ||
            address.Contains(location, StringComparison.CurrentCultureIgnoreCase))
            return address;
        return $"{location} - {address}";
    }
}
