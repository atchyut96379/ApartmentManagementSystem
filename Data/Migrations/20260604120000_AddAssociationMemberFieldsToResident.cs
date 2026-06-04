using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagementSystem.Data.Migrations
{
    public partial class AddAssociationMemberFieldsToResident : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssociationDesignation",
                table: "Residents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberType",
                table: "Residents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE Residents SET MemberType = 1 WHERE IsOwner = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssociationDesignation",
                table: "Residents");

            migrationBuilder.DropColumn(
                name: "MemberType",
                table: "Residents");
        }
    }
}
