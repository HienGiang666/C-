-- =============================================
-- TÌM RECORD CÓ CỘT NULL TRONG CÁC BẢNG
-- =============================================

-- 1. Tìm Users có Role hoặc Code là NULL
SELECT 'Users' AS TableName, Id, Username, FullName, Role, Code
FROM Users
WHERE Role IS NULL OR Code IS NULL;

-- 2. Tìm POIs có Code là NULL
SELECT 'POIs' AS TableName, Id, Name, Code
FROM POIs
WHERE Code IS NULL;

-- 3. Tìm Tours có Code là NULL
SELECT 'Tours' AS TableName, Id, Name, Code
FROM Tours
WHERE Code IS NULL;

-- 4. Tìm Bookings có Code là NULL
SELECT 'Bookings' AS TableName, Id, TourId, UserId, Code
FROM Bookings
WHERE Code IS NULL;

-- =============================================
-- FIX: Cập nhật NULL thành giá trị mặc định
-- =============================================

-- Users: Role NULL -> 'Customer', Code NULL -> '#U' + Id
UPDATE Users SET Role = 'Customer' WHERE Role IS NULL;
UPDATE Users SET Code = '#U' + CAST(Id AS VARCHAR) WHERE Code IS NULL;

-- POIs: Code NULL -> '#P' + Id
UPDATE POIs SET Code = '#P' + CAST(Id AS VARCHAR) WHERE Code IS NULL;

-- Tours: Code NULL -> 'TR-' + Id
UPDATE Tours SET Code = 'TR-' + CAST(Id AS VARCHAR) WHERE Code IS NULL;

-- Bookings: Code NULL -> 'BK-' + Id
UPDATE Bookings SET Code = 'BK-' + CAST(Id AS VARCHAR) WHERE Code IS NULL;

PRINT 'Đã cập nhật tất cả NULL values thành giá trị mặc định';
