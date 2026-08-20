using KitRental.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace KitRental.Web.Mvc.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TurkishPhoneAttribute : ValidationAttribute, IClientModelValidator
{
    private const string DefaultMessage = "Telefon numarası 0xxx xxx xx xx formatında olmalıdır.";

    public TurkishPhoneAttribute() : base(DefaultMessage)
    {
    }

    public override bool IsValid(object? value)
    {
        var text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) || TurkishPhoneNumber.IsValid(text);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-turkishphone", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        MergeAttribute(context.Attributes, "data-phone-mask", "tr");
        MergeAttribute(context.Attributes, "placeholder", "0xxx xxx xx xx");
        MergeAttribute(context.Attributes, "inputmode", "tel");
        MergeAttribute(context.Attributes, "autocomplete", "tel");
    }

    public override string FormatErrorMessage(string name) => DefaultMessage;

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key)) attributes.Add(key, value);
    }
}
