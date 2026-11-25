IF OBJECT_ID(N'dbo.SecurityPersons', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SecurityPersons (
        PersonId uniqueidentifier NOT NULL PRIMARY KEY,
        HouseId uniqueidentifier NOT NULL,
        Name nvarchar(120) NOT NULL,
        Method nvarchar(20) NOT NULL CHECK (Method IN ('face', 'fingerprint')),
        Status nvarchar(20) NOT NULL CHECK (Status IN ('pending', 'authorized', 'revoked', 'expired', 'failed')),
        ValidUntilUtc datetimeoffset NOT NULL,
        BiometricReferenceId nvarchar(128) NULL,
        EnrollmentCaptureId uniqueidentifier NULL,
        ConfidenceScore float NULL,
        RevokedAtUtc datetimeoffset NULL,
        RetentionPurgedAtUtc datetimeoffset NULL,
        CreatedAtUtc datetimeoffset NOT NULL,
        UpdatedAtUtc datetimeoffset NOT NULL
    );
    CREATE INDEX IX_SecurityPersons_House_Created ON dbo.SecurityPersons (HouseId, CreatedAtUtc DESC, PersonId DESC);
END;

IF OBJECT_ID(N'dbo.SecurityEnrollmentSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SecurityEnrollmentSessions (
        EnrollmentId uniqueidentifier NOT NULL PRIMARY KEY,
        HouseId uniqueidentifier NOT NULL,
        PersonId uniqueidentifier NOT NULL REFERENCES dbo.SecurityPersons(PersonId),
        Method nvarchar(20) NOT NULL CHECK (Method IN ('face', 'fingerprint')),
        Status nvarchar(20) NOT NULL CHECK (Status IN ('pending', 'acquiring', 'completed', 'failed', 'cancelled')),
        Instruction nvarchar(256) NULL,
        ErrorCode nvarchar(64) NULL,
        ErrorMessage nvarchar(256) NULL,
        ExpiresAtUtc datetimeoffset NOT NULL,
        CaptureId uniqueidentifier NULL,
        TemplateSlotId nvarchar(64) NULL,
        CreatedAtUtc datetimeoffset NOT NULL,
        UpdatedAtUtc datetimeoffset NOT NULL
    );
    CREATE INDEX IX_SecurityEnrollmentSessions_House_Person ON dbo.SecurityEnrollmentSessions (HouseId, PersonId, CreatedAtUtc DESC);
END;