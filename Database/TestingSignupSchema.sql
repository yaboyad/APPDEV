-- Lightweight SQL starter for the signup flow.
-- The current app stores accounts in a DPAPI-protected local file for testing.
-- When you are ready to mirror the same shape into SQL Server, this table is the simplest starting point.

CREATE TABLE dbo.AppUsers
(
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    LoginName NVARCHAR(255) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(40) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordSalt NVARCHAR(128) NOT NULL,
    PasswordHash NVARCHAR(128) NOT NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_AppUsers_CreatedUtc DEFAULT SYSUTCDATETIME()
);
GO

CREATE UNIQUE INDEX UX_AppUsers_LoginName ON dbo.AppUsers (LoginName);
GO

CREATE UNIQUE INDEX UX_AppUsers_Email ON dbo.AppUsers (Email);
GO

-- For later hardening:
-- 1. Keep PasswordSalt and PasswordHash exactly as they come from the app.
-- 2. If you want profile data encrypted in SQL too, change FirstName/LastName/PhoneNumber/Email
--    to VARBINARY(MAX) and save encrypted bytes from the application layer.
