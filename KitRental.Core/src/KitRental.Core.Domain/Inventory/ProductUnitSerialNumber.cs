using System.Text;

namespace KitRental.Core.Domain.Inventory;

public static class ProductUnitSerialNumber
{
    public static string Create(string productModelSku, DateTimeOffset createdAt, Guid uniqueId)
    {
        var sku = NormalizeSku(productModelSku);
        var suffix = uniqueId.ToString("N").ToUpperInvariant();
        return $"{sku}-{createdAt.Year:D4}-{suffix[..8]}-{suffix[8..16]}";
    }

    private static string NormalizeSku(string sku)
    {
        var result = new StringBuilder();
        var previousWasSeparator = false;
        foreach (var character in sku.Trim().ToUpperInvariant())
        {
            var normalizedCharacter = character switch
            {
                'Ç' => 'C',
                'Ğ' => 'G',
                'İ' => 'I',
                'ı' => 'I',
                'Ö' => 'O',
                'Ş' => 'S',
                'Ü' => 'U',
                _ => character
            };
            if (char.IsAsciiLetterOrDigit(normalizedCharacter))
            {
                result.Append(normalizedCharacter);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && result.Length > 0)
            {
                result.Append('-');
                previousWasSeparator = true;
            }

            if (result.Length >= 32)
                break;
        }

        return result.ToString().Trim('-') is { Length: > 0 } normalized ? normalized : "KIT";
    }
}
