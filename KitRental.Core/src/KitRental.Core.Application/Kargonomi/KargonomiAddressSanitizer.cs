using System.Globalization;
using System.Text;

namespace KitRental.Core.Application.Kargonomi;

public static class KargonomiAddressSanitizer
{
    public static string Clean(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var result = new StringBuilder(address.Length);
        var pendingSpace = false;
        foreach (var rune in address.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (!IsAllowed(rune))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace) result.Append(' ');
            result.Append(rune.ToString());
            pendingSpace = false;
        }

        return result.ToString().Trim();
    }

    private static bool IsAllowed(Rune rune)
    {
        var value = rune.Value;
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter)
            return IsLatinLetter(value);

        if (category == UnicodeCategory.DecimalDigitNumber) return value is >= '0' and <= '9';

        return value is >= 0x21 and <= 0x7E && category is
            UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation;
    }

    private static bool IsLatinLetter(int value) =>
        value is >= 0x0041 and <= 0x024F or
            >= 0x1E00 and <= 0x1EFF or
            >= 0x2C60 and <= 0x2C7F or
            >= 0xA720 and <= 0xA7FF or
            >= 0xAB30 and <= 0xAB6F or
            >= 0xFB00 and <= 0xFB06;
}
