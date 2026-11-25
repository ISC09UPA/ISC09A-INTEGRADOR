SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.IdentitySchemaVersions WHERE Version=2)
BEGIN
 IF COL_LENGTH('dbo.IdentityUsers', 'EmailVerified') IS NULL
  ALTER TABLE dbo.IdentityUsers ADD EmailVerified bit NOT NULL CONSTRAINT DF_IdentityUsers_EmailVerified DEFAULT(1);

 IF OBJECT_ID('dbo.IdentityInvitations', 'U') IS NULL
 BEGIN
  CREATE TABLE dbo.IdentityInvitations(
   Id uniqueidentifier NOT NULL PRIMARY KEY,
   HouseId uniqueidentifier NOT NULL REFERENCES dbo.IdentityHouses(Id),
   Email nvarchar(254) NOT NULL,
   NormalizedEmail nvarchar(254) NOT NULL,
   Role nvarchar(20) NOT NULL CHECK(Role IN ('resident')),
   TokenHash varchar(64) NOT NULL UNIQUE,
   CreatedByUserId uniqueidentifier NOT NULL REFERENCES dbo.IdentityUsers(Id),
   CreatedAtUtc datetimeoffset NOT NULL,
   ExpiresAtUtc datetimeoffset NOT NULL,
   AcceptedAtUtc datetimeoffset NULL,
   AcceptedByUserId uniqueidentifier NULL REFERENCES dbo.IdentityUsers(Id),
   RevokedAtUtc datetimeoffset NULL
  );
  CREATE INDEX IX_IdentityInvitations_House_Created ON dbo.IdentityInvitations(HouseId, CreatedAtUtc DESC);
 END;

 IF OBJECT_ID('dbo.IdentityPasswordResetTokens', 'U') IS NULL
 BEGIN
  CREATE TABLE dbo.IdentityPasswordResetTokens(
   TokenHash varchar(64) NOT NULL PRIMARY KEY,
   UserId uniqueidentifier NOT NULL REFERENCES dbo.IdentityUsers(Id),
   CreatedAtUtc datetimeoffset NOT NULL,
   ExpiresAtUtc datetimeoffset NOT NULL,
   UsedAtUtc datetimeoffset NULL
  );
  CREATE INDEX IX_IdentityPasswordResetTokens_User ON dbo.IdentityPasswordResetTokens(UserId, CreatedAtUtc DESC);
 END;

 INSERT dbo.IdentitySchemaVersions VALUES(2,SYSUTCDATETIME());
END;
COMMIT;