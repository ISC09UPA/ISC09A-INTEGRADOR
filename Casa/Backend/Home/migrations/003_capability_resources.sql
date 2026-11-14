SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.HomeSchemaVersions WHERE Version=3)
BEGIN
 CREATE TABLE dbo.HomeResources(HouseId uniqueidentifier NOT NULL, ResourceId nvarchar(128) NOT NULL, DeviceId nvarchar(128) NOT NULL REFERENCES dbo.HomeDevices(DeviceId), Kind nvarchar(20) NOT NULL, DeviceResourceId nvarchar(128) NOT NULL, Enabled bit NOT NULL, CONSTRAINT PK_HomeResources PRIMARY KEY(HouseId,ResourceId), CONSTRAINT CK_HomeResources_Kind CHECK(Kind IN ('security','door','camera')));
 CREATE INDEX IX_HomeResources_Device ON dbo.HomeResources(DeviceId);
 ALTER TABLE dbo.HomeCaptureJobs ADD CreatedAtUtc datetimeoffset NULL;
 ALTER TABLE dbo.HomeCaptureJobs ADD CommandId nvarchar(128) NULL;
 INSERT dbo.HomeSchemaVersions VALUES(3,SYSUTCDATETIME());
END;
COMMIT;
