using System.Text.RegularExpressions;

namespace mps.Infrastructure;

/// <summary>
/// Splits quantities typed into the unit field ("500g", "2x", "6er", "3") into an amount and a
/// unit. Used by <c>ShoppingListController.AddItem</c> and <c>ItemController.PostMapping</c>
/// before the unit is looked up, so the unit table stops collecting quantities.
/// </summary>
public static class UnitParser
{
    private static readonly Regex QuantityPattern =
        new(@"^(\d+)\s*([a-zA-ZäöüÄÖÜ]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Splits <paramref name="unitText"/> only when <paramref name="amount"/> is empty (null or 0)
    /// and the text starts with digits. The returned unit is "" (clears the unit) for the
    /// count markers <c>x</c>, <c>er</c> and for a bare number; anything else is returned unchanged.
    /// </summary>
    public static (int? Amount, string Unit) Split(int? amount, string unitText)
    {
        if ((amount ?? 0) != 0 || unitText == null)
            return (amount, unitText);

        var match = QuantityPattern.Match(unitText.Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsed))
            return (amount, unitText);

        var suffix = match.Groups[2].Value;
        var isCountMarker = suffix.Length == 0 ||
            suffix.Equals("x", StringComparison.OrdinalIgnoreCase) ||
            suffix.Equals("er", StringComparison.OrdinalIgnoreCase);

        return (parsed, isCountMarker ? string.Empty : suffix);
    }

    /// <summary>
    /// A unit needs at least one letter; a short name made only of digits and punctuation
    /// ("10", "...", "&amp;") is junk.
    /// </summary>
    public static bool IsPlausibleUnit(string shortName) =>
        !string.IsNullOrWhiteSpace(shortName) && shortName.Any(char.IsLetter);
}
