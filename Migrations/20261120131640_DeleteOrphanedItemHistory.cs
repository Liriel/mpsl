using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    /// <inheritdoc />
    public partial class DeleteOrphanedItemHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Production had 109 ItemHistories rows whose item no longer exists - left over from
            // deletes that ran without foreign-key enforcement, so the ON DELETE CASCADE never fired.
            // Every reader reaches history through its item (see CLAUDE.md, De-clutter), so these
            // rows are unreachable; delete them so `PRAGMA foreign_key_check` comes back clean.
            migrationBuilder.Sql(
                "DELETE FROM ItemHistories WHERE ShoppingListItemId NOT IN (SELECT Id FROM ShoppingListItems);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only and irreversible: the deleted rows had no parent and cannot be restored.
        }
    }
}
