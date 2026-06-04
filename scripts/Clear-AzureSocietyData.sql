-- Run once in Azure Portal: SQL database → Query editor (or SSMS).
-- Clears wrong import; keeps system Admin login only.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DELETE FROM [Maintenances];
DELETE FROM [AuditLogs];
DELETE FROM [Expenses];
DELETE FROM [Residents];

DELETE FROM [AspNetUserTokens]
WHERE [UserId] IN (SELECT [Id] FROM [AspNetUsers] WHERE [UserName] <> N'Admin');

DELETE FROM [AspNetUserLogins]
WHERE [UserId] IN (SELECT [Id] FROM [AspNetUsers] WHERE [UserName] <> N'Admin');

DELETE FROM [AspNetUserClaims]
WHERE [UserId] IN (SELECT [Id] FROM [AspNetUsers] WHERE [UserName] <> N'Admin');

DELETE FROM [AspNetUserRoles]
WHERE [UserId] IN (SELECT [Id] FROM [AspNetUsers] WHERE [UserName] <> N'Admin');

DELETE FROM [AspNetUsers]
WHERE [UserName] <> N'Admin';

SELECT COUNT(*) AS RemainingResidents FROM [Residents];
SELECT COUNT(*) AS RemainingUsers FROM [AspNetUsers];
