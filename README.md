

# PRD — Phố Ẩm Thực Vĩnh Khánh (TourApp)

> Ứng dụng hướng dẫn tham quan ẩm thực tự động: GPS tracking, Geofence, thuyết minh âm thanh tự động, bản đồ tương tác — khám phá Phố Vĩnh Khánh, Quận 4, TP.HCM.

**Tech Stack:** .NET MAUI (Android/iOS) · ASP.NET Core Web API · ASP.NET MVC (CMS) · SQL Server · Goong.io Maps · Text-to-Speech · QR Code

---

## Mục lục

1. [Giới thiệu dự án](#1-giới-thiệu-dự-án)
2. [Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
3. [Database Schema](#3-database-schema---tourappdb)
4. [Actor #1 — Admin](#4-actor-1--admin-quản-trị-viên)
5. [Actor #2 — Chủ Quán Ăn](#5-actor-2--chủ-quán-ăn-restaurant-owner)
6. [Actor #3 — Khách Tham Quan](#6-actor-3--khách-tham-quan-customer)
7. [Diagrams](#7-diagrams)
8. [Technology Stack](#8-technology-stack)

---

## 1. Giới thiệu dự án

### 1.1. Bối cảnh

Phố Vĩnh Khánh (Quận 4, TP.HCM) là tuyến phố ẩm thực nổi tiếng với hơn 100 quán ăn đa dạng. Khách du lịch thường không biết quán nào đáng thử, không có thông tin thuyết minh, và dễ bị lạc trong khu vực đông đúc. Ứng dụng **Tour Vĩnh Khánh** giải quyết bài toán này bằng công nghệ GPS + Geofence + Thuyết minh âm thanh tự động.

### 1.2. Mục tiêu

#### PoC (Proof of Concept)
- GPS tracking theo tuyến thực (foreground + background)
- Geofence kích hoạt điểm thuyết minh tự động
- Thuyết minh tự động qua TTS hoặc file audio thu sẵn
- Quản lý dữ liệu POI (mô tả, ảnh, link bản đồ, audio)
- Map View hiển thị vị trí người dùng & các POI

#### MVP (Minimum Viable Product)
- Hệ thống quản trị nội dung (CMS) cho Admin & Chủ quán
- Phân tích dữ liệu: heatmap, top POI, lịch sử nghe
- QR Code kích hoạt nội dung (tại trạm xe buýt, quán...)
- Quản lý Tour ẩm thực & Bookings
- Phân quyền đa vai trò (Admin, RestaurantOwner, Customer)

### 1.3. Phạm vi Actors

| Actor | Nền tảng | Mô tả |
|-------|----------|-------|
| **Admin** (Quản trị viên) | CMS Web | Toàn quyền quản lý hệ thống: POI, User, Audio, Tour, Booking, duyệt POI, xem thống kê |
| **Restaurant Owner** (Chủ Quán) | CMS Web | Đăng ký quán ăn, cập nhật thông tin & audio, xem thống kê quán mình |
| **Customer** (Khách) | Mobile App | Sử dụng app: xem bản đồ, nghe thuyết minh tự động, tìm kiếm, yêu thích, đặt tour |

---

## 2. Kiến trúc hệ thống

### 2.1. Tổng quan 3 tầng

| Thành phần | Công nghệ | Mô tả |
|-----------|-----------|-------|
| **TourApp.Mobile** | .NET MAUI (Android/iOS) | App di động: GPS tracking, Goong Maps, Geofence, Audio, QR Code |
| **TourApp.CMS** | ASP.NET Core MVC (Razor Views) | Trang quản trị web cho Admin & Owner |
| **TourApp.API** | ASP.NET Core Web API | API RESTful, xử lý nghiệp vụ, trả JSON |
| **Database** | SQL Server + EF Core | TourAppDB, 9 tables, indexes, foreign keys |
| **Map Service** | Goong.io Maps | Map tiles, geocoding, hiển thị POI trên bản đồ |
| **Audio Engine** | TTS / File MP3 | Thuyết minh tự động, đa ngôn ngữ |

### 2.2. Luồng dữ liệu tổng quan

```
📱 Mobile App  ──(REST JSON)──▶  ASP.NET Core API  ──(EF Core)──▶  SQL Server DB
🌐 CMS Web     ──(HTTP/API)───▶  ASP.NET Core API  ──(EF Core)──▶  SQL Server DB
```

### 2.3. Luồng hoạt động chính

1. **Mobile App** gọi API lấy danh sách POIs → hiển thị bản đồ Goong.
2. App chạy **GPS Background Service** cập nhật vị trí liên tục → gửi log lên API (`UserLocationLogs`).
3. **Geofence Engine** tính khoảng cách user–POI, khi < `Radius` → kích hoạt **Narration Engine** phát audio/TTS.
4. Ghi log vào `NarrationLogs` (chống phát trùng lặp).
5. **CMS Web** (Admin/Owner) quản lý POI, Audio, Tour, User, duyệt POI, xem thống kê.

---

## 3. Database Schema — TourAppDB

### 3.1. Danh sách Tables

| Table | Mục đích | Quan hệ |
|-------|----------|---------|
| `Users` | Tài khoản: Admin, RestaurantOwner, Customer | PK → POIs, FavoritePOIs, Bookings |
| `POIs` | Điểm ẩm thực (Point of Interest), tọa độ, bán kính geofence | FK Users → Audios, FavoritePOIs, NarrationLogs, TourPOIs |
| `Tours` | Tour ẩm thực (lịch trình tham quan) | → TourPOIs, Bookings |
| `TourPOIs` | Bảng trung gian Tour ↔ POI (thứ tự dừng) | FK Tours, FK POIs |
| `Audios` | File audio thuyết minh, script TTS, đa ngôn ngữ | FK POIs |
| `FavoritePOIs` | Danh sách POI yêu thích của user | FK Users, FK POIs |
| `UserLocationLogs` | Log vị trí GPS (ẩn danh theo DeviceId) → Heatmap | Standalone |
| `NarrationLogs` | Log đã phát audio tại POI nào → chống trùng, thống kê | FK POIs |
| `Bookings` | Đặt chỗ tour (số người, giá, trạng thái) | FK Tours, FK Users |

### 3.2. CREATE TABLE Scripts

#### Users

```sql
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
```

#### POIs (Points of Interest)

```sql
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
```

#### Tours

```sql
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
```

#### TourPOIs (Bảng trung gian Tour ↔ POI)

```sql
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
```

#### Audios

```sql
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
```

#### FavoritePOIs

```sql
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
```

#### UserLocationLogs

```sql
CREATE TABLE [UserLocationLogs] (
    [Id] int NOT NULL IDENTITY(1,1),
    [DeviceId] nvarchar(200) NOT NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    [Timestamp] datetime2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_UserLocationLogs] PRIMARY KEY ([Id])
);
```

#### NarrationLogs

```sql
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
```

#### Bookings

```sql
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
```

---

## 4. Actor #1 — Admin (Quản trị viên)

**Nền tảng:** CMS Web (ASP.NET MVC). Admin có toàn quyền quản lý hệ thống.

### A1. Đăng nhập / Xác thực
- Đăng nhập bằng Username/Password (hash SHA-256)
- Session-based Auth via Cookie
- Bộ lọc toàn cục `AuthFilter` bắt buộc đăng nhập
- Đổi mật khẩu, quản lý phiên

### A2. Quản lý POI (CRUD)
- Thêm / Sửa / Xóa điểm ẩm thực
- Cấu hình tọa độ (Lat/Lng), bán kính Geofence (`Radius`)
- Thiết lập mức ưu tiên (`Priority`) — quán nào đọc trước khi trùng bán kính
- Upload ảnh minh họa
- Bật/Tắt trạng thái hoạt động (`IsActive`)

### A3. Duyệt POI (Approval)
- Xem danh sách POI chờ duyệt (`ApprovalStatus = 'Pending'`)
- Duyệt → `Approved` (hiển thị trên Mobile App)
- Từ chối → `Rejected`
- Ghi log hành động duyệt

### A4. Quản lý Audio / TTS
- Xem, thêm, sửa, xóa file audio thuyết minh
- Text-to-Speech: nhập Script → tạo audio tự động
- Chọn giọng đọc (Nam/Nữ), ngôn ngữ (Việt/Anh...)
- Nghe thử (Preview) trực tiếp trên CMS
- Tính toán thời lượng audio

### A5. Quản lý User
- CRUD tài khoản (Admin, Owner, Customer)
- Phân quyền theo Role
- Khóa/Mở khóa tài khoản (`IsActive`)
- Reset mật khẩu

### A6. Quản lý Tour
- Tạo Tour ẩm thực (tên, mô tả, giá, thời lượng)
- Gán POI vào Tour theo thứ tự (`TourPOIs.OrderIndex`)
- Quản lý Bookings (xác nhận, hủy)

### A7. Thống kê & Analytics
- Xem **Heatmap** vị trí tập trung khách (từ `UserLocationLogs`)
- **Top POI** được nghe nhiều nhất (từ `NarrationLogs`)
- Thời lượng trung bình nghe tại 1 POI
- Lịch sử di chuyển (ẩn danh)
- Tổng quan Bookings & doanh thu

### A8. Cài đặt Hệ thống
- Cấu hình kết nối API
- Quản lý Goong Maps API Key
- Xem Activity Log hệ thống
- Backup/Restore database

---

## 5. Actor #2 — Chủ Quán Ăn (Restaurant Owner)

**Nền tảng:** CMS Web. Chủ quán ăn tự quản lý thông tin quán của mình, đăng ký POI mới cần Admin duyệt.

### O1. Đăng ký / Đăng nhập
- Đăng ký tài khoản RestaurantOwner qua API
- Đăng nhập vào CMS (role-based access)
- Xem/Cập nhật hồ sơ cá nhân

### O2. Quản lý Quán ăn (My POI)
- Cập nhật thông tin quán: Tên, Mô tả, Giờ mở cửa
- Upload ảnh minh họa
- Cắm mốc vị trí (Lat/Lng) trên bản đồ
- Đăng ký POI mới → trạng thái `Pending` (chờ Admin duyệt)

### O3. Nội dung Thuyết minh
- Viết kịch bản thuyết minh (`ScriptText`)
- Upload file MP3 tự thu âm
- Xem trước / nghe thử audio

### O4. Thống kê Quán mình
- Số lượt khách nghe audio đi ngang (`NarrationLogs`)
- Điểm đánh giá trung bình
- Số lượt yêu thích (`FavoritePOIs`)

### Luồng đăng ký Owner

```
Chủ quán đăng ký → Tạo User (RestaurantOwner) → Đăng POI (Pending) → Admin duyệt → Approved → Hiện trên App
```

---

## 6. Actor #3 — Khách Tham Quan (Customer)

**Nền tảng:** .NET MAUI Mobile App (Android/iOS). Trải nghiệm tham quan ẩm thực hoàn toàn trên điện thoại.

### C1. Đăng ký / Đăng nhập
- Đăng ký tài khoản Customer
- Đăng nhập bằng Username/Password
- Hỗ trợ Guest mode (xem bản đồ, không lưu yêu thích)

### C2. Bản đồ tương tác (Map View)
- Hiển thị bản đồ Goong.io với vị trí thực user (chấm xanh)
- Hiển thị tất cả POI đã duyệt (marker)
- Highlight POI đang ở gần bán kính
- Nhấn marker → xem chi tiết quán ăn

### C3. Thuyết minh tự động (Narration)
- **Auto:** Tự động phát audio khi lọt vào bán kính POI (Geofence trigger)
- **Manual:** Quét QR Code tại quán để nghe ngay
- Chống phát trùng (debounce + cooldown từ `NarrationLogs`)
- Hàng đợi audio (queue) — không phát chèn

### C4. Tìm kiếm & Khám phá
- Tìm quán ăn theo tên, khu vực
- Bộ lọc: Rating, khoảng cách, giờ mở cửa
- Xem danh sách POI dạng list + hình ảnh
- Xem chi tiết: mô tả, ảnh, giờ mở, rating

### C5. Yêu thích
- Thêm/Xóa POI khỏi danh sách yêu thích
- Xem danh sách đã yêu thích trên Profile

### C6. Quét QR Code
- Quét mã QR dán tại quán ăn/trạm xe buýt
- Nhận diện mã Code POI (VD: `#P1001`)
- Hiển thị thông tin + phát audio ngay lập tức
- Không cần GPS (backup trigger)

### C7. Tour ẩm thực
- Xem danh sách Tour có sẵn
- Xem chi tiết Tour (lịch trình, giá, các quán)
- Đặt Tour (Booking) — chọn ngày, số người
- Xem lịch sử Booking trên Profile

### C8. Hồ sơ Cá nhân
- Xem/Sửa thông tin cá nhân
- Danh sách yêu thích
- Lịch sử Booking
- Đăng xuất

### Luồng Geofence → Audio (Core Flow)

```
User di chuyển → GPS cập nhật → Geofence Engine tính d(user, POI) → d ≤ Radius? → Phát Audio/TTS → Ghi NarrationLog
```

---

## 7. Technology Stack

| Thành phần | Công nghệ | Phiên bản / Ghi chú |
|-----------|-----------|---------------------|
| **Mobile App** | .NET MAUI | C#, XAML, Android/iOS |
| Mobile — Bản đồ | Goong Maps SDK | goong.io API |
| Mobile — GPS | Microsoft.Maui.Essentials | Geolocation, FusedLocationProvider |
| Mobile — QR Code | ZXing.Net.MAUI | Camera-based scanning |
| **CMS Web** | ASP.NET Core MVC | Razor Views, Bootstrap 5 |
| CMS — TTS Preview | Web Speech API | Browser-based preview |
| CMS — Map picking | Goong Maps JS | Chọn vị trí quán trên bản đồ |
| **Backend API** | ASP.NET Core Web API | RESTful, JSON |
| API — ORM | Entity Framework Core | Code-first migrations |
| API — Auth | SHA-256 + Session/Cookie | Password hashing |
| **Database** | SQL Server | LocalDB / Express |
| DB — Management | SQL Server Management Studio | SSMS |
| **Version Control** | Git + GitHub | Branching, CI |

---

*Đồ án Giữa kỳ • Lập trình C# • 2026*
*Phố Ẩm Thực Vĩnh Khánh — Product Requirements Document*
