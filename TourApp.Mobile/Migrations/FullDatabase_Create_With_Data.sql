-- =============================================
-- TOURAPP DATABASE - FULL CREATE SCRIPT WITH SAMPLE DATA
-- Đã cập nhật khớp với Models C# (API & CMS)
-- =============================================

-- Create Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'TourAppDB')
BEGIN
    CREATE DATABASE TourAppDB;
END
GO

USE TourAppDB;
GO

-- =============================================
-- 1. CREATE TABLES - Đúng cấu trúc Models
-- =============================================

-- Users table (khớp với User.cs)
IF OBJECT_ID(N'[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY(1,1),
        [FullName] nvarchar(200) NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(50) NULL,
        [Address] nvarchar(500) NULL,
        [DateOfBirth] datetime2 NULL,
        [Role] nvarchar(50) NULL DEFAULT 'Customer',
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
        [LastLoginAt] datetime2 NULL,
        [Code] varchar(50) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Users_Username] UNIQUE ([Username]),
        CONSTRAINT [UQ_Users_Email] UNIQUE ([Email])
    );
END
GO

-- Create indexes for Users (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Role' AND object_id = OBJECT_ID('Users'))
    CREATE INDEX [IX_Users_Role] ON [Users]([Role]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Code' AND object_id = OBJECT_ID('Users'))
    CREATE INDEX [IX_Users_Code] ON [Users]([Code]);
GO

-- POIs table (Points of Interest)
IF OBJECT_ID(N'[POIs]', N'U') IS NULL
BEGIN
    CREATE TABLE [POIs] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [Radius] float NOT NULL DEFAULT 80,
        [Priority] int NOT NULL DEFAULT 1,
        [Address] nvarchar(500) NULL,
        [ImageUrl] nvarchar(500) NULL,
        [OpenTime] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [Rating] float NOT NULL DEFAULT 4.5,
        [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT 'Approved',
        [OwnerUserId] int NULL,
        [Code] varchar(50) NULL,
        CONSTRAINT [PK_POIs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_POIs_Users] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users]([Id]),
        CONSTRAINT [UQ_POIs_Code] UNIQUE ([Code])
    );
END
GO

-- Create indexes for POIs (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_POIs_OwnerUserId' AND object_id = OBJECT_ID('POIs'))
    CREATE INDEX [IX_POIs_OwnerUserId] ON [POIs]([OwnerUserId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_POIs_Code' AND object_id = OBJECT_ID('POIs'))
    CREATE INDEX [IX_POIs_Code] ON [POIs]([Code]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_POIs_ApprovalStatus' AND object_id = OBJECT_ID('POIs'))
    CREATE INDEX [IX_POIs_ApprovalStatus] ON [POIs]([ApprovalStatus]);
GO

-- Tours table
IF OBJECT_ID(N'[Tours]', N'U') IS NULL
BEGIN
    CREATE TABLE [Tours] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Price] decimal(18,2) NOT NULL DEFAULT 0,
        [Duration] int NOT NULL DEFAULT 1,
        [Destination] nvarchar(500) NULL,
        [MaxParticipants] int NOT NULL DEFAULT 20,
        [ImageUrl] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
        [IsActive] bit NOT NULL DEFAULT 1,
        [SearchKeywords] nvarchar(1000) NULL,
        [Code] varchar(50) NULL,
        CONSTRAINT [PK_Tours] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Tours_Code] UNIQUE ([Code])
    );
END
GO

-- Create indexes for Tours (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tours_Code' AND object_id = OBJECT_ID('Tours'))
    CREATE INDEX [IX_Tours_Code] ON [Tours]([Code]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tours_IsActive' AND object_id = OBJECT_ID('Tours'))
    CREATE INDEX [IX_Tours_IsActive] ON [Tours]([IsActive]);
GO

-- Tour-POI Relationship table (for multiple stops in a tour)
IF OBJECT_ID(N'[TourPOIs]', N'U') IS NULL
BEGIN
    CREATE TABLE [TourPOIs] (
        [Id] int NOT NULL IDENTITY(1,1),
        [TourId] int NOT NULL,
        [POIId] int NOT NULL,
        [OrderIndex] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_TourPOIs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TourPOIs_Tours] FOREIGN KEY ([TourId]) REFERENCES [Tours]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TourPOIs_POIs] FOREIGN KEY ([POIId]) REFERENCES [POIs]([Id]) ON DELETE CASCADE,
        CONSTRAINT [UQ_TourPOIs_Tour_POI] UNIQUE ([TourId], [POIId])
    );
END
GO

-- Create indexes for TourPOIs (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TourPOIs_TourId' AND object_id = OBJECT_ID('TourPOIs'))
    CREATE INDEX [IX_TourPOIs_TourId] ON [TourPOIs]([TourId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TourPOIs_POIId' AND object_id = OBJECT_ID('TourPOIs'))
    CREATE INDEX [IX_TourPOIs_POIId] ON [TourPOIs]([POIId]);
GO

-- Audios table (đúng cấu trúc Audio.cs - đơn giản hơn)
IF OBJECT_ID(N'[Audios]', N'U') IS NULL
BEGIN
    CREATE TABLE [Audios] (
        [Id] int NOT NULL IDENTITY(1,1),
        [POIId] int NOT NULL,
        [Language] nvarchar(10) NOT NULL DEFAULT 'vi',
        [AudioPath] nvarchar(500) NOT NULL,
        [Duration] int NOT NULL DEFAULT 0,
        [ScriptText] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_Audios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Audios_POIs] FOREIGN KEY ([POIId]) REFERENCES [POIs]([Id]) ON DELETE CASCADE
    );
END
GO

-- Create indexes for Audios (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Audios_POIId' AND object_id = OBJECT_ID('Audios'))
    CREATE INDEX [IX_Audios_POIId] ON [Audios]([POIId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Audios_Language' AND object_id = OBJECT_ID('Audios'))
    CREATE INDEX [IX_Audios_Language] ON [Audios]([Language]);
GO

-- FavoritePOIs table (khớp với FavoritePOI.cs)
IF OBJECT_ID(N'[FavoritePOIs]', N'U') IS NULL
BEGIN
    CREATE TABLE [FavoritePOIs] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [POIId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_FavoritePOIs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FavoritePOIs_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FavoritePOIs_POIs] FOREIGN KEY ([POIId]) REFERENCES [POIs]([Id]) ON DELETE CASCADE,
        CONSTRAINT [UQ_FavoritePOIs_User_POI] UNIQUE ([UserId], [POIId])
    );
END
GO

-- Create indexes for FavoritePOIs (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FavoritePOIs_UserId' AND object_id = OBJECT_ID('FavoritePOIs'))
    CREATE INDEX [IX_FavoritePOIs_UserId] ON [FavoritePOIs]([UserId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FavoritePOIs_POIId' AND object_id = OBJECT_ID('FavoritePOIs'))
    CREATE INDEX [IX_FavoritePOIs_POIId] ON [FavoritePOIs]([POIId]);
GO

-- UserLocationLogs table (khớp với UserLocationLog.cs)
IF OBJECT_ID(N'[UserLocationLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [UserLocationLogs] (
        [Id] int NOT NULL IDENTITY(1,1),
        [DeviceId] nvarchar(200) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [Timestamp] datetime2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_UserLocationLogs] PRIMARY KEY ([Id])
    );
END
GO

-- Create indexes for UserLocationLogs (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UserLocationLogs_DeviceId' AND object_id = OBJECT_ID('UserLocationLogs'))
    CREATE INDEX [IX_UserLocationLogs_DeviceId] ON [UserLocationLogs]([DeviceId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UserLocationLogs_Timestamp' AND object_id = OBJECT_ID('UserLocationLogs'))
    CREATE INDEX [IX_UserLocationLogs_Timestamp] ON [UserLocationLogs]([Timestamp]);
GO

-- NarrationLogs table (khớp với NarrationLog.cs)
IF OBJECT_ID(N'[NarrationLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [NarrationLogs] (
        [Id] int NOT NULL IDENTITY(1,1),
        [POIId] int NOT NULL,
        [AudioId] int NULL,
        [TriggerType] nvarchar(100) NOT NULL,
        [Timestamp] datetime2 NOT NULL DEFAULT GETDATE(),
        [DeviceId] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_NarrationLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NarrationLogs_POIs] FOREIGN KEY ([POIId]) REFERENCES [POIs]([Id]) ON DELETE CASCADE
    );
END
GO

-- Create indexes for NarrationLogs (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NarrationLogs_POIId' AND object_id = OBJECT_ID('NarrationLogs'))
    CREATE INDEX [IX_NarrationLogs_POIId] ON [NarrationLogs]([POIId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NarrationLogs_Timestamp' AND object_id = OBJECT_ID('NarrationLogs'))
    CREATE INDEX [IX_NarrationLogs_Timestamp] ON [NarrationLogs]([Timestamp]);
GO

-- Bookings table
IF OBJECT_ID(N'[Bookings]', N'U') IS NULL
BEGIN
    CREATE TABLE [Bookings] (
        [Id] int NOT NULL IDENTITY(1,1),
        [TourId] int NOT NULL,
        [UserId] int NOT NULL,
        [NumberOfParticipants] int NOT NULL DEFAULT 1,
        [BookingDate] datetime2 NOT NULL DEFAULT GETDATE(),
        [TourDate] datetime2 NOT NULL,
        [TotalPrice] decimal(18,2) NOT NULL DEFAULT 0,
        [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
        [Notes] nvarchar(1000) NULL,
        [Code] varchar(50) NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bookings_Tours] FOREIGN KEY ([TourId]) REFERENCES [Tours]([Id]),
        CONSTRAINT [FK_Bookings_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]),
        CONSTRAINT [UQ_Bookings_Code] UNIQUE ([Code])
    );
END
GO

-- Create indexes for Bookings (with IF NOT EXISTS check)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_TourId' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX [IX_Bookings_TourId] ON [Bookings]([TourId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_UserId' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX [IX_Bookings_UserId] ON [Bookings]([UserId]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_Status' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX [IX_Bookings_Status] ON [Bookings]([Status]);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_Code' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX [IX_Bookings_Code] ON [Bookings]([Code]);
GO

-- =============================================
-- 2. INSERT SAMPLE DATA
-- =============================================

-- Users (Admin first, then others)
-- MẬT KHẨU MẶC ĐỊNH CHO TẤT CẢ TÀI KHOẢN DƯỚI ĐÂY LÀ: admin123
SET IDENTITY_INSERT [Users] ON;

-- Admin user
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 1)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Address], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (1, N'Quản trị viên', 'admin', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'admin@tourapp.vn', '0901234567', N'HCM', 'Admin', 1, GETDATE(), '#U1001');

-- Regular users
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 2)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (2, N'Cuong', 'cuong', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'pcz5@gmail.com', '0773980690', 'Customer', 1, GETDATE(), '#U1002');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 3)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (3, N'Cuong', 'cuongowner', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'phamcuong80690@gmail.com', '0773980690', 'RestaurantOwner', 1, GETDATE(), '#U1003');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 4)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (4, N'Hien', 'hien', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', '123@gmail.com', '0255445544', 'RestaurantOwner', 1, GETDATE(), '#U1004');

SET IDENTITY_INSERT [Users] OFF;

-- Reset identity to continue from 5
DBCC CHECKIDENT ('[Users]', RESEED, 4);
GO

-- POIs (11 POIs with codes #P1001 to #P1011)
SET IDENTITY_INSERT [POIs] ON;

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 1)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (1, N'Ốc Oanh', N'Hải sản tươi sống, nêm nước chấm đặc biệt', 10.759902, 106.701834, 80, 1, N'534 Vĩnh Khánh, Q4', '/images/pois/oc-oanh.jpg', '10:00-22:00', 1, 4.5, 'Approved', 3, '#P1001');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 2)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (2, N'Ốc Thảo', N'Không gian rộng rãi, menu đa dạng', 10.7618517, 106.7022358, 80, 1, N'528 Vĩnh Khánh, Q4', '/images/pois/oc-thao.jpg', '10:00-23:00', 1, 4.3, 'Approved', 3, '#P1002');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 3)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (3, N'Ốc Đào 2', N'Hơn 30 loại ốc, nêm đậm đà', 10.7612137, 106.7048739, 80, 1, N'550 Vĩnh Khánh, Q4', '/images/pois/oc-dao-2.jpg', '09:00-23:00', 1, 4.6, 'Approved', 3, '#P1003');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 4)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (4, N'Ốc Vũ', N'Quán ốc mở khuya, giá sinh viên', 10.7614237, 106.7025894, 80, 1, N'512 Vĩnh Khánh, Q4', '/images/pois/oc-vu.jpg', '17:00-02:00', 1, 4.2, 'Approved', 3, '#P1004');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 5)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (5, N'Làng Restaurant', N'Thực đơn của Làng là ẩm thực Việt', 10.76161627, 106.7048494, 80, 1, N'560 Vĩnh Khánh, Q4', '/images/pois/lang-restaurant.jpg', '10:00-22:00', 1, 4.7, 'Approved', 3, '#P1005');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 6)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (6, N'Ốc 35k', N'Điểm nổi bật của Ốc 35k với các món hải sản tươi ngon, giá cả phải chăng', 10.7614812, 106.702496, 80, 1, N'612 Vĩnh Khánh, Q4', '/images/pois/oc-35k.jpg', '10:00-22:00', 1, 4.5, 'Approved', 4, '#P1006');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 7)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (7, N'Ốc 662', N'Quán Ốc 662 là một quán ốc bình dân nổi tiếng', 10.7634607, 106.701916, 80, 1, N'662 Vĩnh Khánh, Q4', '/images/pois/oc-662.jpg', '16:00-23:00', 1, 4.3, 'Approved', 4, '#P1007');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 8)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (8, N'Nem Nướng Đặc Sản Quê Nhà', N'Nem Nướng Đặc Sản Quê Nhà với hương vị truyền thống', 10.7612055, 106.7037086, 80, 1, N'620 Vĩnh Khánh, Q4', '/images/pois/nem-nuong.jpg', '09:00-21:00', 1, 4.6, 'Approved', 4, '#P1008');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 9)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (9, N'Thế Giới Bò - Nướng, Sốt và Lẩu', N'Thế Giới Bò nổi bật với các món bò nướng, sốt đặc biệt và lẩu', 10.7640394, 106.69937, 80, 1, N'580 Vĩnh Khánh, Q4', '/images/pois/the-gioi-bo.jpg', '10:00-22:00', 1, 4.4, 'Approved', 4, '#P1009');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 10)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (10, N'Bánh Mì Que - Pizza Đà Nẵng QUIN', N'Bánh ngon nha, nóng giòn và đầy đặn', 10.7637277, 106.7017558, 80, 1, N'590 Vĩnh Khánh, Q4', '/images/pois/banh-mi-que.jpg', '07:00-21:00', 1, 4.5, 'Approved', 4, '#P1010');

