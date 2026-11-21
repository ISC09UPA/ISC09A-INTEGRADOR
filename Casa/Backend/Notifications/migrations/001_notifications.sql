IF OBJECT_ID(N'dbo.NotificationsInbox', N'U') IS NULL
CREATE TABLE dbo.NotificationsInbox (
    EventId uniqueidentifier NOT NULL PRIMARY KEY,
    BodyHash nvarchar(128) NOT NULL,
    Outcome nvarchar(64) NOT NULL,
    ReceivedAtUtc datetimeoffset NOT NULL
);

IF OBJECT_ID(N'dbo.NotificationPreferences', N'U') IS NULL
CREATE TABLE dbo.NotificationPreferences (
    HouseId uniqueidentifier NOT NULL PRIMARY KEY,
    EmailEnabled bit NOT NULL,
    VerifiedEmail nvarchar(254) NULL,
    VerifiedAtUtc datetimeoffset NULL,
    PendingRecipientEmail nvarchar(254) NULL,
    VerificationCodeHash nvarchar(128) NULL,
    VerificationExpiresAtUtc datetimeoffset NULL,
    UpdatedAtUtc datetimeoffset NOT NULL
);

IF OBJECT_ID(N'dbo.NotificationDeliveries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationDeliveries (
        DeliveryId uniqueidentifier NOT NULL PRIMARY KEY,
        HouseId uniqueidentifier NOT NULL,
        Kind nvarchar(32) NOT NULL,
        SourceEventId uniqueidentifier NULL,
        IdempotencyKey nvarchar(128) NOT NULL,
        RecipientEmail nvarchar(254) NOT NULL,
        Subject nvarchar(256) NOT NULL,
        BodyText nvarchar(4000) NOT NULL,
        Status nvarchar(32) NOT NULL,
        ProviderOperationId nvarchar(128) NULL,
        ErrorCode nvarchar(64) NULL,
        CorrelationId uniqueidentifier NOT NULL,
        CreatedAtUtc datetimeoffset NOT NULL,
        UpdatedAtUtc datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX UX_NotificationDeliveries_House_Key ON dbo.NotificationDeliveries (HouseId, IdempotencyKey);
    CREATE INDEX IX_NotificationDeliveries_House_Created ON dbo.NotificationDeliveries (HouseId, CreatedAtUtc DESC);
END;
