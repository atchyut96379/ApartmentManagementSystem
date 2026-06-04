using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[AuditLogs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AuditLogs] (
                        [Id] int NOT NULL IDENTITY,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [ActorUserId] nvarchar(450) NULL,
                        [ActorDisplayName] nvarchar(200) NOT NULL,
                        [Action] nvarchar(100) NOT NULL,
                        [EntityType] nvarchar(100) NULL,
                        [EntityId] int NULL,
                        [FlatNumber] nvarchar(50) NULL,
                        [Details] nvarchar(2000) NOT NULL,
                        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_AuditLogs_CreatedAtUtc] ON [AuditLogs] ([CreatedAtUtc]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
