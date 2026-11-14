SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.IdentitySchemaVersions') IS NULL
 CREATE TABLE dbo.IdentitySchemaVersions(Version int NOT NULL PRIMARY KEY, AppliedAt datetimeoffset NOT NULL);
IF NOT EXISTS(SELECT 1 FROM dbo.IdentitySchemaVersions WHERE Version=1)
BEGIN
 CREATE TABLE dbo.IdentityUsers(Id uniqueidentifier NOT NULL PRIMARY KEY,Email nvarchar(254) NOT NULL,NormalizedEmail nvarchar(254) NOT NULL UNIQUE,PasswordHash nvarchar(512) NOT NULL,Enabled bit NOT NULL);
 CREATE TABLE dbo.IdentityHouses(Id uniqueidentifier NOT NULL PRIMARY KEY,Name nvarchar(200) NOT NULL);
 CREATE TABLE dbo.IdentityMemberships(UserId uniqueidentifier NOT NULL REFERENCES dbo.IdentityUsers(Id),HouseId uniqueidentifier NOT NULL REFERENCES dbo.IdentityHouses(Id),Role nvarchar(20) NOT NULL CHECK(Role IN ('admin','resident')),PRIMARY KEY(UserId,HouseId));
 CREATE TABLE dbo.IdentitySessions(Id uniqueidentifier NOT NULL PRIMARY KEY,UserId uniqueidentifier NOT NULL REFERENCES dbo.IdentityUsers(Id),CreatedAt datetimeoffset NOT NULL,ExpiresAt datetimeoffset NOT NULL,RevokedAt datetimeoffset NULL);
 CREATE TABLE dbo.IdentityRefreshTokens(Hash varchar(64) NOT NULL PRIMARY KEY,SessionId uniqueidentifier NOT NULL REFERENCES dbo.IdentitySessions(Id),IssuedAt datetimeoffset NOT NULL,UsedAt datetimeoffset NULL);
 CREATE TABLE dbo.IdentityAssertions(Jti nvarchar(128) NOT NULL PRIMARY KEY,ExpiresAt datetimeoffset NOT NULL);
 INSERT dbo.IdentitySchemaVersions VALUES(1,SYSUTCDATETIME());
END;
COMMIT;
