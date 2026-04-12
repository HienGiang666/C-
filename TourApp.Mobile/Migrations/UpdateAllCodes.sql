-- =============================================
-- CẬP NHẬT TẤT CẢ CODE VỀ FORMAT CHUẨN
-- POI: #P1001-#P1010
-- User: #U1001 trở đi (admin #U1001)
-- Audio: #A1001 trở đi
-- Tour: TR-1, TR-2...
-- Booking: BK-1, BK-2...
-- =============================================

-- =============================================
-- 1. CẬP NHẬT POIs - #P1001 đến #P1010
-- =============================================
UPDATE POIs SET Code = '#P1001' WHERE Id = 1;
UPDATE POIs SET Code = '#P1002' WHERE Id = 2;
UPDATE POIs SET Code = '#P1003' WHERE Id = 3;
UPDATE POIs SET Code = '#P1004' WHERE Id = 4;
UPDATE POIs SET Code = '#P1005' WHERE Id = 5;
UPDATE POIs SET Code = '#P1006' WHERE Id = 6;
UPDATE POIs SET Code = '#P1007' WHERE Id = 7;
UPDATE POIs SET Code = '#P1008' WHERE Id = 8;
UPDATE POIs SET Code = '#P1009' WHERE Id = 9;
UPDATE POIs SET Code = '#P1010' WHERE Id = 10;

-- Các POI mới thêm sau này sẽ tự động từ #P1011...
UPDATE POIs SET Code = '#P1011' WHERE Id = 11 AND (Code IS NULL OR Code = '');
UPDATE POIs SET Code = '#P' + CAST(Id AS VARCHAR) WHERE Id > 11 AND (Code IS NULL OR Code = '');

-- =============================================
-- 2. CẬP NHẬT Users - #U1001 trở đi
-- Admin/User đầu tiên là #U1001, các user sau tăng dần
-- =============================================
-- Lấy danh sách users theo thứ tự Id
WITH UserRank AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) as rn
    FROM Users
)
UPDATE u SET Code = '#U' + CAST(1000 + ur.rn AS VARCHAR)
FROM Users u
JOIN UserRank ur ON u.Id = ur.Id;

-- =============================================
-- 3. CẬP NHẬT Audios - #A1001 trở đi
-- =============================================
WITH AudioRank AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) as rn
    FROM Audios
)
UPDATE a SET Code = '#A' + CAST(1000 + ar.rn AS VARCHAR)
FROM Audios a
JOIN AudioRank ar ON a.Id = ar.Id;

-- =============================================
-- 4. CẬP NHẬT Tours - TR-1, TR-2...
-- =============================================
WITH TourRank AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) as rn
    FROM Tours
)
UPDATE t SET Code = 'TR-' + CAST(tr.rn AS VARCHAR)
FROM Tours t
JOIN TourRank tr ON t.Id = tr.Id;

-- =============================================
-- 5. CẬP NHẬT Bookings - BK-1, BK-2...
-- =============================================
WITH BookingRank AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) as rn
    FROM Bookings
)
UPDATE b SET Code = 'BK-' + CAST(br.rn AS VARCHAR)
FROM Bookings b
JOIN BookingRank br ON b.Id = br.Id;

-- =============================================
-- 6. TẠO BẢNG LIÊN KẾT TOUR-POI (TourPOIs) NẾU CHƯA CÓ
-- =============================================
IF OBJECT_ID(N'[TourPOIs]') IS NULL
BEGIN
    CREATE TABLE [TourPOIs] (
        [Id] int NOT NULL IDENTITY,
        [TourId] int NOT NULL,
        [POIId] int NOT NULL,
        [OrderIndex] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_TourPOIs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TourPOIs_Tours] FOREIGN KEY ([TourId]) REFERENCES [Tours]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TourPOIs_POIs] FOREIGN KEY ([POIId]) REFERENCES [POIs]([Id]) ON DELETE CASCADE
    );
    
    -- Thêm dữ liệu mẫu: Tour 1 có POI 1,2,3; Tour 2 có POI 2,4; Tour 3 có POI 1,5
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (1, 1, 0);
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (1, 2, 1);
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (1, 3, 2);
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (2, 2, 0);
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (2, 4, 1);
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (3, 1, 0);
    INSERT INTO TourPOIs (TourId, POIId, OrderIndex) VALUES (3, 5, 1);
END

PRINT 'Đã cập nhật tất cả Code và tạo liên kết Tour-POI';
