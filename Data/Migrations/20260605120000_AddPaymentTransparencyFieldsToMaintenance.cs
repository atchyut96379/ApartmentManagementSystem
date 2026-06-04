using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagementSystem.Data.Migrations
{
    public partial class AddPaymentTransparencyFieldsToMaintenance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('Maintenances', 'FineAmount') IS NULL
                    ALTER TABLE [Maintenances] ADD [FineAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Maintenances_FineAmount] DEFAULT 0;
                IF COL_LENGTH('Maintenances', 'TotalPaidAmount') IS NULL
                    ALTER TABLE [Maintenances] ADD [TotalPaidAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Maintenances_TotalPaidAmount] DEFAULT 0;
                IF COL_LENGTH('Maintenances', 'TransactionId') IS NULL
                    ALTER TABLE [Maintenances] ADD [TransactionId] nvarchar(max) NULL;
                IF COL_LENGTH('Maintenances', 'PayerName') IS NULL
                    ALTER TABLE [Maintenances] ADD [PayerName] nvarchar(max) NULL;
                IF COL_LENGTH('Maintenances', 'PaymentGateway') IS NULL
                    ALTER TABLE [Maintenances] ADD [PaymentGateway] nvarchar(max) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('Maintenances', 'FineAmount') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP CONSTRAINT [DF_Maintenances_FineAmount];
                IF COL_LENGTH('Maintenances', 'FineAmount') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP COLUMN [FineAmount];
                IF COL_LENGTH('Maintenances', 'TotalPaidAmount') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP CONSTRAINT [DF_Maintenances_TotalPaidAmount];
                IF COL_LENGTH('Maintenances', 'TotalPaidAmount') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP COLUMN [TotalPaidAmount];
                IF COL_LENGTH('Maintenances', 'TransactionId') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP COLUMN [TransactionId];
                IF COL_LENGTH('Maintenances', 'PayerName') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP COLUMN [PayerName];
                IF COL_LENGTH('Maintenances', 'PaymentGateway') IS NOT NULL
                    ALTER TABLE [Maintenances] DROP COLUMN [PaymentGateway];
                """);
        }
    }
}
