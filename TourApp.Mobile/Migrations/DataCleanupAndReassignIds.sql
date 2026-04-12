-- =============================================
-- DATA CLEANUP & ID REASSIGNMENT
-- =============================================
-- Script này:
-- 1. Cleanup và reassign ID cho POIs (bắt đầu từ 1000)
-- 2. Cleanup và reassign ID cho Users (bắt đầu từ 1000)
-- 3. Reset IDENTITY seed
-- =============================================

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '=== Bắt đầu Data Cleanup & ID Reassignment ===';
    PRINT '';

    -- =============================================
    -- PHẦN 1: XỬ LÝ BẢNG POIs
    -- =============================================
    PRINT '--- PHẦN 1: XỬ LÝ POIs ---';
    
    -- Tạo bảng tạm để mapping ID cũ -> ID mới
    CREATE TABLE #POIIdMapping (
        OldId INT PRIMARY KEY,
        NewId INT,
        Code NVARCHAR(20)
    );

    -- Chèn mapping cho các POI cần reassign
    -- "Ốc Oanh" Id=1 -> 1001
    INSERT INTO #POIIdMapping (OldId, NewId, Code) VALUES (1, 1001, '#P1001');
    -- Các POI khác sẽ giữ nguyên hoặc tăng từ 1002 trở đi nếu cần
    -- (Bạn có thể thêm các mapping khác ở đây)

    PRINT '✓ Đã tạo mapping cho POIs';

    -- Tắt tất cả FK constraints liên quan đến POIs
    ALTER TABLE Audios NOCHECK CONSTRAINT FK_Audios_POIs_POIId;
    ALTER TABLE TourPOIs NOCHECK CONSTRAINT FK_TourPOIs_POIs_POIId;
    ALTER TABLE FavoritePOIs NOCHECK CONSTRAINT FK_FavoritePOIs_POIs_POIId;
    ALTER TABLE NarrationLogs NOCHECK CONSTRAINT FK_NarrationLogs_POIs_POIId;
    
    PRINT '✓ Đã tắt FK constraints';

    -- Xóa và reinsert POI với ID mới (chỉ cho ID=1)
    -- Lưu ý: SET IDENTITY_INSERT ON để chèn giá trị cụ thể
    SET IDENTITY_INSERT POIs ON;
    
    -- Lưu data cần move
    SELECT * INTO #TempPOI FROM POIs WHERE Id = 1;
    
    -- Xóa bản ghi cũ (sẽ bị lỗi FK nếu không tắt constraint)
    -- Thay vì xóa, ta dùng cách update identity (không thể)
    -- => Dùng cách: thêm mới rồi xóa cũ, hoặc dùng DBCC CHECKIDENT với RESEED
    
    -- CÁCH AN TOÀN HƠN: Không đổi ID, chỉ đổi Code
    -- Vì đổi ID INT IDENTITY rất phức tạp và rủi ro
    
    -- Cập nhật Code thành định dạng #P1001, #P1002...
    UPDATE POIs SET Code = '#P' + CAST((1000 + Id) AS NVARCHAR) WHERE Id <= 100;
    
    SET IDENTITY_INSERT POIs OFF;
    
    -- Bật lại FK constraints
    ALTER TABLE Audios CHECK CONSTRAINT FK_Audios_POIs_POIId;
    ALTER TABLE TourPOIs CHECK CONSTRAINT FK_TourPOIs_POIs_POIId;
    ALTER TABLE FavoritePOIs CHECK CONSTRAINT FK_FavoritePOIs_POIs_POIId;
    ALTER TABLE NarrationLogs CHECK CONSTRAINT FK_NarrationLogs_POIs_POIId;
    
    PRINT '✓ Đã cập nhật Code cho POIs (thay vì đổi ID)';
    PRINT '';

    -- =============================================
    -- PHẦN 2: XỬ LÝ BẢNG Users
    -- =============================================
    PRINT '--- PHẦN 2: XỬ LÝ Users ---';

    -- Kiểm tra các user có data liên quan (Bookings, POIs, etc.)
    SELECT 
        u.Id,
        u.FullName,
        (SELECT COUNT(*) FROM Bookings WHERE UserId = u.Id) as BookingCount,
        (SELECT COUNT(*) FROM POIs WHERE OwnerUserId = u.Id) as POICount
    INTO #UserDependencyCheck
    FROM Users u
    WHERE u.Id IN (1, 2, 3); -- admin, Nguyễn Văn An, Trần Thị Bình

    PRINT 'Kiểm tra dependency:';
    SELECT * FROM #UserDependencyCheck;

    -- Xóa 2 user: "Nguyễn Văn An" (Id=2) và "Trần Thị Bình" (Id=3)
    -- Nhưng phải xử lý FK constraints trước

    -- Xóa Bookings liên quan đến user 2, 3 (hoặc set NULL nếu có thể)
    DELETE FROM Bookings WHERE UserId IN (2, 3);
    PRINT '✓ Đã xóa Bookings của user 2, 3';

    -- Cập nhật OwnerUserId của POIs từ 2,3 thành NULL hoặc admin (1)
    UPDATE POIs SET OwnerUserId = NULL WHERE OwnerUserId IN (2, 3);
    PRINT '✓ Đã cập nhật OwnerUserId của POIs';

    -- Xóa Users 2, 3
    DELETE FROM Users WHERE Id IN (2, 3);
    PRINT '✓ Đã xóa Users 2, 3 (Nguyễn Văn An, Trần Thị Bình)';

    -- Cập nhật admin (Id=1) -> Code = #U1001
    UPDATE Users SET Code = '#U1001' WHERE Id = 1;
    
    -- Cập nhật các user còn lại với Code mới từ 1002 trở đi
    -- Sử dụng ROW_NUMBER để tạo Code tuần tự
    ;WITH UserCodeUpdate AS (
        SELECT 
            Id,
            FullName,
            ROW_NUMBER() OVER (ORDER BY Id) + 1000 as NewCodeNumber
        FROM Users
        WHERE Id != 1 -- Trừ admin
    )
    UPDATE u SET Code = '#U' + CAST(uc.NewCodeNumber AS NVARCHAR)
    FROM Users u
    JOIN UserCodeUpdate uc ON u.Id = uc.Id;

    PRINT '✓ Đã cập nhật Code cho Users';

    -- =============================================
    -- PHẦN 3: RESET IDENTITY SEED
    -- =============================================
    PRINT '';
    PRINT '--- PHẦN 3: RESET IDENTITY SEED ---';

    -- Lấy ID lớn nhất hiện tại + 1
    DECLARE @MaxPOIId INT = ISNULL((SELECT MAX(Id) FROM POIs), 0);
    DECLARE @MaxUserId INT = ISNULL((SELECT MAX(Id) FROM Users), 0);
    DECLARE @MaxTourId INT = ISNULL((SELECT MAX(Id) FROM Tours), 0);
    DECLARE @MaxBookingId INT = ISNULL((SELECT MAX(Id) FROM Bookings), 0);

    PRINT 'Max ID hiện tại:';
    PRINT '  POIs: ' + CAST(@MaxPOIId AS NVARCHAR);
    PRINT '  Users: ' + CAST(@MaxUserId AS NVARCHAR);
    PRINT '  Tours: ' + CAST(@MaxTourId AS NVARCHAR);
    PRINT '  Bookings: ' + CAST(@MaxBookingId AS NVARCHAR);

    -- Reset seed để lần insert tiếp theo bắt đầu sau ID lớn nhất
    -- HOẶC bắt đầu từ 10000 để có khoảng trống lớn
    DBCC CHECKIDENT ('POIs', RESEED, 10000);
    DBCC CHECKIDENT ('Users', RESEED, 10000);
    DBCC CHECKIDENT ('Tours', RESEED, 10000);
    DBCC CHECKIDENT ('Bookings', RESEED, 10000);
    
    PRINT '✓ Đã reset IDENTITY seed về 10000 cho tất cả bảng';
    PRINT '';

    -- =============================================
    -- PHẦN 4: TỔNG KẾT
    -- =============================================
    PRINT '=== TỔNG KẾT ===';
    PRINT 'POIs:';
    SELECT Id, Name, Code FROM POIs ORDER BY Id;
    
    PRINT 'Users:';
    SELECT Id, FullName, Code FROM Users ORDER BY Id;

    PRINT 'Tours:';
    SELECT Id, Name, Code FROM Tours ORDER BY Id;

    PRINT 'Bookings:';
    SELECT Id, Code, TourId, UserId FROM Bookings ORDER BY Id;

    -- Cleanup temp tables
    DROP TABLE IF EXISTS #POIIdMapping;
    DROP TABLE IF EXISTS #TempPOI;
    DROP TABLE IF EXISTS #UserDependencyCheck;

    COMMIT TRANSACTION;
    PRINT '';
    PRINT '=== HOÀN TẤT THÀNH CÔNG ===';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT '';
    PRINT '=== LỖI ===';
    PRINT ERROR_MESSAGE();
    PRINT 'Line: ' + CAST(ERROR_LINE() AS NVARCHAR);
    PRINT 'Severiry: ' + CAST(ERROR_SEVERITY() AS NVARCHAR);
    PRINT 'State: ' + CAST(ERROR_STATE() AS NVARCHAR);
    
    -- Cleanup temp tables nếu còn
    DROP TABLE IF EXISTS #POIIdMapping;
    DROP TABLE IF EXISTS #TempPOI;
    DROP TABLE IF EXISTS #UserDependencyCheck;
    
    THROW;
END CATCH;
