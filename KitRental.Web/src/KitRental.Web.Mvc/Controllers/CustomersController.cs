using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager")]
public sealed class CustomersController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(string? query, CancellationToken cancellationToken)
    {
        var customers = await apiClient.GetCustomersAsync(cancellationToken);
        var term = query?.Trim() ?? string.Empty;
        if (term.Length > 0)
            customers = customers.Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Addresses.Any(address => address.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    address.District.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
        var accounts = (await apiClient.GetUsersAsync(cancellationToken))
            .Where(item => item.CustomerId.HasValue).ToArray();
        return View(new CustomersPageViewModel(customers, accounts, term));
    }

    [HttpGet]
    public IActionResult Create() => View("CustomerForm", new CreateCustomerViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCustomerViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateCustomerAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Müşteri ve ilk sipariş adresi oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Müşteri oluşturulamadı.");
        }
        return View("CustomerForm", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var customer = await apiClient.GetCustomerAsync(id, cancellationToken);
        return customer is null ? NotFound() : View("EditCustomer", new CustomerInputViewModel
        { Id = customer.Id, Name = customer.Name, Email = customer.Email, IsActive = customer.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerInputViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.UpdateCustomerAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Müşteri bilgileri güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Müşteri güncellenemedi.");
        }
        return View("EditCustomer", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteCustomerAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Müşteri pasife alındı; geçmiş siparişleri korundu." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateAddress(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await apiClient.GetCustomerAsync(customerId, cancellationToken);
        return customer is null ? NotFound() : View("AddressForm", new CustomerAddressInputViewModel
            { CustomerId = customer.Id, CustomerName = customer.Name });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddress(CustomerAddressInputViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateCustomerAddressAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Sipariş adresi eklendi.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Adres eklenemedi.");
        }
        return View("AddressForm", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditAddress(Guid customerId, Guid id, CancellationToken cancellationToken)
    {
        var customer = await apiClient.GetCustomerAsync(customerId, cancellationToken);
        var address = customer?.Addresses.SingleOrDefault(item => item.Id == id);
        return customer is null || address is null ? NotFound() : View("AddressForm", new CustomerAddressInputViewModel
        {
            CustomerId = customer.Id, CustomerName = customer.Name, Id = address.Id, Title = address.Title,
            ContactName = address.ContactName, Phone = address.Phone, Line1 = address.Line1,
            District = address.District, City = address.City, PostalCode = address.PostalCode
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAddress(CustomerAddressInputViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.UpdateCustomerAddressAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Sipariş adresi güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Adres güncellenemedi.");
        }
        return View("AddressForm", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(Guid customerId, Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteCustomerAddressAsync(customerId, id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Sipariş adresi silindi." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateContactAccount(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await apiClient.GetCustomerAsync(customerId, cancellationToken);
        return customer is null ? NotFound() : View(new CustomerContactAccountViewModel
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Username = customer.Email
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContactAccount(CustomerContactAccountViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateCustomerContactAccountAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = $"{model.FirstName} {model.LastName} için ilgili kişi hesabı oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "İlgili kişi hesabı oluşturulamadı.");
        }
        return View(model);
    }
}
