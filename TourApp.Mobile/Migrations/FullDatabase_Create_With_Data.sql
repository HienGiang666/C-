-- =============================================
-- TOURAPP DATABASE - FULL CREATE SCRIPT WITH SAMPLE DATA
-- For: SQL Server (can be adapted for MySQL)
-- Includes: All tables, relationships, indexes, and sample data
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
-- 1. CREATE TABLES
-- =============================================

-- Users table
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
        [Code] varchar(50) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Users_Username] UNIQUE ([Username]),
        CONSTRAINT [UQ_Users_Email] UNIQUE ([Email]),
        CONSTRAINT [UQ_Users_Code] UNIQUE ([Code])
    );
    CREATE INDEX [IX_Users_Role] ON [Users]([Role]);
    CREATE INDEX [IX_Users_Code] ON [Users]([Code]);
END
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
    CREATE INDEX [IX_POIs_OwnerUserId] ON [POIs]([OwnerUserId]);
    CREATE INDEX [IX_POIs_Code] ON [POIs]([Code]);
    CREATE INDEX [IX_POIs_ApprovalStatus] ON [POIs]([ApprovalStatus]);
END
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
    CREATE INDEX [IX_Tours_Code] ON [Tours]([Code]);
    CREATE INDEX [IX_Tours_IsActive] ON [Tours]([IsActive]);
END
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
    CREATE INDEX [IX_TourPOIs_TourId] ON [TourPOIs]([TourId]);
    CREATE INDEX [IX_TourPOIs_POIId] ON [TourPOIs]([POIId]);
END
GO

-- Audios table
IF OBJECT_ID(N'[Audios]', N'U') IS NULL
BEGIN
    CREATE TABLE [Audios] (
        [Id] int NOT NULL IDENTITY(1,1),
        [POIId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [VietnameseUrl] nvarchar(500) NULL,
        [EnglishUrl] nvarchar(500) NULL,
        [ChineseUrl] nvarchar(500) NULL,
        [JapaneseUrl] nvarchar(500) NULL,
        [Duration] int NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [Code] varchar(50) NULL,
        CONSTRAINT [PK_Audios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Audios_POIs] FOREIGN KEY ([POIId]) REFERENCES [POIs]([Id]) ON DELETE CASCADE,
        CONSTRAINT [UQ_Audios_Code] UNIQUE ([Code])
    );
    CREATE INDEX [IX_Audios_POIId] ON [Audios]([POIId]);
    CREATE INDEX [IX_Audios_Code] ON [Audios]([Code]);
END
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
    CREATE INDEX [IX_Bookings_TourId] ON [Bookings]([TourId]);
    CREATE INDEX [IX_Bookings_UserId] ON [Bookings]([UserId]);
    CREATE INDEX [IX_Bookings_Status] ON [Bookings]([Status]);
    CREATE INDEX [IX_Bookings_Code] ON [Bookings]([Code]);
END
GO

-- Activity Logs table
IF OBJECT_ID(N'[ActivityLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [ActivityLogs] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NULL,
        [EntityId] int NULL,
        [Description] nvarchar(500) NULL,
        [Timestamp] datetime2 NOT NULL DEFAULT GETDATE(),
        [IpAddress] nvarchar(50) NULL,
        CONSTRAINT [PK_ActivityLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id])
    );
    CREATE INDEX [IX_ActivityLogs_Timestamp] ON [ActivityLogs]([Timestamp]);
    CREATE INDEX [IX_ActivityLogs_UserId] ON [ActivityLogs]([UserId]);
END
GO

-- =============================================
-- 2. INSERT SAMPLE DATA
-- =============================================

-- Users (Admin first, then others)
SET IDENTITY_INSERT [Users] ON;

-- Admin user #U1000
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 1)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Address], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (1, N'Quản trị viên', 'admin', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'admin@tourapp.vn', '0901234567', N'HCM', 'Admin', 1, GETDATE(), '#U1000');

