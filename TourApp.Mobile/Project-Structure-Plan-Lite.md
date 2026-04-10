# Project-Structure-Plan.md

## Mục tiêu của plan
Dọn project hiện tại sang cấu trúc gọn hơn, ít trùng hơn, mà:
- không làm vỡ build
- không làm vỡ luồng đang chạy
- ưu tiên đúng yêu cầu đồ án
- dễ mở rộng sau này

---

## Pha 0 - Không sửa code ngay
### Việc cần làm
- xác định file nào đang được dùng thật
- xác định file nào chỉ là duplicate / dead code
- xác định startup thật đang đi qua đâu
- xác định route thật trong `AppShell`

### Kết quả mong muốn
Có bảng:
- file giữ
- file bỏ / ngưng dùng
- file cần sửa startup
- file cần sửa route

---

## Pha 1 - Chốt 1 luồng auth duy nhất
### Việc cần làm
- giữ auth trong `Views/Auth/`
- bỏ hoặc ngưng dùng:
  - `Views/LoginPage.*`
  - `Views/RegisterPage.*`
- kiểm tra `App.xaml.cs` để app mở đúng:
  - chưa login → `Views/Auth/LoginPage`
  - đã login → `AppShell`

### Kết quả mong muốn
- không còn 2 luồng login/register song song
- startup rõ ràng
- không dùng `MainPage` nếu không cần

---

## Pha 2 - Dọn startup và shell
### Việc cần làm
- kiểm tra `App.xaml.cs`
- kiểm tra `AppShell.xaml` / `AppShell.xaml.cs`
- bỏ route trùng
- chỉ giữ route đúng cho:
  - Login
  - SignUp
  - ForgotPassword
  - ResetPassword
  - QRScanner
  - MapPage navigation

### Kết quả mong muốn
- app route rõ ràng
- điều hướng không vòng vèo
- không còn page trùng chức năng

---

## Pha 3 - Chốt MapPage là trung tâm
### Việc cần làm
- `HomePage` chỉ dẫn sang `MapPage`
- `POIPage` bấm item → `MapPage?poiId=...`
- `TourPage` bấm item → `MapPage?tourId=...`
- `MapPage` phải:
  - focus Vĩnh Khánh / Quận 4
  - load POI
  - hiện detail
  - audio / directions / close

### Kết quả mong muốn
- chỉ có một nơi xử lý POI detail thật: `MapPage`
- không duplicate logic detail ở nhiều page

---

## Pha 4 - Ổn định service layer
### Việc cần làm
- `ApiService` chỉ lo gọi API
- `AuthService` chỉ lo login/register/session/logout/forgot password demo
- `LanguageService` chỉ lo ngôn ngữ + notify
- `LocationService` chỉ lo vị trí
- `GeofenceService` chỉ lo trigger + audio
- `DatabaseService` chỉ giữ nếu thật sự cần

### Kết quả mong muốn
- service không chồng chéo trách nhiệm
- dễ debug hơn

---

## Pha 5 - Ngôn ngữ
### Việc cần làm
- dùng `LanguageService` ổn định
- tránh kiểu XAML localization dễ lỗi build nếu chưa chắc
- ưu tiên localization ở:
  - tab labels
  - profile
  - login/register
  - map alerts
  - các page chính
- sync audio/TTS theo ngôn ngữ đang chọn

### Kết quả mong muốn
- đổi ngôn ngữ không làm vỡ build
- hỗ trợ mở rộng lên 10 ngôn ngữ

---

## Pha 6 - Tính năng phụ
### Việc cần làm
- forgot password demo
- QR scanner
- favorites/history
- language polish
- local cache nếu cần

### Quy tắc
- làm sau khi luồng chính đã ổn
- nếu tính năng phụ làm vỡ build thì tạm rollback / hardcode

---

## Pha 7 - CMS polish
### Việc cần làm
- search + pagination
- sticky header/action bar nếu cần
- role admin-only
- không cho số âm
- rename User → Người dùng
- admin không bị khóa

---

## Scope demo đề xuất
### Phần bắt buộc phải chạy
1. Login
2. AppShell
3. Home / POI / Tour dẫn về Map
4. Map hiển thị được
5. POI hiện được
6. detail POI hoạt động
7. directions hoạt động
8. audio/TTS hoạt động
9. geofence trigger cơ bản
10. Profile đổi ngôn ngữ cơ bản
11. logout

### Phần có thể demo mức tối giản
1. Forgot password demo
2. QR scanner
3. Favorites
4. History
5. analytics

### Phần có thể nói là hướng phát triển
1. email reset thật
2. OTP thật
3. offline SQLite đầy đủ
4. analytics nâng cao
5. multi-language toàn bộ UI hoàn chỉnh