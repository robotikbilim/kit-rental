using PhoneNumbers;

namespace KitRental.SharedKernel;

public static class TurkishPhoneNumber
{
    private const string Region = "TR";
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    public static string Normalize(string value, string fieldName = "Telefon numarası")
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("phone.required", $"{fieldName} zorunludur.");

        try
        {
            var number = PhoneUtil.Parse(value.Trim(), Region);
            if (!PhoneUtil.IsValidNumberForRegion(number, Region))
                throw new DomainException("phone.invalid_tr", $"{fieldName} Türkiye telefon numarası formatında olmalıdır.");

            return PhoneUtil.Format(number, PhoneNumberFormat.NATIONAL);
        }
        catch (NumberParseException)
        {
            throw new DomainException("phone.invalid_tr", $"{fieldName} Türkiye telefon numarası formatında olmalıdır.");
        }
    }

    public static string NormalizeOptional(string? value, string fieldName = "Telefon numarası") =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Normalize(value, fieldName);

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            var number = PhoneUtil.Parse(value.Trim(), Region);
            return PhoneUtil.IsValidNumberForRegion(number, Region);
        }
        catch (NumberParseException)
        {
            return false;
        }
    }
}
