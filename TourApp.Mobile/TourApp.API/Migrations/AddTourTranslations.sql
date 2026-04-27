-- Add TourTranslations table manually (when EF migration conflicts with existing DB)
-- Run this in your SQL Server database

CREATE TABLE [TourTranslations] (
    [Id] int NOT NULL IDENTITY,
    [TourId] int NOT NULL,
    [Language] nvarchar(10) NOT NULL DEFAULT 'vi',
    [Description] nvarchar(max) NULL,
    CONSTRAINT [PK_TourTranslations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TourTranslations_Tours_TourId] FOREIGN KEY ([TourId]) REFERENCES [Tours] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_TourTranslations_TourId] ON [TourTranslations] ([TourId]);
GO
