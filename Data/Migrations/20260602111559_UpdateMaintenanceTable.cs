using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMaintenanceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "Maintenances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Maintenances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "Maintenances");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Maintenances");
        }
    }
}
