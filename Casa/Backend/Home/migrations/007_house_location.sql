SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.HomeSchemaVersions WHERE Version=7)
BEGIN
 CREATE TABLE dbo.HomeLocations(
  HouseId uniqueidentifier NOT NULL PRIMARY KEY,
  Label nvarchar(200) NOT NULL,
  Latitude float NOT NULL,
  Longitude float NOT NULL,
  UpdatedByUserId uniqueidentifier NOT NULL,
  UpdatedAtUtc datetimeoffset NOT NULL
 );
 INSERT dbo.HomeSchemaVersions VALUES(7,SYSUTCDATETIME());
END;
COMMIT;