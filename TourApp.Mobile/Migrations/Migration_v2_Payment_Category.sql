-- =============================================
-- MIGRATION v2: Thêm Payment và Category cho TourAppDB
-- Tạo: 28/04/2026
-- Mục đích: Hỗ trợ thanh toán QR giả lập và phân loại POI
-- =============================================

USE TourAppDB;
GO

PRINT '========================================';
PRINT 'BẮT ĐẦU MIGRATION v2';
PRINT '========================================';
GO

-- =============================================
-- PHẦN 1: ALTER BẢNG BOOKINGS (Thêm cột thanh toán)
-- =============================================
PRINT 'Step 1: Alter Bookings table...';

IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('Bookings') 
               AND name = 'PaymentMethod')
BEGIN
    ALTER TABLE Bookings ADD 
        PaymentMethod nvarchar(50) NULL,      -- QR, Momo, CreditCard...
        TransactionId nvarchar(200) NULL,   -- Mã giao dịch giả lập (SIM_QR_xxx)
        PaidAt datetime2 NULL,              -- Thời gian thanh toán thành công
        CancelledAt datetime2 NULL,       -- Thời gian hủy (nếu có)
        CancelReason nvarchar(500) NULL;    -- Lý do hủy
    
    PRINT '  ✓ Đã thêm cột: PaymentMethod, TransactionId, PaidAt, CancelledAt, CancelReason';
END
ELSE
BEGIN
    PRINT '  ℹ Bookings đã có các cột thanh toán';
END
GO

-- =============================================
-- PHẦN 2: TẠO BẢNG PAYMENTS (Lưu giao dịch thanh toán)
-- =============================================
PRINT 'Step 2: Create Payments table...';

