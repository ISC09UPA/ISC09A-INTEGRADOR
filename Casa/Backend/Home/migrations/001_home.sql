SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.HomeSchemaVersions') IS NULL CREATE TABLE dbo.HomeSchemaVersions(Version int NOT NULL PRIMARY KEY,AppliedAt datetimeoffset NOT NULL);
IF NOT EXISTS(SELECT 1 FROM dbo.HomeSchemaVersions WHERE Version=1)
BEGIN
 CREATE TABLE dbo.HomeDevices(DeviceId nvarchar(128) NOT NULL PRIMARY KEY,HouseId uniqueidentifier NOT NULL,Enabled bit NOT NULL,LastSequence bigint NOT NULL,LastReportedAtUtc datetimeoffset NULL);
 CREATE INDEX IX_HomeDevices_HouseId ON dbo.HomeDevices(HouseId);
 CREATE TABLE dbo.HomeLights(HouseId uniqueidentifier NOT NULL,LightId nvarchar(128) NOT NULL,DeviceId nvarchar(128) NOT NULL REFERENCES dbo.HomeDevices(DeviceId),LightOn bit NULL,LightingMode nvarchar(10) NULL,StateRevision bigint NOT NULL,AppliedAtUtc datetimeoffset NULL,LastCommandId nvarchar(128) NULL,PRIMARY KEY(HouseId,LightId));
 CREATE UNIQUE INDEX IX_HomeLights_Device ON dbo.HomeLights(DeviceId);
 CREATE TABLE dbo.HomeCommands(CommandId nvarchar(128) NOT NULL PRIMARY KEY,HouseId uniqueidentifier NOT NULL,LightId nvarchar(128) NOT NULL,DeviceId nvarchar(128) NOT NULL,Type nvarchar(50) NOT NULL,PayloadJson nvarchar(max) NOT NULL,RequestHash varchar(64) NOT NULL,IdempotencyKey nvarchar(36) NOT NULL,Sequence bigint NOT NULL,UserId uniqueidentifier NOT NULL,SessionId uniqueidentifier NOT NULL,CorrelationId uniqueidentifier NOT NULL,RequestedAtUtc datetimeoffset NOT NULL,ExpiresAtUtc datetimeoffset NOT NULL,DispatchedAtUtc datetimeoffset NULL,Status nvarchar(20) NOT NULL,ResultJson nvarchar(max) NULL,Active bit NOT NULL);
 CREATE UNIQUE INDEX IX_HomeCommands_Key ON dbo.HomeCommands(HouseId,IdempotencyKey);
 CREATE UNIQUE INDEX IX_HomeCommands_Active ON dbo.HomeCommands(HouseId,LightId) WHERE Active=1;
 CREATE TABLE dbo.HomeCaptureJobs(CaptureId uniqueidentifier NOT NULL PRIMARY KEY,HouseId uniqueidentifier NOT NULL,DeviceId nvarchar(128) NOT NULL,ExpiresAtUtc datetimeoffset NOT NULL);
 CREATE TABLE dbo.HomeInbox(EventId uniqueidentifier NOT NULL PRIMARY KEY,DeviceId nvarchar(128) NOT NULL,ReceivedAtUtc datetimeoffset NOT NULL);
 CREATE TABLE dbo.HomeOutbox(EventId uniqueidentifier NOT NULL PRIMARY KEY,Type nvarchar(80) NOT NULL,BodyJson nvarchar(max) NOT NULL,CreatedAtUtc datetimeoffset NOT NULL,PublishedAtUtc datetimeoffset NULL);
 CREATE TABLE dbo.HomeTelemetry(EventId uniqueidentifier NOT NULL PRIMARY KEY,HouseId uniqueidentifier NOT NULL,SensorId nvarchar(128) NOT NULL,Type nvarchar(80) NOT NULL,PayloadJson nvarchar(max) NOT NULL,OccurredAtUtc datetimeoffset NOT NULL,ReceivedAtUtc datetimeoffset NOT NULL);
 CREATE INDEX IX_HomeTelemetry_House_Received ON dbo.HomeTelemetry(HouseId,ReceivedAtUtc);
 INSERT dbo.HomeSchemaVersions VALUES(1,SYSUTCDATETIME());
END;
COMMIT;
