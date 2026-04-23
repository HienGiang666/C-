IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [POIs] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    CONSTRAINT [PK_POIs] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260318005419_InitialCreate', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Bookings] (
    [Id] int NOT NULL IDENTITY,
    [TourId] int NOT NULL,
    [UserId] int NOT NULL,
    [NumberOfParticipants] int NOT NULL,
    [BookingDate] datetime2 NOT NULL,
    [TourDate] datetime2 NOT NULL,
    [TotalPrice] decimal(18,2) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id])
);

CREATE TABLE [Tours] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Duration] int NOT NULL,
    [Destination] nvarchar(max) NOT NULL,
    [MaxParticipants] int NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [SearchKeywords] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Tours] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [PhoneNumber] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    [DateOfBirth] datetime2 NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260331081020_AddNewAdminModels', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [POIs] ADD [Address] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [POIs] ADD [ImageUrl] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [POIs] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [POIs] ADD [OpenTime] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [POIs] ADD [Priority] int NOT NULL DEFAULT 0;

ALTER TABLE [POIs] ADD [Radius] float NOT NULL DEFAULT 0.0E0;

ALTER TABLE [POIs] ADD [Rating] float NOT NULL DEFAULT 0.0E0;

CREATE TABLE [Audios] (
    [Id] int NOT NULL IDENTITY,
    [POIId] int NOT NULL,
    [Language] nvarchar(max) NOT NULL,
    [AudioPath] nvarchar(max) NOT NULL,
    [ScriptText] nvarchar(max) NOT NULL,
    [Duration] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Audios] PRIMARY KEY ([Id])
);

CREATE TABLE [NarrationLogs] (
    [Id] int NOT NULL IDENTITY,
    [POIId] int NOT NULL,
    [AudioId] int NULL,
    [TriggerType] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [DeviceId] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_NarrationLogs] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260331163216_UpdatePOITables', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [TourPOIs] (
    [Id] int NOT NULL IDENTITY,
    [TourId] int NOT NULL,
    [POIId] int NOT NULL,
    [OrderIndex] int NOT NULL,
    CONSTRAINT [PK_TourPOIs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TourPOIs_POIs_POIId] FOREIGN KEY ([POIId]) REFERENCES [POIs] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TourPOIs_Tours_TourId] FOREIGN KEY ([TourId]) REFERENCES [Tours] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserLocationLogs] (
    [Id] int NOT NULL IDENTITY,
    [DeviceId] nvarchar(max) NOT NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_UserLocationLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Audios_POIId] ON [Audios] ([POIId]);

CREATE INDEX [IX_TourPOIs_POIId] ON [TourPOIs] ([POIId]);

CREATE INDEX [IX_TourPOIs_TourId] ON [TourPOIs] ([TourId]);

ALTER TABLE [Audios] ADD CONSTRAINT [FK_Audios_POIs_POIId] FOREIGN KEY ([POIId]) REFERENCES [POIs] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260403151744_AddUserLocationAndTourPOI', N'10.0.5');

COMMIT;
GO