-- Regular users starting from #U1001
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 2)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (2, N'Phạm Cường', 'cuong', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'phamcuong80690@gmail.com', '0773980690', 'Customer', 1, GETDATE(), '#U1001');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 3)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (3, N'Cường Restaurant', 'cuongowner', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'phamcuong80690@gmail.com', '0773980690', 'RestaurantOwner', 1, GETDATE(), '#U1002');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 4)
    INSERT INTO [Users] ([Id], [FullName], [Username], [PasswordHash], [Email], [PhoneNumber], [Role], [IsActive], [CreatedAt], [Code])
    VALUES (4, N'Hiền', 'hien', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', '123@gmail.com', '0255445544', 'RestaurantOwner', 1, GETDATE(), '#U1003');

SET IDENTITY_INSERT [Users] OFF;

-- Reset identity to continue from 5
DBCC CHECKIDENT ('[Users]', RESEED, 4);
GO

-- POIs (10 POIs with codes #P1001 to #P1010)
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
    VALUES (6, N'Sườn nướng muối ớt', N'Đặc sản sườn nướng, cơm chiên', 10.76161627, 106.7048494, 80, 1, N'712 Vĩnh Khánh, Q4', '/images/pois/suon-nuong.jpg', '10:00-22:00', 1, 4.4, 'Pending', 4, '#P1006');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 7)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (7, N'Bánh canh cua', N'Bánh canh sợi dai, nhiều thịt cua', 10.760, 106.703, 80, 1, N'480 Vĩnh Khánh, Q4', '/images/pois/banh-canh.jpg', '06:00-21:00', 1, 4.5, 'Approved', 4, '#P1007');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 8)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (8, N'Lẩu dê', N'Lẩu dê thuốc bắc, nhậu khuya', 10.758, 106.705, 80, 1, N'600 Vĩnh Khánh, Q4', '/images/pois/lau-de.jpg', '17:00-02:00', 1, 4.3, 'Approved', 4, '#P1008');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 9)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (9, N'Bún riêu', N'Bún riêu cua đồng, chả cá', 10.762, 106.700, 80, 1, N'450 Vĩnh Khánh, Q4', '/images/pois/bun-rieu.jpg', '06:00-20:00', 1, 4.4, 'Approved', 4, '#P1009');

IF NOT EXISTS (SELECT 1 FROM [POIs] WHERE [Id] = 10)
    INSERT INTO [POIs] ([Id], [Name], [Description], [Latitude], [Longitude], [Radius], [Priority], [Address], [ImageUrl], [OpenTime], [IsActive], [Rating], [ApprovalStatus], [OwnerUserId], [Code])
    VALUES (10, N'Chè truyền thống', N'Chè thập cẩm, chè chuối, chè bưởi', 10.7615, 106.7015, 80, 1, N'500 Vĩnh Khánh, Q4', '/images/pois/che.jpg', '14:00-22:00', 1, 4.6, 'Approved', 4, '#P1010');

SET IDENTITY_INSERT [POIs] OFF;
DBCC CHECKIDENT ('[POIs]', RESEED, 10);
GO

-- Tours (3 tours with codes TR-1, TR-2, TR-3)
SET IDENTITY_INSERT [Tours] ON;

IF NOT EXISTS (SELECT 1 FROM [Tours] WHERE [Id] = 1)
    INSERT INTO [Tours] ([Id], [Name], [Description], [Price], [Duration], [Destination], [MaxParticipants], [ImageUrl], [CreatedAt], [IsActive], [SearchKeywords], [Code])
    VALUES (1, N'Tour Ẩm Thực Vĩnh Khánh - Con Đường Ốc', 
            N'Khám phá con phố ẩm thực nổi tiếng nhất Quận 4 - đường Vĩnh Khánh. Thưởng thức đặc sản ốc, hải sản tươi sống và các món nhậu dân dã.',
            250000, 1, N'Đường Vĩnh Khánh, Quận 4, TP.HCM', 20, 
            '/images/tours/vinh-khanh-oc.jpg', GETDATE(), 0, 
            N'tour ẩm thực vĩnh khánh ốc oanh thảo đào hải sản', 'TR-1');

IF NOT EXISTS (SELECT 1 FROM [Tours] WHERE [Id] = 2)
    INSERT INTO [Tours] ([Id], [Name], [Description], [Price], [Duration], [Destination], [MaxParticipants], [ImageUrl], [CreatedAt], [IsActive], [SearchKeywords], [Code])
    VALUES (2, N'Tour Ẩm Thực Buổi Sáng Quận 4', 
            N'Trải nghiệm ẩm thực sáng tại Quận 4 với bánh canh cua, bún riêu, chè truyền thống. Phù hợp cho người thích khám phá văn hóa địa phương.',
            150000, 1, N'Quận 4, TP.HCM', 15, 
            '/images/tours/buoi-sang.jpg', GETDATE(), 1, 
            N'tour buổi sáng bánh canh bún riêu chè quận 4', 'TR-2');

