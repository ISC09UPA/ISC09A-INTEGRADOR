IF OBJECT_ID(N'dbo.SecurityCaptures', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.SecurityCaptures (CaptureId uniqueidentifier NOT NULL PRIMARY KEY, HouseId uniqueidentifier NOT NULL, DeviceId nvarchar(128) NOT NULL, BlobName nvarchar(1024) NOT NULL, BlobETag nvarchar(128) NOT NULL, OccurredAtUtc datetimeoffset NOT NULL, ReceivedAtUtc datetimeoffset NOT NULL, AnalysisStatus nvarchar(24) NULL, AnalysisText nvarchar(max) NULL, AnalysisRequestedAtUtc datetimeoffset NULL, AnalysisStartedAtUtc datetimeoffset NULL, AnalysisError nvarchar(64) NULL, DeleteRequestedAtUtc datetimeoffset NULL, DeletedAtUtc datetimeoffset NULL, DeleteError nvarchar(64) NULL);
 CREATE INDEX IX_SecurityCaptures_House_Time ON dbo.SecurityCaptures (HouseId, OccurredAtUtc DESC, CaptureId DESC);
END;
IF OBJECT_ID(N'dbo.SecurityCapturePolicies', N'U') IS NULL
CREATE TABLE dbo.SecurityCapturePolicies (HouseId uniqueidentifier NOT NULL PRIMARY KEY, RetentionDays int NULL, UpdatedAtUtc datetimeoffset NOT NULL, CONSTRAINT CK_SecurityCapturePolicies_Days CHECK (RetentionDays IS NULL OR RetentionDays BETWEEN 1 AND 3650));
