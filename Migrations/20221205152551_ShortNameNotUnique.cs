using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    public partial class ShortNameNotUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_ShortName_ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "a48079e8-bc30-4082-951e-424b6a8d081c", "432f9f2f-5de2-4d97-817f-629b727fa8d6" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "b1922bc1-02f4-4118-93ed-11525e63019e", "22e04eed-6cc3-4ab8-a14a-6e72a905259c" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_ShortName_ShoppingListId",
                table: "ShoppingListItems",
                columns: new[] { "ShortName", "ShoppingListId" },
                unique: true);
        }
    }
}