IF NOT EXISTS (SELECT 1 FROM [Tours] WHERE [Id] = 3)
    INSERT INTO [Tours] ([Id], [Name], [Description], [Price], [Duration], [Destination], [MaxParticipants], [ImageUrl], [CreatedAt], [IsActive], [SearchKeywords], [Code])
    VALUES (3, N'Tour Xóm Chiếu - Chợ Đêm Ẩm Thực', 
            N'Khám phá chợ đêm Xóm Chiếu với lẩu dê, sườn nướng, các món nhậu đêm. Trải nghiệm cuộc sống về đêm của người dân Quận 4.',
            200000, 1, N'Xóm Chiếu, Quận 4, TP.HCM', 25, 
            '/images/tours/cho-dem.jpg', GETDATE(), 1, 
            N'tour chợ đêm xóm chiếu lẩu dê sườn nướng quận 4', 'TR-3');

SET IDENTITY_INSERT [Tours] OFF;
DBCC CHECKIDENT ('[Tours]', RESEED, 3);
GO

-- Tour-POI Relationships (which POIs are in which tours)
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 1 AND [POIId] = 1)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (1, 1, 0);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 1 AND [POIId] = 2)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (1, 2, 1);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 1 AND [POIId] = 3)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (1, 3, 2);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 1 AND [POIId] = 4)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (1, 4, 3);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 1 AND [POIId] = 5)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (1, 5, 4);

IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 2 AND [POIId] = 7)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (2, 7, 0);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 2 AND [POIId] = 9)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (2, 9, 1);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 2 AND [POIId] = 10)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (2, 10, 2);

IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 3 AND [POIId] = 6)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (3, 6, 0);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 3 AND [POIId] = 8)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (3, 8, 1);
IF NOT EXISTS (SELECT 1 FROM [TourPOIs] WHERE [TourId] = 3 AND [POIId] = 5)
    INSERT INTO [TourPOIs] ([TourId], [POIId], [OrderIndex]) VALUES (3, 5, 2);
GO

-- Audios (6 audios with codes #A1001 to #A1006)
SET IDENTITY_INSERT [Audios] ON;

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 1)
    INSERT INTO [Audios] ([Id], [POIId], [Title], [Description], [VietnameseUrl], [EnglishUrl], [ChineseUrl], [JapaneseUrl], [Duration], [IsActive], [Code])
    VALUES (1, 1, N'Giới thiệu Ốc Oanh', N'Thuật minh về lịch sử và đặc sản ốc tại quán Ốc Oanh',
            '/audio/oc-oanh-vn.mp3', '/audio/oc-oanh-en.mp3', '/audio/oc-oanh-cn.mp3', '/audio/oc-oanh-jp.mp3', 120, 1, '#A1001');

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 2)
    INSERT INTO [Audios] ([Id], [POIId], [Title], [Description], [VietnameseUrl], [EnglishUrl], [ChineseUrl], [JapaneseUrl], [Duration], [IsActive], [Code])
    VALUES (2, 2, N'Giới thiệu Ốc Thảo', N'Thuật minh về không gian và menu đa dạng tại Ốc Thảo',
            '/audio/oc-thao-vn.mp3', '/audio/oc-thao-en.mp3', '/audio/oc-thao-cn.mp3', '/audio/oc-thao-jp.mp3', 110, 1, '#A1002');

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 3)
    INSERT INTO [Audios] ([Id], [POIId], [Title], [Description], [VietnameseUrl], [EnglishUrl], [ChineseUrl], [JapaneseUrl], [Duration], [IsActive], [Code])
    VALUES (3, 3, N'Giới thiệu Ốc Đào 2', N'Thuật minh về hơn 30 loại ốc và cách nêm nước chấm',
            '/audio/oc-dao-vn.mp3', '/audio/oc-dao-en.mp3', '/audio/oc-dao-cn.mp3', '/audio/oc-dao-jp.mp3', 130, 1, '#A1003');

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 4)
    INSERT INTO [Audios] ([Id], [POIId], [Title], [Description], [VietnameseUrl], [EnglishUrl], [ChineseUrl], [JapaneseUrl], [Duration], [IsActive], [Code])
    VALUES (4, 4, N'Giới thiệu Ốc Vũ', N'Thuật minh về quán ốc mở khuya giá sinh viên',
            '/audio/oc-vu-vn.mp3', '/audio/oc-vu-en.mp3', '/audio/oc-vu-cn.mp3', '/audio/oc-vu-jp.mp3', 100, 1, '#A1004');

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 5)
    INSERT INTO [Audios] ([Id], [POIId], [Title], [Description], [VietnameseUrl], [EnglishUrl], [ChineseUrl], [JapaneseUrl], [Duration], [IsActive], [Code])
    VALUES (5, 5, N'Giới thiệu Làng Restaurant', N'Thuật minh về ẩm thực Việt Nam tại Làng Restaurant',
            '/audio/lang-restaurant-vn.mp3', '/audio/lang-restaurant-en.mp3', '/audio/lang-restaurant-cn.mp3', '/audio/lang-restaurant-jp.mp3', 140, 1, '#A1005');

