SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.HomeSchemaVersions WHERE Version=6)
BEGIN
 CREATE TABLE dbo.HomeExpiredCommandKeys(HouseId uniqueidentifier NOT NULL, IdempotencyKey nvarchar(36) NOT NULL, ClientExpiresAtUtc datetimeoffset NOT NULL, ExpiredAtUtc datetimeoffset NOT NULL, PRIMARY KEY(HouseId, IdempotencyKey));
 INSERT dbo.HomeSchemaVersions VALUES(6,SYSUTCDATETIME());
END;
COMMIT;
