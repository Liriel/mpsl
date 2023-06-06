using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    public partial class LastChangedBy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddedByUserId",
                table: "ShoppingListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckedByUserId",
                table: "ShoppingListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeByUserId",
                table: "ShoppingListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChangedDate",
                table: "ShoppingListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "ShoppingListItems",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "bc8c97bc-9403-46ec-a860-6874fd750ca4", "b91ede7a-21f5-4d52-a5cd-6bc41e6358b5" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_AddedByUserId",
                table: "ShoppingListItems",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_CheckedByUserId",
                table: "ShoppingListItems",
                column: "CheckedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_LastChangeByUserId",
                table: "ShoppingListItems",
                column: "LastChangeByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_AddedByUserId",
                table: "ShoppingListItems",
                column: "AddedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_CheckedByUserId",
                table: "ShoppingListItems",
                column: "CheckedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_LastChangeByUserId",
                table: "ShoppingListItems",
                column: "LastChangeByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_AddedByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_CheckedByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_AspNetUsers_LastChangeByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_AddedByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_CheckedByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_LastChangeByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "AddedByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "CheckedByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "LastChangeByUserId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "LastChangedDate",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "ShoppingListItems");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "a48079e8-bc30-4082-951e-424b6a8d081c", "432f9f2f-5de2-4d97-817f-629b727fa8d6" });
        }
    }
}
