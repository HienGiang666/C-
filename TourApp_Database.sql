
-- =============================================
-- TOURAPP DATABASE - PHỐ ẨM THỰC TP.HCM
-- =============================================

CREATE DATABASE TourAppDB;
GO
USE TourAppDB;
GO

-- =============================================
-- 1. BẢNG TOUR
-- =============================================
CREATE TABLE Tour (
    TourId      INT IDENTITY(1,1) PRIMARY KEY,
    TourName    NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    ImageUrl    NVARCHAR(500),
    IsActive    BIT DEFAULT 1,
    CreatedAt   DATETIME DEFAULT GETDATE()
);

-- =============================================
-- 2. BẢNG POI
-- =============================================
CREATE TABLE POI (
    PoiId       INT IDENTITY(1,1) PRIMARY KEY,
    PoiName     NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Address     NVARCHAR(300),
    Latitude    FLOAT NOT NULL,
    Longitude   FLOAT NOT NULL,
    Radius      FLOAT DEFAULT 80,        -- bán kính geofence (mét)
    Priority    INT DEFAULT 1,
    ImageUrl    NVARCHAR(500),
    OpenTime    NVARCHAR(100),
    IsActive    BIT DEFAULT 1,
    CreatedAt   DATETIME DEFAULT GETDATE()
);

-- =============================================
-- 3. BẢNG TOUR_POI
-- =============================================
CREATE TABLE TourPOI (
    TourPOIId   INT IDENTITY(1,1) PRIMARY KEY,
    TourId      INT NOT NULL FOREIGN KEY REFERENCES Tour(TourId),
    PoiId       INT NOT NULL FOREIGN KEY REFERENCES POI(PoiId),
    OrderIndex  INT DEFAULT 0
);

-- =============================================
-- 4. BẢNG AUDIO
-- =============================================
CREATE TABLE Audio (
    AudioId     INT IDENTITY(1,1) PRIMARY KEY,
    PoiId       INT NOT NULL FOREIGN KEY REFERENCES POI(PoiId),
    Language    NVARCHAR(10) DEFAULT 'vi',
    AudioPath   NVARCHAR(500),
    ScriptText  NVARCHAR(MAX),
    Duration    INT,
    IsActive    BIT DEFAULT 1
);

-- =============================================
-- 5. BẢNG LOG VỊ TRÍ
-- =============================================
CREATE TABLE UserLocationLog (
    LogId       INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId    NVARCHAR(100),
    Latitude    FLOAT NOT NULL,
    Longitude   FLOAT NOT NULL,
    Accuracy    FLOAT,
    LoggedAt    DATETIME DEFAULT GETDATE()
);

-- =============================================
-- 6. BẢNG LOG THUYẾT MINH
-- =============================================
CREATE TABLE NarrationLog (
    LogId       INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId    NVARCHAR(100),
    PoiId       INT FOREIGN KEY REFERENCES POI(PoiId),
    AudioId     INT FOREIGN KEY REFERENCES Audio(AudioId),
    PlayedAt    DATETIME DEFAULT GETDATE(),
    TriggerType NVARCHAR(50)  -- 'geofence', 'manual', 'qrcode'
);

-- =============================================
-- DỮ LIỆU TOUR
-- =============================================
INSERT INTO Tour (TourName, Description) VALUES
(N'Phố Ẩm Thực Vĩnh Khánh - Quận 4', N'Khám phá thiên đường ốc và hải sản dọc đường Vĩnh Khánh'),
(N'Ăn Vặt Tôn Đản - Quận 4', N'Thiên đường ăn vặt sinh viên đường Tôn Đản và Xóm Chiếu');

