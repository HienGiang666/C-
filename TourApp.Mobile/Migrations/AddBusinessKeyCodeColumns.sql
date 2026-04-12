-- =============================================
-- MIGRATION: Thêm cột Code (Business Key) vào các bảng
-- =============================================
-- Script này thêm cột Code VARCHAR vào POIs, Tours, Bookings, Users
-- và tạo UNIQUE INDEX để đảm bảo không trùng mã
-- =============================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT '=== Bắt đầu Migration: Add Business Key Code Columns ===';

    -- 1. Thêm cột Code vào bảng POIs
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = 'Code' AND Object_ID = Object_ID(N'POIs'))
    BEGIN
        ALTER TABLE POIs ADD Code NVARCHAR(20) NULL;
        PRINT '✓ Đã thêm cột Code vào POIs';
    END
    ELSE
        PRINT '✓ Cột Code đã tồn tại trong POIs';

    -- 2. Thêm cột Code vào bảng Tours
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = 'Code' AND Object_ID = Object_ID(N'Tours'))
    BEGIN
        ALTER TABLE Tours ADD Code NVARCHAR(20) NULL;
        PRINT '✓ Đã thêm cột Code vào Tours';
    END
    ELSE
        PRINT '✓ Cột Code đã tồn tại trong Tours';

    -- 3. Thêm cột Code vào bảng Bookings
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = 'Code' AND Object_ID = Object_ID(N'Bookings'))
    BEGIN
        ALTER TABLE Bookings ADD Code NVARCHAR(20) NULL;
        PRINT '✓ Đã thêm cột Code vào Bookings';
    END
    ELSE
        PRINT '✓ Cột Code đã tồn tại trong Bookings';

    -- 4. Thêm cột Code vào bảng Users
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = 'Code' AND Object_ID = Object_ID(N'Users'))
    BEGIN
        ALTER TABLE Users ADD Code NVARCHAR(20) NULL;
        PRINT '✓ Đã thêm cột Code vào Users';
    END
    ELSE
        PRINT '✓ Cột Code đã tồn tại trong Users';

    -- 5. Khởi tạo giá trị Code mặc định cho data hiện có
    PRINT '=== Khởi tạo giá trị Code mặc định ===';
    
    -- POIs: #P{Id}
    UPDATE POIs SET Code = '#P' + CAST(Id AS NVARCHAR) WHERE Code IS NULL;
    PRINT '✓ Đã khởi tạo Code cho POIs';

    -- Tours: TR-{Id}
    UPDATE Tours SET Code = 'TR-' + CAST(Id AS NVARCHAR) WHERE Code IS NULL;
    PRINT '✓ Đã khởi tạo Code cho Tours';

    -- Bookings: BK-{Id}
    UPDATE Bookings SET Code = 'BK-' + CAST(Id AS NVARCHAR) WHERE Code IS NULL;
    PRINT '✓ Đã khởi tạo Code cho Bookings';

    -- Users: #U{Id}
    UPDATE Users SET Code = '#U' + CAST(Id AS NVARCHAR) WHERE Code IS NULL;
    PRINT '✓ Đã khởi tạo Code cho Users';

    -- 6. Thay đổi cột Code thành NOT NULL sau khi đã có data
    ALTER TABLE POIs ALTER COLUMN Code NVARCHAR(20) NOT NULL;
    ALTER TABLE Tours ALTER COLUMN Code NVARCHAR(20) NOT NULL;
    ALTER TABLE Bookings ALTER COLUMN Code NVARCHAR(20) NOT NULL;
    ALTER TABLE Users ALTER COLUMN Code NVARCHAR(20) NOT NULL;
    PRINT '✓ Đã set Code thành NOT NULL';

    -- 7. Tạo UNIQUE INDEX cho cột Code
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_POIs_Code')
        CREATE UNIQUE INDEX IX_POIs_Code ON POIs(Code);
    
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tours_Code')
        CREATE UNIQUE INDEX IX_Tours_Code ON Tours(Code);
    
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Code')
        CREATE UNIQUE INDEX IX_Bookings_Code ON Bookings(Code);
    
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Code')
        CREATE UNIQUE INDEX IX_Users_Code ON Users(Code);
    
    PRINT '✓ Đã tạo UNIQUE INDEX cho Code columns';

    COMMIT TRANSACTION;
    PRINT '=== Migration hoàn tất thành công ===';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT '=== LỖI Migration ===';
    PRINT ERROR_MESSAGE();
    PRINT 'Line: ' + CAST(ERROR_LINE() AS NVARCHAR);
    THROW;
END CATCH;
