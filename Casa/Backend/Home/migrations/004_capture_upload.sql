SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.HomeSchemaVersions WHERE Version=4)
BEGIN
 ALTER TABLE dbo.HomeCaptureJobs ADD UploadTokenHash varchar(64) NULL;
 ALTER TABLE dbo.HomeCaptureJobs ADD UploadStatus nvarchar(16) NULL;
 ALTER TABLE dbo.HomeCaptureJobs ADD UploadedBlobName nvarchar(1024) NULL;
 ALTER TABLE dbo.HomeCaptureJobs ADD UploadedETag nvarchar(128) NULL;
 ALTER TABLE dbo.HomeCaptureJobs ADD UploadedContentHash varchar(64) NULL;
 ALTER TABLE dbo.HomeCaptureJobs ADD UploadedContentType varchar(32) NULL;
 INSERT dbo.HomeSchemaVersions VALUES(4,SYSUTCDATETIME());
END;
COMMIT;
