# Deploy TourApp API lên Render (Free)

## Bước 1: Push code lên GitHub

```bash
cd d:\C-\TourApp.Mobile

# Init git (nếu chưa có)
git init
git add .
git commit -m "Chuyen sang PostgreSQL + Dockerfile cho Render"

# Tạo repo trên GitHub rồi push
git remote add origin https://github.com/YOUR_USERNAME/TourApp.Mobile.git
git push -u origin main
```

## Bước 2: Tạo tài khoản Render

1. Vào https://render.com
2. Đăng ký bằng GitHub
3. Verify email

## Bước 3: Tạo PostgreSQL Database

1. Dashboard → **New +** → **PostgreSQL**
2. Name: `tourapp-postgres`
3. Region: **Singapore** (gần VN nhất)
4. Plan: **Free** (1GB)
5. Click **Create Database**
6. Đợi 1-2 phút cho DB ready
7. Copy **Internal Connection String** (dạng: `postgresql://tourapp:password@host:5432/tourappdb`)

## Bước 4: Tạo Web Service (API)

1. Dashboard → **New +** → **Web Service**
2. Chọn repo `TourApp.Mobile`
3. Cấu hình:
   - **Name**: `tourapp-api`
   - **Runtime**: **Docker**
   - **Root Directory**: `TourApp.API`
   - **Plan**: **Free**
4. Environment Variables:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `ConnectionStrings__DefaultConnection` = *(dán connection string từ Bước 3)*
5. Click **Create Web Service**

## Bước 5: Kiểm tra

- Đợi build hoàn tất (3-5 phút)
- URL API sẽ là: `https://tourapp-api.onrender.com`
- Test: mở browser vào `https://tourapp-api.onrender.com/swagger`

## Lưu ý quan trọng

| Vấn đề | Giải pháp |
|--------|-----------|
| **Sleep sau 15 phút** | Gọi API 1 lần mỗi 10 phút (dùng UptimeRobot miễn phí) |
| **Cold start** | Lần đầu gọi API sau sleep sẽ chậm 10-30 giây |
| **DB bị reset** | Free tier DB giữ dữ liệu, nhưng backup định kỳ |
| **Upload ảnh** | Ảnh lưu trong container sẽ mất khi deploy lại → cần S3 hoặc external storage sau này |

## Cập nhật Mobile App

Sửa URL API trong code:
```csharp
// TourApp.Mobile/Constants.cs hoặc nơi lưu base URL
public const string ApiBaseUrl = "https://tourapp-api.onrender.com";
```

Rebuild và phát hành APK mới.