-- =============================================
-- DỮ LIỆU POI - QUÁN ĂN THỰC TẾ QUẬN 4
-- =============================================
INSERT INTO POI (PoiName, Description, Address, Latitude, Longitude, Radius, Priority, OpenTime) VALUES
(
    N'Ốc Oanh',
    N'Quán ốc nổi tiếng nhất nhì khu phố Vĩnh Khánh với các món ốc móng tay mít hải sản nướng mỡ hành thơm lừng.',
    N'534 Vĩnh Khánh, Phường 8, Quận 4',
    10.7596, 106.7018, 50, 1, N'15:00 - 23:00'
),
(
    N'Lẩu Bò Khu Vực',
    N'Điểm dừng chân tuyệt vời cho món lẩu bò đậm đà, nóng hổi nhâm nhi trong buổi tối Sài Gòn.',
    N'123 Vĩnh Khánh, Phường 8, Quận 4',
    10.7589, 106.7005, 50, 1, N'16:00 - 24:00'
),
(
    N'Phá Lấu Bò Cô Oanh',
    N'Phá lấu nước cốt dừa thơm béo, chấm bánh mì hoặc an kèm mì gói cực kỳ ngon miệng, luôn tấp nập khách.',
    N'200/20 Xóm Chiếu, Phường 14, Quận 4',
    10.7601, 106.7062, 40, 1, N'14:00 - 22:00'
),
(
    N'Bánh Đúc Nóng Q4',
    N'Bánh đúc nóng hổi dẻo thơm với thịt băm mộc nhĩ, chan nước mắm chua ngọt, ăn một lần là ghiền.',
    N'Chợ Xóm Chiếu, Phường 14, Quận 4',
    10.7595, 106.7075, 40, 2, N'15:00 - 21:00'
),
(
    N'Mì Cay Sasin Vĩnh Khánh',
    N'Chuỗi mì cay 7 cấp độ phiên bản Vĩnh Khánh, Q4, phù hợp với giới trẻ.',
    N'Hoàng Diệu, Phường 9, Quận 4',
    10.7615, 106.7022, 60, 2, N'09:00 - 22:30'
);

-- =============================================
-- DỮ LIỆU TOUR - POI
-- =============================================
-- Tour 1: Phố Ẩm Thực Vĩnh Khánh
INSERT INTO TourPOI (TourId, PoiId, OrderIndex) VALUES
(1, 1, 1), -- Ốc Oanh
(1, 2, 2), -- Lẩu Bò Khu Vực
(1, 5, 3); -- Mì Cay

-- Tour 2: Ăn Vặt Xóm Chiếu
INSERT INTO TourPOI (TourId, PoiId, OrderIndex) VALUES
(2, 3, 1), -- Phá Lấu Cô Oanh
(2, 4, 2); -- Bánh Đúc Nóng

-- =============================================
-- DỮ LIỆU AUDIO (Script TTS)
-- =============================================
INSERT INTO Audio (PoiId, Language, ScriptText) VALUES
(1, 'vi', N'Chào mừng bạn đến với Ốc Oanh, quán ốc sầm uất nhất Vĩnh Khánh. Các món bạn nhất định phải thử bao gồm hàu nướng phô mai, càng ghẹ rang muối và nghêu hấp xả.'),
(2, 'vi', N'Bạn đang ở gần Lẩu Bò Khu Vực. Hương vị lẩu thanh ngọt từ xương và thịt bò mềm cùng rau tươi sẽ nạp lại năng lượng cho bạn sau một ngày dài.'),
(3, 'vi', N'Chào mừng bạn đến Phá Lấu Bò Cô Oanh trong hẻm Xóm Chiếu. Hương vị nước cốt dừa beo béo nơi đây chắc chắn sẽ làm bạn no bụng.'),
(4, 'vi', N'Bạn đang nếm thử món Bánh đúc nóng đặc sản của Quận 4. Lớp bánh mềm mịn tan trong miệng cùng nhân thịt băm mộc nhĩ sẽ khiến bạn không thể quên.'),
(5, 'vi', N'Chào mừng bạn qua tiệm Mì Cay Sasin. Nổi bật với mì 7 cấp độ, đây là lựa chọn hợp lý để xách túi và thử thách vị giác của bản thân.');

ALTER LOGIN sa ENABLE;
ALTER LOGIN sa WITH PASSWORD = '123456';