-- POI 11: Sườn nướng muối ớt (Cuong, chờ duyệt)
IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 11)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (11, N'Sườn nướng muối ớt', N'Thiên đường nướng với hơn 100 món nướng đặc sắc', 10.7608097, 106.7035061, 80, 1, N'712 Vĩnh Khánh, Q4', '/images/pois/suon-nuong.jpg', '10:00-22:00', 1, 4.4, 'Pending', 3, '#P1011');

SET IDENTITY_INSERT [POIs] OFF;
DBCC CHECKIDENT ('[POIs]', RESEED, 11);
GO



-- Audios (đúng cấu trúc Audio.cs - mỗi POI có 1 audio Tiếng Việt)
SET IDENTITY_INSERT [Audios] ON;

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 1)
    INSERT INTO [Audios] ([Id], [POIId], [Language], [AudioPath], [Duration], [ScriptText], [IsActive], [CreatedAt])
    VALUES (1, 1, 'vi', '/audio/oc-oanh.mp3', 120, N'Giới thiệu Ốc Oanh - Hải sản tươi sống, nêm nước chấm đặc biệt', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 2)
    INSERT INTO [Audios] ([Id], [POIId], [Language], [AudioPath], [Duration], [ScriptText], [IsActive], [CreatedAt])
    VALUES (2, 2, 'vi', '/audio/oc-thao.mp3', 110, N'Giới thiệu Ốc Thảo - Không gian rộng rãi, menu đa dạng', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 3)
    INSERT INTO [Audios] ([Id], [POIId], [Language], [AudioPath], [Duration], [ScriptText], [IsActive], [CreatedAt])
    VALUES (3, 3, 'vi', '/audio/oc-dao-2.mp3', 130, N'Giới thiệu Ốc Đào 2 - Hơn 30 loại ốc, nêm đậm đà', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 4)
    INSERT INTO [Audios] ([Id], [POIId], [Language], [AudioPath], [Duration], [ScriptText], [IsActive], [CreatedAt])
    VALUES (4, 4, 'vi', '/audio/oc-vu.mp3', 100, N'Giới thiệu Ốc Vũ - Quán ốc mở khuya, giá sinh viên', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 5)
    INSERT INTO [Audios] ([Id], [POIId], [Language], [AudioPath], [Duration], [ScriptText], [IsActive], [CreatedAt])
    VALUES (5, 5, 'vi', '/audio/lang-restaurant.mp3', 140, N'Giới thiệu Làng Restaurant - Thực đơn của Làng là ẩm thực Việt', 1, GETDATE());

SET IDENTITY_INSERT [Audios] OFF;
DBCC CHECKIDENT ('[Audios]', RESEED, 5);
GO



-- =============================================
-- 3. CREATE __EFMigrationsHistory TABLE
-- =============================================
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    
    -- Insert migration entries
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('InitialCreate', '9.0.0');
END
GO

PRINT 'Database created successfully with sample data!';

