using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagementSystem.Data.Migrations
{
    public partial class AddRazorpayOrderIdToMaintenance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Maintenances', 'RazorpayOrderId') IS NULL
                    ALTER TABLE [Maintenances] ADD [RazorpayOrderId] nvarchar(max) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Maintenances', 'RazorpayOrderId') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP COLUMN [RazorpayOrderId];
                """);
        }
    }
}
