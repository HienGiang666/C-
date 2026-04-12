-- =============================================
-- THÊM CỘT CODE VÀO CÁC BẢNG (NẾU CHƯA CÓ)
-- =============================================

-- 1. Thêm cột Code vào Users
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE Name = N'Code' AND Object_ID = Object_ID(N'Users'))
BEGIN
    ALTER TABLE Users ADD Code VARCHAR(50) NULL;
    CREATE UNIQUE INDEX IX_Users_Code ON Users(Code) WHERE Code IS NOT NULL;
    PRINT 'Đã thêm cột Code vào Users';
END
ELSE
    PRINT 'Cột Code đã tồn tại trong Users';
GO

-- 2. Thêm cột Code vào POIs
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE Name = N'Code' AND Object_ID = Object_ID(N'POIs'))
BEGIN
    ALTER TABLE POIs ADD Code VARCHAR(50) NULL;
    CREATE UNIQUE INDEX IX_POIs_Code ON POIs(Code) WHERE Code IS NOT NULL;
    PRINT 'Đã thêm cột Code vào POIs';
END
ELSE
    PRINT 'Cột Code đã tồn tại trong POIs';
GO

-- 3. Thêm cột Code vào Tours
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE Name = N'Code' AND Object_ID = Object_ID(N'Tours'))
BEGIN
    ALTER TABLE Tours ADD Code VARCHAR(50) NULL;
    CREATE UNIQUE INDEX IX_Tours_Code ON Tours(Code) WHERE Code IS NOT NULL;
    PRINT 'Đã thêm cột Code vào Tours';
END
ELSE
    PRINT 'Cột Code đã tồn tại trong Tours';
GO

-- 4. Thêm cột Code vào Audios
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE Name = N'Code' AND Object_ID = Object_ID(N'Audios'))
BEGIN
    ALTER TABLE Audios ADD Code VARCHAR(50) NULL;
    CREATE UNIQUE INDEX IX_Audios_Code ON Audios(Code) WHERE Code IS NOT NULL;
    PRINT 'Đã thêm cột Code vào Audios';
END
ELSE
    PRINT 'Cột Code đã tồn tại trong Audios';
GO

-- 5. Thêm cột Code vào Bookings
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE Name = N'Code' AND Object_ID = Object_ID(N'Bookings'))
BEGIN
    ALTER TABLE Bookings ADD Code VARCHAR(50) NULL;
    CREATE UNIQUE INDEX IX_Bookings_Code ON Bookings(Code) WHERE Code IS NOT NULL;
    PRINT 'Đã thêm cột Code vào Bookings';
END
ELSE
    PRINT 'Cột Code đã tồn tại trong Bookings';
GO

PRINT '';
PRINT 'Hoàn tất! Bây giờ bạn có thể chạy UpdateAllCodes.sql để cập nhật giá trị Code.';
GO
