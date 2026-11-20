using Microsoft.EntityFrameworkCore;
using mps.Infrastructure;
using mps.Model;

namespace mps.Services;

/// <summary>An item of one list together with the facts the matcher ranks by.</summary>
public record ItemMatch(int Id, string Name, string ShortName, ItemState Status, int PurchaseCount, DateTime? LastPurchaseDate);

/// <summary>A set of items that probably are one and the same thing.</summary>
public record DuplicateGroup(IReadOnlyList<ItemMatch> Items);

/// <summary>
/// The one place that decides whether two item names mean the same thing, so the add-time
/// "did you mean" suggestion and the merge tool's duplicate groups always agree.
/// </summary>
public interface IItemMatchService
{
    /// <summary>
    /// Existing items that are near <paramref name="name"/> but not an exact
    /// <c>NormalizedName</c> match, ranked by purchase count. Empty when an exact match exists.
    /// </summary>
    IReadOnlyList<ItemMatch> Suggest(int shoppingListId, string name, int take = 3);

    /// <summary>Ids of items whose match key contains the match key of <paramref name="pattern"/>.</summary>
    IReadOnlyList<int> FindByMatchKey(int shoppingListId, string pattern);

    /// <summary>Suggested duplicate groups of the list (two or more items each).</summary>
    IReadOnlyList<DuplicateGroup> FindDuplicateGroups(int shoppingListId);
}

public class ItemMatchService : IItemMatchService
{
    /// <summary>Transitive clusters larger than this fall back to star clustering.</summary>
    public const int MaxGroupSize = 6;

    private static readonly string[] PluralSuffixes = { "en", "er", "n", "s", "e" };

    private readonly IRepository repo;

    public ItemMatchService(IRepository repo)
    {
        this.repo = repo;
    }

    // ---- the matching rules (pure, unit-tested) ----

    /// <summary>
    /// Whether two match keys name the same thing: identical, identical after stripping a plural
    /// suffix, or within a small edit distance (1 for keys of 5-7 letters, 2 from 8 on). Substring
    /// matching is deliberately absent: Wein/Portwein must stay apart.
    /// </summary>
    public static bool AreNear(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
            return false;

        if (a == b)
            return true;

        // "Pizza 1" and "Pizza 2" differ in one character but are different things
        if (!DigitsEqual(a, b))
            return false;

        if (StripPlural(a) == StripPlural(b))
            return true;

        var shorter = Math.Min(a.Length, b.Length);
        if (shorter < 5)
            return false;

        var allowed = shorter >= 8 ? 2 : 1;
        if (Math.Abs(a.Length - b.Length) > allowed)
            return false;

        return EditDistance(a, b, allowed) <= allowed;
    }

    internal static string StripPlural(string key)
    {
        if (key.Length <= 4)
            return key;

        foreach (var suffix in PluralSuffixes)
        {
            if (key.EndsWith(suffix, StringComparison.Ordinal))
                return key.Substring(0, key.Length - suffix.Length);
        }

        return key;
    }

    private static bool DigitsEqual(string a, string b) =>
        new string(a.Where(char.IsDigit).ToArray()) == new string(b.Where(char.IsDigit).ToArray());

    /// <summary>Levenshtein distance with an early exit once every cell exceeds <paramref name="max"/>.</summary>
    internal static int EditDistance(string a, string b, int max)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            var rowMin = curr[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, curr[j]);
            }

            if (rowMin > max)
                return max + 1;

            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    // ---- list-level operations ----

    private List<(ItemMatch Item, string Key, string Normalized)> Load(int shoppingListId)
    {
        // ~800 rows per list: one grouped query, matching happens in memory
        var rows = (from i in this.repo.ShoppingListItems
                    where i.ShoppingListId == shoppingListId
                    select new
                    {
                        i.Id,
                        i.Name,
                        i.NormalizedName,
                        i.ShortName,
                        i.Status,
                        Count = i.History.Count(),
                        Last = i.History.Select(h => (DateTime?)h.CheckDate).Max()
                    }).AsNoTracking().ToList();

        return rows.Select(r => (new ItemMatch(r.Id, r.Name, r.ShortName, r.Status, r.Count, r.Last),
            NameHelper.GetMatchKey(r.Name), r.NormalizedName)).ToList();
    }

    public IReadOnlyList<ItemMatch> Suggest(int shoppingListId, string name, int take = 3)
    {
        var key = NameHelper.GetMatchKey(name);
        if (key.Length == 0)
            return Array.Empty<ItemMatch>();

        var normalized = NameHelper.GetNormalizedName(name);
        var items = Load(shoppingListId);

        if (items.Any(i => i.Normalized == normalized))
            return Array.Empty<ItemMatch>();

        return items.Where(i => AreNear(key, i.Key))
            .Select(i => i.Item)
            .OrderByDescending(i => i.PurchaseCount)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    public IReadOnlyList<int> FindByMatchKey(int shoppingListId, string pattern)
    {
        var key = NameHelper.GetMatchKey(pattern);
        if (key.Length == 0)
            return Array.Empty<int>();

        return (from i in this.repo.ShoppingListItems
                where i.ShoppingListId == shoppingListId
                select new { i.Id, i.Name }).AsNoTracking().ToList()
            .Where(i => NameHelper.GetMatchKey(i.Name).Contains(key, StringComparison.Ordinal))
            .Select(i => i.Id)
            .ToList();
    }

    public IReadOnlyList<DuplicateGroup> FindDuplicateGroups(int shoppingListId)
    {
        var items = Load(shoppingListId);
        var n = items.Count;

        // adjacency by pairwise comparison; the length pre-check inside AreNear keeps this cheap
        var near = new List<int>[n];
        for (var i = 0; i < n; i++)
            near[i] = new List<int>();

        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (!AreNear(items[i].Key, items[j].Key))
                    continue;

                near[i].Add(j);
                near[j].Add(i);
                parent[Find(i)] = Find(j);
            }
        }

        var components = Enumerable.Range(0, n)
            .Where(i => near[i].Count > 0)
            .GroupBy(Find)
            .Select(g => g.ToList());

        var groups = new List<List<int>>();
        foreach (var component in components)
        {
            if (component.Count <= MaxGroupSize)
            {
                groups.Add(component);
                continue;
            }

            // a long chain (a~b~c~d...) is not one thing: let the best-established item of the
            // cluster claim only those items that are directly near it
            var remaining = component.OrderByDescending(i => items[i].Item.PurchaseCount).ThenBy(i => i).ToList();
            var taken = new HashSet<int>();
            foreach (var leader in remaining)
            {
                if (taken.Contains(leader))
                    continue;

                var group = new List<int> { leader };
                foreach (var other in near[leader])
                {
                    if (!taken.Contains(other) && group.Count < MaxGroupSize)
                        group.Add(other);
                }

                taken.UnionWith(group);
                if (group.Count > 1)
                    groups.Add(group);
            }
        }

        return groups
            .Select(g => new DuplicateGroup(g.Select(i => items[i].Item)
                .OrderByDescending(i => i.PurchaseCount).ThenBy(i => i.Id).ToList()))
            .OrderByDescending(g => g.Items.Sum(i => i.PurchaseCount))
            .ToList();
    }
}
