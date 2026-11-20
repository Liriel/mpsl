using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    /// <summary>
    /// Bundles the data cleanup behind improvements I1-I3 with the unique indexes that keep it clean.
    /// All cleanup SQL runs BEFORE the indexes are created, otherwise creating them would fail on the
    /// very rows being cleaned up.
    /// </summary>
    /// <remarks>
    /// Down() only drops the indexes - deleted duplicates and trimmed names are not restorable.
    /// </remarks>
    public partial class ArchiveRaceNameTrimAndPushEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- I2: units - trim, merging whitespace twins ('750g' / '750g ') ----
            migrationBuilder.Sql($@"
CREATE TEMP TABLE _unit_clean AS
SELECT Id, {Clean("ShortName")} AS CShort FROM Units;");
            migrationBuilder.Sql(@"
CREATE TEMP TABLE _unit_map AS
SELECT u.Id AS TwinId, (SELECT MIN(o.Id) FROM _unit_clean o WHERE o.CShort = u.CShort) AS KeepId
FROM _unit_clean u
WHERE u.Id <> (SELECT MIN(o.Id) FROM _unit_clean o WHERE o.CShort = u.CShort);");
            migrationBuilder.Sql("UPDATE ShoppingListItems SET UnitId = (SELECT KeepId FROM _unit_map WHERE TwinId = UnitId) WHERE UnitId IN (SELECT TwinId FROM _unit_map);");
            migrationBuilder.Sql("UPDATE ItemHistories SET UnitId = (SELECT KeepId FROM _unit_map WHERE TwinId = UnitId) WHERE UnitId IN (SELECT TwinId FROM _unit_map);");
            migrationBuilder.Sql("DELETE FROM Units WHERE Id IN (SELECT TwinId FROM _unit_map);");
            migrationBuilder.Sql($"UPDATE Units SET ShortName = {Clean("ShortName")}, Name = {Clean("Name")} WHERE ShortName <> {Clean("ShortName")} OR Name <> {Clean("Name")};");
            migrationBuilder.Sql("DROP TABLE _unit_map; DROP TABLE _unit_clean;");

            // ---- I2: items - trim names, merging collisions ('Klopapier ' into 'Klopapier') ----
            // NormalizedName is derived from the stored one rather than recomputed from Name: the umlaut
            // replacement and upper-casing are whitespace-independent, and SQLite's UPPER() only handles
            // ASCII, so cleaning the existing value is the exact equivalent of NameHelper.GetNormalizedName.
            migrationBuilder.Sql($@"
CREATE TEMP TABLE _item_clean AS
SELECT Id, ShoppingListId, {Clean("Name")} AS CName, {Clean("NormalizedName")} AS CNorm FROM ShoppingListItems;");
            // survivor = oldest row (lowest Id) of each (list, cleaned name) group
            migrationBuilder.Sql(@"
CREATE TEMP TABLE _item_map AS
SELECT i.Id AS TwinId, (SELECT MIN(o.Id) FROM _item_clean o WHERE o.ShoppingListId = i.ShoppingListId AND o.CName = i.CName) AS KeepId
FROM _item_clean i
WHERE i.Id <> (SELECT MIN(o.Id) FROM _item_clean o WHERE o.ShoppingListId = i.ShoppingListId AND o.CName = i.CName);");
            // the survivor stays on the list if either row was still wanted (ItemState.Open = 1)
            migrationBuilder.Sql("UPDATE ShoppingListItems SET Status = 1, CheckDate = NULL WHERE Id IN (SELECT KeepId FROM _item_map) AND EXISTS (SELECT 1 FROM _item_map m JOIN ShoppingListItems t ON t.Id = m.TwinId WHERE m.KeepId = ShoppingListItems.Id AND t.Status = 1);");
            migrationBuilder.Sql("UPDATE ItemHistories SET ShoppingListItemId = (SELECT KeepId FROM _item_map WHERE TwinId = ShoppingListItemId) WHERE ShoppingListItemId IN (SELECT TwinId FROM _item_map);");
            migrationBuilder.Sql("DELETE FROM ShoppingListItems WHERE Id IN (SELECT TwinId FROM _item_map);");
            migrationBuilder.Sql("UPDATE ShoppingListItems SET Name = (SELECT CName FROM _item_clean c WHERE c.Id = ShoppingListItems.Id), NormalizedName = (SELECT CNorm FROM _item_clean c WHERE c.Id = ShoppingListItems.Id) WHERE Id IN (SELECT Id FROM _item_clean WHERE CName <> (SELECT Name FROM ShoppingListItems s WHERE s.Id = _item_clean.Id) OR CNorm <> (SELECT NormalizedName FROM ShoppingListItems s WHERE s.Id = _item_clean.Id));");
            migrationBuilder.Sql("DROP TABLE _item_map; DROP TABLE _item_clean;");

            // ---- I1: duplicate history rows (archive race, and any created by the merge above) ----
            // keep the lowest Id per (item, CheckDate)
            migrationBuilder.Sql("DELETE FROM ItemHistories WHERE Id NOT IN (SELECT MIN(Id) FROM ItemHistories GROUP BY ShoppingListItemId, CheckDate);");

            // ---- I3: dead and duplicate push subscriptions ----
            // host = text between '://' and the next '/'; '.invalid' is the reserved TLD Chrome writes
            // into retired FCM subscriptions
            migrationBuilder.Sql(@"
WITH rest AS (SELECT Id, substr(Endpoint, instr(Endpoint, '://') + 3) AS R FROM PushSubscriptions),
     host AS (SELECT Id, lower(substr(R, 1, instr(R || '/', '/') - 1)) AS H FROM rest)
DELETE FROM PushSubscriptions WHERE Id IN (SELECT Id FROM host WHERE H LIKE '%.invalid' OR H LIKE '%.invalid:%' OR H = 'invalid');");
            // keep the newest row per endpoint
            migrationBuilder.Sql(@"
DELETE FROM PushSubscriptions WHERE EXISTS (
    SELECT 1 FROM PushSubscriptions o
    WHERE o.Endpoint = PushSubscriptions.Endpoint
      AND (o.CreatedAt > PushSubscriptions.CreatedAt OR (o.CreatedAt = PushSubscriptions.CreatedAt AND o.Id > PushSubscriptions.Id)));");

            migrationBuilder.DropIndex(
                name: "IX_ItemHistories_ShoppingListItemId",
                table: "ItemHistories");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemHistories_ShoppingListItemId_CheckDate",
                table: "ItemHistories",
                columns: new[] { "ShoppingListItemId", "CheckDate" },
                unique: true);
        }

        /// <summary>
        /// SQL expression that trims a column and collapses internal whitespace runs (space, tab, CR,
        /// LF) to one space, like <see cref="mps.Infrastructure.NameHelper.CollapseWhitespace"/>.
        /// Six doubling passes cover runs of up to 64 characters, longer than any 50 character name.
        /// </summary>
        private static string Clean(string column)
        {
            var expr = $"REPLACE(REPLACE(REPLACE({column}, char(9), ' '), char(10), ' '), char(13), ' ')";
            for (var i = 0; i < 6; i++)
                expr = $"REPLACE({expr}, '  ', ' ')";
            return $"TRIM({expr}, ' ')";
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_ItemHistories_ShoppingListItemId_CheckDate",
                table: "ItemHistories");

            migrationBuilder.CreateIndex(
                name: "IX_ItemHistories_ShoppingListItemId",
                table: "ItemHistories",
                column: "ShoppingListItemId");
        }
    }
}
