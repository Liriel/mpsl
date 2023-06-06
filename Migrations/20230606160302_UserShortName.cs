using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    public partial class UserShortName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowShortnameInList",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "90fa7c0d-fed5-4b49-bf0e-40dc51e9f884", "f5f731cc-43b3-4140-9145-1603a91bfd21" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ShowShortnameInList",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "00000000-0000-0000-0000-000000000000",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "bc8c97bc-9403-46ec-a860-6874fd750ca4", "b91ede7a-21f5-4d52-a5cd-6bc41e6358b5" });
        }
    }
}
