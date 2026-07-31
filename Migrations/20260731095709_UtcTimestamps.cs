using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    /// <summary>
    /// Backfills every locally-stamped (Europe/Berlin, DST-aware) timestamp to UTC. Pairs with the
    /// <c>DateTime.Now</c> -&gt; <c>DateTime.UtcNow</c> fixes in <c>PersistedRepository</c>,
    /// <c>DeclutterService</c>, <c>ItemController</c> and <c>ShoppingListController</c>, and with the
    /// <see cref="mps.Model.UtcDateTimeConverter"/> registered in
    /// <see cref="mps.Model.DataContext.ConfigureConventions"/>, which is what makes every future
    /// write/read round-trip as UTC.
    /// </summary>
    /// <remarks>
    /// <c>PushSubscriptions.CreatedAt</c> is deliberately not touched here - it has always been
    /// written with <c>DateTime.UtcNow</c>. The ambiguous October changeover hour (local 02:00-03:00,
    /// which occurs twice) is resolved as CEST, i.e. shifted back 2 hours - see
    /// <see cref="mps.Migrations.UtcBackfill.BuildForwardUpdate"/>.
    /// </remarks>
    /// <inheritdoc />
    public partial class UtcTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UtcBackfill.BuildForwardUpdate("ShoppingListItems", "AddDate"));
            migrationBuilder.Sql(UtcBackfill.BuildForwardUpdate("ShoppingListItems", "CheckDate"));
            migrationBuilder.Sql(UtcBackfill.BuildForwardUpdate("ShoppingListItems", "LastChangedDate"));
            migrationBuilder.Sql(UtcBackfill.BuildForwardUpdate("ItemHistories", "CheckDate"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UtcBackfill.BuildReverseUpdate("ShoppingListItems", "AddDate"));
            migrationBuilder.Sql(UtcBackfill.BuildReverseUpdate("ShoppingListItems", "CheckDate"));
            migrationBuilder.Sql(UtcBackfill.BuildReverseUpdate("ShoppingListItems", "LastChangedDate"));
            migrationBuilder.Sql(UtcBackfill.BuildReverseUpdate("ItemHistories", "CheckDate"));
        }
    }
}
