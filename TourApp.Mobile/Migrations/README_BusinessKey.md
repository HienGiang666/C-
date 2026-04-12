# Hướng dẫn triển khai Business Key Pattern

## Tóm tắt thay đổi

Pattern mới tách biệt:
- **Surrogate Key**: `Id` (INT, IDENTITY) - dùng cho FK, JOIN
- **Business Key**: `Code` (VARCHAR) - dùng hiển thị người dùng (#P1, TR-1, BK-1)

## Files đã cập nhật

### 1. API Models
| File | Thay đổi |
|------|-----------|
| `TourApp.API/Models/POI.cs` | Thêm `Code`, xóa `PublicCatalogNumber`, thêm `DisplayCode` |
| `TourApp.API/Models/Tour.cs` | Thêm `Code`, xóa `PublicCatalogNumber` |
| `TourApp.API/Models/Booking.cs` | Thêm `Code`, xóa `PublicCatalogNumber` |
| `TourApp.API/Models/User.cs` | Thêm `Code`, xóa `PublicCatalogNumber` |

### 2. CMS Models
| File | Thay đổi |
|------|-----------|
| `TourApp.CMS/Models/POI.cs` | Thêm `Code`, xóa `PublicCatalogNumber`, thêm `DisplayCode` |
| `TourApp.CMS/Models/Tour.cs` | Thêm `Code`, xóa `PublicCatalogNumber`, thêm `DisplayCode` |
| `TourApp.CMS/Models/Booking.cs` | Thêm `Code`, xóa `PublicCatalogNumber`, thêm `DisplayCode` |
| `TourApp.CMS/Models/User.cs` | Thêm `Code`, xóa `PublicCatalogNumber`, thêm `DisplayCode` |

### 3. Services
| File | Mô tả |
|------|-------|
| `TourApp.API/Services/BusinessKeyService.cs` | Service tự động sinh mã Code mới |

### 4. Controllers
| File | Thay đổi |
|------|-----------|
| `TourApp.API/Controllers/POIController.cs` | Inject `BusinessKeyService`, tự động sinh Code khi Create |
| `TourApp.CMS/Controllers/BookingController.cs` | Đổi sang dùng `UserCodeByUserId`, `TourCodeByTourId` |

### 5. Views
| File | Thay đổi |
|------|-----------|
| `TourApp.CMS/Views/POI/Index.cshtml` | Hiển thị `@item.DisplayCode` |
| `TourApp.CMS/Views/Tour/Index.cshtml` | Hiển thị `@tour.DisplayCode` |
| `TourApp.CMS/Views/Booking/Index.cshtml` | Hiển thị `@booking.DisplayCode` |
| `TourApp.CMS/Views/User/Index.cshtml` | Hiển thị `@user.DisplayCode` |
| `TourApp.CMS/Helpers/DisplayIdHelper.cs` | Cập nhật dùng `DisplayCode` |

### 6. Khởi tạo
| File | Thay đổi |
|------|-----------|
| `TourApp.API/Program.cs` | Đăng ký `BusinessKeyService`, dùng `EnsureBusinessKeyCodes` |
| `TourApp.API/Data/DbSeeder.cs` | Cập nhật seed data dùng `Code`, thêm `TryAddCodeColumn` |

## Script SQL

### 1. AddBusinessKeyCodeColumns.sql
Thêm cột `Code` vào database, khởi tạo giá trị mặc định, tạo UNIQUE INDEX.

### 2. DataCleanupAndReassignIds.sql
Cleanup data và reset IDENTITY seed.

## Cách chạy

### Bước 1: Chạy Script SQL
```sql
-- Trong SSMS hoặc VS SQL Query
TourApp.Mobile/Migrations/AddBusinessKeyCodeColumns.sql
TourApp.Mobile/Migrations/DataCleanupAndReassignIds.sql
```

### Bước 2: Build và chạy API
```bash
cd TourApp.Mobile/TourApp.API
dotnet build
dotnet run
```

### Bước 3: Build và chạy CMS
```bash
cd TourApp.Mobile/TourApp.CMS
dotnet build
dotnet run
```

## Kiểm tra

### API Test
```bash
# Tạo POI mới - Code sẽ tự động sinh
curl -X POST http://localhost:5000/api/poi \
  -H "Content-Type: application/json" \
  -d '{"name":"Test POI","description":"Test"}'

# Kiểm tra Code đã được gán
```

### CMS View
Mở trang:
- `/POI` - POI hiển thị #P1001, #P1002...
- `/Tour` - Tour hiển thị TR-1001, TR-1002...
- `/Booking` - Booking hiển thị BK-1, BK-2...
- `/User` - User hiển thị #U1001, #U1002...

## Lưu ý quan trọng

1. **Backward Compatibility**: Các bảng vẫn giữ `Id` làm PK, FK không bị ảnh hưởng
2. **Code là VARCHAR**: Cho phép linh hoạt format (có thể dùng #P1 hoặc POI-001)
3. **DisplayCode là computed**: Fallback về format mặc định nếu Code null
4. **Auto-generate**: Khi Create mới, nếu không truyền Code thì API tự sinh
