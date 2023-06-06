using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    public partial class LastChangedBy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_NormalizedName_ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.AddColumn<string>(
                name: "LastChangeByUserId",
                table: "Units",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeByUserId",
                table: "ShoppingLists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeByUserId",
                table: "ShoppingListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "32403c07-e1b3-4165-a56e-a4b6bdc58711", "fcf6fd07-4f04-448d-8e79-a034b515df6a" });

            migrationBuilder.CreateIndex(
                name: "IX_Units_LastChangeByUserId",
                table: "Units",
                column: "LastChangeByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_LastChangeByUserId",
                table: "ShoppingLists",
                column: "LastChangeByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_LastChangeByUserId",
                table: "ShoppingListItems",
                column: "LastChangeByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_LastChangeByUserId",
                table: "ShoppingListItems",
                column: "LastChangeByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingLists_AspNetUsers_LastChangeByUserId",
                table: "ShoppingLists",
                column: "LastChangeByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Units_AspNetUsers_LastChangeByUserId",
                table: "Units",
                column: "LastChangeByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_LastChangeByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingLists_AspNetUsers_LastChangeByUserId",
                table: "ShoppingLists");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_AspNetUsers_LastChangeByUserId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_LastChangeByUserId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingLists_LastChangeByUserId",
                table: "ShoppingLists");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_LastChangeByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "LastChangeByUserId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LastChangeByUserId",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "LastChangeByUserId",
                table: "ShoppingListItems");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "a48079e8-bc30-4082-951e-424b6a8d081c", "432f9f2f-5de2-4d97-817f-629b727fa8d6" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_NormalizedName_ShoppingListId",
                table: "ShoppingListItems",
                columns: new[] { "NormalizedName", "ShoppingListId" },
                unique: true);
        }
    }
}
