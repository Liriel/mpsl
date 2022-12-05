using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    public partial class NormalizedName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "ShoppingListItems",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
            
            // fill column before adding the unique constraint
            migrationBuilder.Sql("update ShoppingListItems set NormalizedName = upper(replace(replace(replace(replace(replace(replace(Name,'ä','ae'),'Ä','ae'),'ü','ue'),'Ü','ue'),'ö','oe'),'Ö','oe'));");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "142c0c14-69ba-4ebc-93f5-42961c666243", "8e7a865d-3775-47ae-8e1d-16152941f0ae" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_NormalizedName_ShoppingListId",
                table: "ShoppingListItems",
                columns: new[] { "NormalizedName", "ShoppingListId" },
                unique: true);

            
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_NormalizedName_ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "ShoppingListItems");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "b1922bc1-02f4-4118-93ed-11525e63019e", "22e04eed-6cc3-4ab8-a14a-6e72a905259c" });
        }
    }
}
