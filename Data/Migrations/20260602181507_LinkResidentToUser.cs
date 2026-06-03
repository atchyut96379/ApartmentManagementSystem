using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkResidentToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlatNumber",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Residents",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlatNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Residents");
        }
    }
}