IF NOT EXISTS (SELECT 1 FROM [Audios] WHERE [Id] = 6)
    INSERT INTO [Audios] ([Id], [POIId], [Title], [Description], [VietnameseUrl], [EnglishUrl], [ChineseUrl], [JapaneseUrl], [Duration], [IsActive], [Code])
    VALUES (6, 6, N'Giới thiệu Sườn nướng muối ớt', N'Thuật minh về đặc sản sườn nướng và cơm chiên',
            '/audio/suon-nuong-vn.mp3', '/audio/suon-nuong-en.mp3', '/audio/suon-nuong-cn.mp3', '/audio/suon-nuong-jp.mp3', 115, 1, '#A1006');

SET IDENTITY_INSERT [Audios] OFF;
DBCC CHECKIDENT ('[Audios]', RESEED, 6);
GO

-- Bookings (3 sample bookings)
SET IDENTITY_INSERT [Bookings] ON;

IF NOT EXISTS (SELECT 1 FROM [Bookings] WHERE [Id] = 1)
    INSERT INTO [Bookings] ([Id], [TourId], [UserId], [NumberOfParticipants], [BookingDate], [TourDate], [TotalPrice], [Status], [Notes], [Code])
    VALUES (1, 1, 2, 2, GETDATE(), DATEADD(day, 7, GETDATE()), 500000, 'Confirmed', N'Đặt cho 2 người, có trẻ em', 'BK-1');

IF NOT EXISTS (SELECT 1 FROM [Bookings] WHERE [Id] = 2)
    INSERT INTO [Bookings] ([Id], [TourId], [UserId], [NumberOfParticipants], [BookingDate], [TourDate], [TotalPrice], [Status], [Notes], [Code])
    VALUES (2, 2, 2, 1, GETDATE(), DATEADD(day, 5, GETDATE()), 150000, 'Pending', N'Đặt cho 1 người', 'BK-2');

IF NOT EXISTS (SELECT 1 FROM [Bookings] WHERE [Id] = 3)
    INSERT INTO [Bookings] ([Id], [TourId], [UserId], [NumberOfParticipants], [BookingDate], [TourDate], [TotalPrice], [Status], [Notes], [Code])
    VALUES (3, 1, 3, 4, GETDATE(), DATEADD(day, 10, GETDATE()), 1000000, 'Confirmed', N'Đặt cho nhóm 4 người', 'BK-3');

SET IDENTITY_INSERT [Bookings] OFF;
DBCC CHECKIDENT ('[Bookings]', RESEED, 3);
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
PRINT '';
PRINT 'Sample accounts:';
PRINT '  - Admin: admin/admin (Code: #U1000)';
PRINT '  - User: cuong/cuong (Code: #U1001)';
PRINT '  - Restaurant Owner: cuongowner/cuong (Code: #U1002)';
PRINT '  - Restaurant Owner: hien/hien (Code: #U1003)';
PRINT '';
PRINT 'POIs: #P1001 to #P1010';
PRINT 'Audios: #A1001 to #A1006';
PRINT 'Tours: TR-1, TR-2, TR-3';
PRINT 'Bookings: BK-1, BK-2, BK-3';
GO
