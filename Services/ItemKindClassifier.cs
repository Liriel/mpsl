using mps.Infrastructure;
using mps.Model;

namespace mps.Services;

/// <summary>Decides whether a typed name is a product or a task (errand / "go to that shop" reminder).</summary>
public interface IItemKindClassifier
{
    /// <summary>
    /// Classifies <paramref name="name"/>. <paramref name="otherListNames"/> are the names of the
    /// other lists: an item named like one of them is a reminder to go there.
    /// </summary>
    (ItemKind Kind, string Reason) Classify(string name, IEnumerable<string> otherListNames);
}

public class ItemKindClassifier : IItemKindClassifier
{
    /// <summary>Typed-name prefix that forces a task.</summary>
    public const string TaskPrefix = "!";

    // matched against NameHelper.GetMatchKey (lower case, umlauts spelled out, letters/digits only).
    // The Up of migration AddItemKind mirrors these in SQL - keep them in sync.
    private static readonly string[] ContainsWords =
        { "packen", "tanken", "abholen", "bringen", "anrufen", "zahlen", "termin", "erledigen", "schularbeit" };

    private static readonly string[] Suffixes = { "zeiten", "plaene" };

    public (ItemKind Kind, string Reason) Classify(string name, IEnumerable<string> otherListNames)
    {
        var key = NameHelper.GetMatchKey(name);
        if (key.Length == 0)
            return (ItemKind.Product, null);

        foreach (var word in ContainsWords)
            if (key.Contains(word, StringComparison.Ordinal))
                return (ItemKind.Task, $"contains '{word}'");

        foreach (var suffix in Suffixes)
            if (key.EndsWith(suffix, StringComparison.Ordinal))
                return (ItemKind.Task, $"ends with '{suffix}'");

        if (otherListNames != null && otherListNames.Any(l => NameHelper.GetMatchKey(l) == key))
            return (ItemKind.Task, "named like another list");

        return (ItemKind.Product, null);
    }

    /// <summary>
    /// Strips a leading <c>!</c> (and the whitespace around the rest) from a typed name. Returns
    /// whether the prefix was present.
    /// </summary>
    public static bool StripTaskPrefix(ref string name)
    {
        if (name == null)
            return false;

        var trimmed = name.TrimStart();
        if (!trimmed.StartsWith(TaskPrefix, StringComparison.Ordinal))
            return false;

        name = trimmed.Substring(TaskPrefix.Length).Trim();
        return true;
    }
}
