IF OBJECT_ID(N'dbo.SecurityPreferences', N'U') IS NULL
CREATE TABLE dbo.SecurityPreferences (HouseId uniqueidentifier NOT NULL PRIMARY KEY, CapturesEnabled bit NOT NULL, AnalysisEnabled bit NOT NULL, UpdatedAtUtc datetimeoffset NOT NULL);
