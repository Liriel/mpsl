using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    /// <summary>
    /// I6: the unit field was used for quantities ('500g', '2x', '6er', '10') and junk ('.', '&amp;').
    /// (a) split numeric-prefixed units into amount + canonical unit where the row has no amount,
    /// (b) clear units made only of digits/punctuation, (c) delete unreferenced units, (d) give the
    /// canonical units their long name.
    /// </summary>
    /// <remarks>
    /// A row that already has an amount keeps it - its numeric unit is just cleared in (b), the unit
    /// text is never allowed to overwrite a deliberately entered amount. A prefix of 0 ('0p') is not
    /// a quantity and is left alone. Letters are the ASCII ones plus German umlauts, mirroring
    /// <c>UnitParser</c>. Units that are canonical (g, kg, dag, l, ml, Stk, Pkg, Kiste, Sc) or carry
    /// a long name are kept even when unreferenced, so the autocomplete keeps its core vocabulary.
    /// Down() is a no-op: the old junk is not restorable.
    /// </remarks>
    public partial class UnitCleanup : Migration
    {
        private const string Letters = "a-zA-ZäöüÄÖÜ";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- (a) split '500g' -> 500 + g ----
            // digit prefix length via recursive CTE (SQLite has no regex)
            migrationBuilder.Sql($@"
CREATE TEMP TABLE _unit_split AS
WITH RECURSIVE d(Id, S, n) AS (
    SELECT Id, ShortName, 0 FROM Units WHERE ShortName GLOB '[0-9]*'
    UNION ALL
    SELECT Id, S, n + 1 FROM d WHERE substr(S, n + 1, 1) GLOB '[0-9]'
),
p AS (
    SELECT Id, S, MAX(n) AS Len FROM d GROUP BY Id, S
),
q AS (
    SELECT Id, CAST(substr(S, 1, Len) AS INTEGER) AS Num, trim(substr(S, Len + 1)) AS Suffix
    FROM p WHERE Len <= 9
)
SELECT Id, Num,
       CASE WHEN lower(Suffix) IN ('', 'x', 'er') OR length(Suffix) > 5 THEN NULL ELSE lower(Suffix) END AS Target,
       CAST(NULL AS INTEGER) AS TargetId
FROM q
WHERE Num > 0 AND Suffix NOT GLOB '*[^{Letters}]*';");

            // create the canonical unit when no case-insensitive match exists yet
            migrationBuilder.Sql(@"
INSERT INTO Units (ShortName, Name)
SELECT DISTINCT s.Target, s.Target FROM _unit_split s
WHERE s.Target IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Units u WHERE lower(u.ShortName) = s.Target);");

            // prefer the exact lower-case unit, otherwise the oldest case-insensitive match
            migrationBuilder.Sql(@"
UPDATE _unit_split SET TargetId = (
    SELECT u.Id FROM Units u WHERE lower(u.ShortName) = _unit_split.Target
    ORDER BY (u.ShortName = _unit_split.Target) DESC, u.Id LIMIT 1)
WHERE Target IS NOT NULL;");

            migrationBuilder.Sql(@"
UPDATE ShoppingListItems SET
    Amount = (SELECT Num FROM _unit_split WHERE Id = ShoppingListItems.UnitId),
    UnitId = (SELECT TargetId FROM _unit_split WHERE Id = ShoppingListItems.UnitId)
WHERE (Amount IS NULL OR Amount = 0) AND UnitId IN (SELECT Id FROM _unit_split);");
            migrationBuilder.Sql(@"
UPDATE ItemHistories SET
    Amount = (SELECT Num FROM _unit_split WHERE Id = ItemHistories.UnitId),
    UnitId = (SELECT TargetId FROM _unit_split WHERE Id = ItemHistories.UnitId)
WHERE (Amount IS NULL OR Amount = 0) AND UnitId IN (SELECT Id FROM _unit_split);");
            // rows that already had an amount keep it; their quantity-style unit is simply cleared
            migrationBuilder.Sql("UPDATE ShoppingListItems SET UnitId = NULL WHERE UnitId IN (SELECT Id FROM _unit_split);");
            migrationBuilder.Sql("UPDATE ItemHistories SET UnitId = NULL WHERE UnitId IN (SELECT Id FROM _unit_split);");
            migrationBuilder.Sql("DROP TABLE _unit_split;");

            // ---- (b) no unit made only of digits/punctuation ----
            var noLetter = $"ShortName NOT GLOB '*[{Letters}]*'";
            migrationBuilder.Sql($"UPDATE ShoppingListItems SET UnitId = NULL WHERE UnitId IN (SELECT Id FROM Units WHERE {noLetter});");
            migrationBuilder.Sql($"UPDATE ItemHistories SET UnitId = NULL WHERE UnitId IN (SELECT Id FROM Units WHERE {noLetter});");

            // ---- (c) delete what nobody references (but keep canonical / named units) ----
            migrationBuilder.Sql(@"
DELETE FROM Units
WHERE Id NOT IN (SELECT UnitId FROM ShoppingListItems WHERE UnitId IS NOT NULL)
  AND Id NOT IN (SELECT UnitId FROM ItemHistories WHERE UnitId IS NOT NULL)
  AND ShortName NOT IN ('g', 'kg', 'dag', 'l', 'ml', 'Stk', 'Pkg', 'Kiste', 'Sc')
  AND (Name IS NULL OR Name = '' OR Name = ShortName);");

            // ---- (d) long names for the canonical units ----
            migrationBuilder.Sql(@"
UPDATE Units SET Name = CASE ShortName
    WHEN 'g' THEN 'Gramm' WHEN 'kg' THEN 'Kilogramm' WHEN 'dag' THEN 'Dekagramm'
    WHEN 'l' THEN 'Liter' WHEN 'ml' THEN 'Milliliter' WHEN 'Stk' THEN 'Stück'
    WHEN 'Pkg' THEN 'Packung' WHEN 'Kiste' THEN 'Kiste' WHEN 'Sc' THEN 'Scheibe' END
WHERE ShortName IN ('g', 'kg', 'dag', 'l', 'ml', 'Stk', 'Pkg', 'Kiste', 'Sc')
  AND (Name IS NULL OR Name = '' OR Name = ShortName);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
