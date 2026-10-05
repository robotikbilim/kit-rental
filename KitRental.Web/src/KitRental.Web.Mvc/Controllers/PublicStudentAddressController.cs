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
        if (model.CityId is null or <= 0)
            ModelState.AddModelError(nameof(model.CityId), "Lütfen il seçin.");
        if (model.DistrictId is null or <= 0)
            ModelState.AddModelError(nameof(model.DistrictId), "Lütfen ilçe seçin.");
        if (!ModelState.IsValid) return View(model);

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
        PublicStudentAddressContextViewModel context)
    {
        return new()
        {
            Token = token,
            StudentName = context.StudentName,
            GuardianPhone = context.GuardianPhone,
            CustomerName = context.CustomerName,
            OrderNumber = context.OrderNumber,
            ProductName = context.ProductName,
            CityId = context.CityId, DistrictId = context.DistrictId,
            City = context.City, District = context.District,
            AddressLine = context.AddressLine ?? string.Empty,
            Latitude = context.Latitude,
            Longitude = context.Longitude
        };
    }

}
