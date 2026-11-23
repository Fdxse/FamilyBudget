using System.Globalization;

namespace FamilyBudget.Web.Services;

/// <summary>Reads amounts the way people type them: "1234", "1 234,50", "1.234,50", "1234.5 kr".</summary>
public static class AmountParser
{
    public static bool TryParse(string? typedText, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(typedText))
        {
            return false;
        }

        // Drop "kr", spaces and the non-breaking spaces that Swedish number formatting uses.
        var cleanedText = typedText.Trim().ToLowerInvariant()
            .Replace("kr", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty);

        // With a comma present, the comma is the decimal sign and any dots are thousand separators.
        if (cleanedText.Contains(','))
        {
            cleanedText = cleanedText.Replace(".", string.Empty).Replace(',', '.');
        }

        if (!decimal.TryParse(cleanedText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        // Amounts are stored as positive numbers; "-500" means the same as "500".
        amount = Math.Abs(amount);
        return true;
    }
}
