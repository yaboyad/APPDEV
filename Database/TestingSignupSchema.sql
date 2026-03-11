-- Lightweight SQL starter for the signup flow.
-- The current app stores accounts and support submissions in local files for testing.
-- When you are ready to mirror the same shape into SQL Server, these tables are the simplest starting point.

CREATE TABLE dbo.AppUsers
(
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    LoginName NVARCHAR(255) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(40) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    AccountTier NVARCHAR(40) NOT NULL CONSTRAINT DF_AppUsers_AccountTier DEFAULT N'User',
    PasswordSalt NVARCHAR(128) NOT NULL,
    PasswordHash NVARCHAR(128) NOT NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_AppUsers_CreatedUtc DEFAULT SYSUTCDATETIME()
);
GO

CREATE UNIQUE INDEX UX_AppUsers_LoginName ON dbo.AppUsers (LoginName);
GO

CREATE UNIQUE INDEX UX_AppUsers_Email ON dbo.AppUsers (Email);
GO

CREATE TABLE dbo.SupportSubmissions
(
    SupportSubmissionId INT IDENTITY(1,1) PRIMARY KEY,
    LoginName NVARCHAR(255) NOT NULL,
    DisplayName NVARCHAR(255) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    AccountTier NVARCHAR(40) NOT NULL,
    Channel NVARCHAR(80) NOT NULL,
    Status NVARCHAR(80) NOT NULL,
    IsUrgent BIT NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportSubmissions_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE INDEX IX_SupportSubmissions_LoginName ON dbo.SupportSubmissions (LoginName, CreatedAt DESC);
GO

CREATE INDEX IX_SupportSubmissions_CreatedAt ON dbo.SupportSubmissions (CreatedAt DESC);
GO

-- For later hardening:
-- 1. Keep PasswordSalt and PasswordHash exactly as they come from the app.
-- 2. If you want profile data encrypted in SQL too, change FirstName/LastName/PhoneNumber/Email
--    to VARBINARY(MAX) and save encrypted bytes from the application layer.
