IF OBJECT_ID(N'dbo.SecurityInbox', N'U') IS NULL
CREATE TABLE dbo.SecurityInbox (EventId uniqueidentifier NOT NULL PRIMARY KEY, BodyHash varchar(64) NOT NULL, ReceivedAtUtc datetimeoffset NOT NULL);
IF OBJECT_ID(N'dbo.SecurityModes', N'U') IS NULL
CREATE TABLE dbo.SecurityModes (HouseId uniqueidentifier NOT NULL PRIMARY KEY, Mode nvarchar(16) NOT NULL, EventId uniqueidentifier NOT NULL, OccurredAtUtc datetimeoffset NOT NULL, ReceivedAtUtc datetimeoffset NOT NULL, CONSTRAINT CK_SecurityModes_Mode CHECK (Mode IN ('armed','disarmed')));
IF OBJECT_ID(N'dbo.SecurityActivity', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.SecurityActivity (EventId uniqueidentifier NOT NULL PRIMARY KEY, HouseId uniqueidentifier NOT NULL, Type nvarchar(64) NOT NULL, Result nvarchar(32) NOT NULL, RoomId nvarchar(128) NULL, OccurredAtUtc datetimeoffset NOT NULL, ReceivedAtUtc datetimeoffset NOT NULL);
 CREATE INDEX IX_SecurityActivity_House_Time ON dbo.SecurityActivity (HouseId, OccurredAtUtc DESC, EventId DESC);
END;
IF OBJECT_ID(N'dbo.SecurityIncidents', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.SecurityIncidents (EventId uniqueidentifier NOT NULL PRIMARY KEY, HouseId uniqueidentifier NOT NULL, RoomId nvarchar(128) NOT NULL, OccurredAtUtc datetimeoffset NOT NULL, ReceivedAtUtc datetimeoffset NOT NULL);
 CREATE INDEX IX_SecurityIncidents_House_Time ON dbo.SecurityIncidents (HouseId, OccurredAtUtc DESC, EventId DESC);
END;
IF OBJECT_ID(N'dbo.SecurityOutbox', N'U') IS NULL
CREATE TABLE dbo.SecurityOutbox (EventId uniqueidentifier NOT NULL PRIMARY KEY, Type nvarchar(64) NOT NULL, BodyJson nvarchar(max) NOT NULL, CreatedAtUtc datetimeoffset NOT NULL, PublishedAtUtc datetimeoffset NULL);
