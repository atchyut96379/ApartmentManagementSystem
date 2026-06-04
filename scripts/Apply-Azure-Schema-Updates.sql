-- Run in Azure SQL Query editor if dashboard still errors after deploy.
-- Safe to run more than once (checks column/table existence).

IF COL_LENGTH('Residents', 'MemberType') IS NULL
BEGIN
    ALTER TABLE [Residents] ADD [MemberType] int NOT NULL CONSTRAINT [DF_Residents_MemberType] DEFAULT 0;
    UPDATE [Residents] SET [MemberType] = 1 WHERE [IsOwner] = 0;
END

IF COL_LENGTH('Residents', 'AssociationDesignation') IS NULL
    ALTER TABLE [Residents] ADD [AssociationDesignation] nvarchar(max) NULL;

IF COL_LENGTH('Residents', 'PropertyOwnerName') IS NULL
    ALTER TABLE [Residents] ADD [PropertyOwnerName] nvarchar(max) NULL;

IF COL_LENGTH('Residents', 'OwnerContactNumber') IS NULL
    ALTER TABLE [Residents] ADD [OwnerContactNumber] nvarchar(max) NULL;

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

IF COL_LENGTH('Maintenances', 'RazorpayOrderId') IS NULL
    ALTER TABLE [Maintenances] ADD [RazorpayOrderId] nvarchar(max) NULL;

IF OBJECT_ID(N'[AuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [ActorDisplayName] nvarchar(200) NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NULL,
        [EntityId] int NULL,
        [FlatNumber] nvarchar(50) NULL,
        [Details] nvarchar(2000) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_AuditLogs_CreatedAtUtc] ON [AuditLogs] ([CreatedAtUtc]);
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260604120000_AddAssociationMemberFieldsToResident')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260604120000_AddAssociationMemberFieldsToResident', N'10.0.0');

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260605120000_AddPaymentTransparencyFieldsToMaintenance')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260605120000_AddPaymentTransparencyFieldsToMaintenance', N'10.0.0');

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260607120000_AddAuditLogs')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260607120000_AddAuditLogs', N'10.0.0');

PRINT 'Schema update complete.';
