SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.HomeSchemaVersions WHERE Version=5)
BEGIN
 CREATE TABLE dbo.HomeVideoSessions(SessionId uniqueidentifier NOT NULL PRIMARY KEY, HouseId uniqueidentifier NOT NULL, ResourceId nvarchar(128) NOT NULL, DeviceId nvarchar(128) NOT NULL, UserId uniqueidentifier NOT NULL, UserSessionId uniqueidentifier NOT NULL, IdempotencyKey nvarchar(36) NOT NULL, CommandId nvarchar(128) NOT NULL, CreatedAtUtc datetimeoffset NOT NULL, ExpiresAtUtc datetimeoffset NOT NULL, ClosedAtUtc datetimeoffset NULL, CloseReason nvarchar(24) NULL, Active bit NOT NULL, ProducerTokenHash varchar(64) NULL, LastSequence bigint NOT NULL, TotalBytes bigint NOT NULL, PlaylistJson nvarchar(max) NULL, PlaylistUpdatedAtUtc datetimeoffset NULL);
 CREATE UNIQUE INDEX IX_HomeVideoSessions_Active ON dbo.HomeVideoSessions(HouseId) WHERE Active=1;
 CREATE UNIQUE INDEX IX_HomeVideoSessions_Key ON dbo.HomeVideoSessions(HouseId, IdempotencyKey);
 CREATE TABLE dbo.HomeVideoSegments(SessionId uniqueidentifier NOT NULL REFERENCES dbo.HomeVideoSessions(SessionId), Sequence bigint NOT NULL, ContentHash varchar(64) NOT NULL, SizeBytes bigint NOT NULL, DurationMs int NOT NULL, State nvarchar(16) NOT NULL, BlobName nvarchar(1024) NULL, BlobETag nvarchar(128) NULL, CreatedAtUtc datetimeoffset NOT NULL, DeletedAtUtc datetimeoffset NULL, CleanupError nvarchar(32) NULL, PRIMARY KEY(SessionId, Sequence));
 INSERT dbo.HomeSchemaVersions VALUES(5,SYSUTCDATETIME());
END;
COMMIT;