IF OBJECT_ID('Payments') IS NULL
BEGIN
    CREATE TABLE Payments (
        Id int IDENTITY(1,1) PRIMARY KEY,
        BookingId int NOT NULL,
        UserId int NOT NULL,
        Amount decimal(18,2) NOT NULL,        -- Số tiền thanh toán
        PaymentMethod nvarchar(50) NOT NULL,  -- QR, Momo, CreditCard...
        TransactionId nvarchar(200) NULL,   -- Mã giao dịch từ cổng thanh toán
        Status nvarchar(50) DEFAULT 'Success', -- Trạng thái: Success, Failed, Pending
        PaidAt datetime2 NULL,              -- Thời gian thanh toán thành công
        QrCodeData nvarchar(500) NULL,      -- Dữ liệu QR đã quét (JSON)
        CreatedAt datetime2 DEFAULT GETDATE(),
        
        -- Foreign Keys
        CONSTRAINT FK_Payments_Bookings FOREIGN KEY (BookingId) 
            REFERENCES Bookings(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Payments_Users FOREIGN KEY (UserId) 
            REFERENCES Users(Id)
    );
    
    -- Indexes
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_BookingId' AND object_id = OBJECT_ID('Payments'))
        CREATE INDEX IX_Payments_BookingId ON Payments(BookingId);
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_UserId' AND object_id = OBJECT_ID('Payments'))
        CREATE INDEX IX_Payments_UserId ON Payments(UserId);
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_Status' AND object_id = OBJECT_ID('Payments'))
        CREATE INDEX IX_Payments_Status ON Payments(Status);
    
    PRINT '  ✓ Đã tạo bảng Payments với indexes';
END
ELSE
BEGIN
    PRINT '  ℹ Bảng Payments đã tồn tại';
END
GO

-- =============================================
-- PHẦN 3: TẠO BẢNG CATEGORIES (Danh mục POI)
-- =============================================
PRINT 'Step 3: Create Categories table...';

IF OBJECT_ID('Categories') IS NULL
BEGIN
    CREATE TABLE Categories (
        Id int IDENTITY(1,1) PRIMARY KEY,
        Name nvarchar(100) NOT NULL,          -- Tên danh mục: Nướng, Lẩu, Ốc...
        Icon nvarchar(100) NULL,              -- Icon class (MaterialIcons)
        Color nvarchar(20) NULL,              -- Màu hiển thị (#FF6B35...)
        DisplayOrder int DEFAULT 0,          -- Thứ tự hiển thị
        IsActive bit DEFAULT 1               -- Trạng thái hoạt động
    );
    
    -- Insert 4 danh mục mặc định (khớp với UI app)
    IF NOT EXISTS (SELECT 1 FROM Categories WHERE Name = N'Nướng')
    BEGIN
        INSERT INTO Categories (Name, Icon, Color, DisplayOrder) VALUES 
        (N'Nướng', 'Grill', '#FF6B35', 1),
        (N'Lẩu', 'HotPot', '#FF4500', 2),
        (N'Ốc', 'Shell', '#20B2AA', 3),
        (N'Ăn vặt', 'Snack', '#FFD700', 4);
        
        PRINT '  ✓ Đã tạo bảng Categories + 4 danh mục mặc định';
    END
    ELSE
    BEGIN
        PRINT '  ✓ Đã tạo bảng Categories (đã có dữ liệu)';
    END
END
ELSE
BEGIN
    PRINT '  ℹ Bảng Categories đã tồn tại';
END
GO

-- =============================================
-- PHẦN 4: TẠO BẢNG POICATEGORIES (Liên kết POI với Category)
-- =============================================
PRINT 'Step 4: Create POICategories table...';

IF OBJECT_ID('POICategories') IS NULL
BEGIN
    CREATE TABLE POICategories (
        Id int IDENTITY(1,1) PRIMARY KEY,
        POIId int NOT NULL,
        CategoryId int NOT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_POICategories_POIs FOREIGN KEY (POIId) 
            REFERENCES POIs(Id) ON DELETE CASCADE,
        CONSTRAINT FK_POICategories_Categories FOREIGN KEY (CategoryId) 
            REFERENCES Categories(Id) ON DELETE CASCADE,
        
        -- Unique constraint: 1 POI không trùng category
        CONSTRAINT UQ_POICategories UNIQUE (POIId, CategoryId)
    );
    
    -- Indexes
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_POICategories_CategoryId' AND object_id = OBJECT_ID('POICategories'))
        CREATE INDEX IX_POICategories_CategoryId ON POICategories(CategoryId);
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_POICategories_POIId' AND object_id = OBJECT_ID('POICategories'))
        CREATE INDEX IX_POICategories_POIId ON POICategories(POIId);
    
    PRINT '  ✓ Đã tạo bảng POICategories với indexes';
END
ELSE
BEGIN
    PRINT '  ℹ Bảng POICategories đã tồn tại';
END
GO

-- =============================================
-- PHẦN 5: GÁN CATEGORY CHO POI HIỆN CÓ (Tùy chọn)
-- =============================================
PRINT 'Step 5: Gán category mẫu cho POI hiện có (nếu cần)...';

-- Lấy ID của category 'Ốc'
DECLARE @OcCategoryId int = (SELECT Id FROM Categories WHERE Name = N'Ốc');
DECLARE @NuongCategoryId int = (SELECT Id FROM Categories WHERE Name = N'Nướng');

-- Gán POI có tên chứa 'Ốc' vào category 'Ốc'
IF @OcCategoryId IS NOT NULL
BEGIN
    INSERT INTO POICategories (POIId, CategoryId)
    SELECT p.Id, @OcCategoryId
    FROM POIs p
    WHERE p.Name LIKE N'%ốc%' 
      AND NOT EXISTS (SELECT 1 FROM POICategories pc WHERE pc.POIId = p.Id AND pc.CategoryId = @OcCategoryId);
    
    PRINT '  ✓ Đã gán category "Ốc" cho các POI liên quan';
END

-- Gán POI có tên chứa 'Nem' vào category 'Nướng'
IF @NuongCategoryId IS NOT NULL
BEGIN
    INSERT INTO POICategories (POIId, CategoryId)
    SELECT p.Id, @NuongCategoryId
    FROM POIs p
    WHERE p.Name LIKE N'%nướng%' OR p.Name LIKE N'%nem%'
      AND NOT EXISTS (SELECT 1 FROM POICategories pc WHERE pc.POIId = p.Id AND pc.CategoryId = @NuongCategoryId);
    
    PRINT '  ✓ Đã gán category "Nướng" cho các POI liên quan';
END
GO

-- =============================================
-- KẾT THÚC
-- =============================================
PRINT '';
PRINT '========================================';
PRINT '✅ MIGRATION v2 HOÀN TẤT!';
PRINT '========================================';
PRINT '';
PRINT 'Các thay đổi đã thực hiện:';
PRINT '  1. Bookings: +5 cột (PaymentMethod, TransactionId, PaidAt, CancelledAt, CancelReason)';
PRINT '  2. Tạo bảng: Payments';
PRINT '  3. Tạo bảng: Categories (+4 danh mục mặc định)';
PRINT '  4. Tạo bảng: POICategories';
PRINT '  5. Gán category mẫu cho POI hiện có';
PRINT '';
PRINT 'Lưu ý: Chạy script này nhiều lần vẫn an toàn (có kiểm tra IF NOT EXISTS)';
GO
