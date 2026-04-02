# Product Requirements Document (PRD): TourApp
**Dự án:** Hệ thống Quản lý và Dẫn Tour Du lịch Tự động  
**Nền tảng:** Mobile (Android/iOS) & Web Application  

---

## 1. Tổng quan dự án (Executive Summary)
TourApp là một giải pháp công nghệ du lịch thông minh, giúp tự động hóa trải nghiệm của du khách. Thay vì cần hướng dẫn viên truyền thống, hệ thống sử dụng định vị GPS, công nghệ Geofence (khoanh vùng địa lý) và Text-to-Speech (TTS) để tự động nhận diện khi du khách đến một điểm tham quan (POI - Point of Interest) và phát âm thanh thuyết minh tương ứng.

## 2. Chân dung người dùng (Target Audience)
* **Khách du lịch (End-User):** Sử dụng Mobile App để xem bản đồ, xác định vị trí và nghe thuyết minh tự động khi đến các địa danh.
* **Ban Quản lý / Admin:** Sử dụng Web CMS để quản lý danh sách địa điểm, tọa độ, nội dung thuyết minh và tạo mã QR truy cập nhanh.

---

## 3. Phạm vi tính năng & Phân công (Scope & Roles)
Dự án được chia độc lập thành 2 module làm việc song song để tối ưu hiệu suất và tránh xung đột mã nguồn (conflict code).

### Module 1: Mobile App (Thành viên 1)
* **Map View & GPS (feature/map-gps):** Tích hợp bản đồ Mapsui và theo dõi vị trí thực của người dùng theo thời gian thực.
* **Geofence:** Thuật toán nhận diện khoảng cách, kích hoạt sự kiện khi người dùng bước vào bán kính của POI.
* **TTS & Audio (feature/tts-audio):** Chuyển đổi văn bản mô tả thành giọng nói và phát audio tự động.

### Module 2: Backend & Web CMS (Thành viên 2)
* **REST API & Database (feature/api):** Xây dựng CSDL SQL Server và cung cấp API (CRUD) kết nối dữ liệu POI cho Mobile App.
* **Web CMS (feature/cms):** Giao diện quản trị trực quan cho phép Admin Thêm, Sửa, Xóa thông tin địa điểm.
* **QR Code Generator:** Tích hợp tính năng tự động sinh mã QR cho từng địa điểm ngay trên hệ thống CMS.

---

## 4. Lộ trình phát triển (Roadmap - 4 Giai đoạn)
Dự án áp dụng mô hình phát triển linh hoạt (Agile), ưu tiên ra mắt các tính năng cốt lõi trước:

### Phase 1: Nền tảng (Foundation)
* **Trọng tâm Mobile:** Tích hợp Map View & định vị GPS.
* **Trọng tâm Backend:** Xây dựng REST API & kết nối Database.
* **Mục tiêu đầu ra:** Mobile hiển thị được bản đồ và lấy được tọa độ điểm từ API.

### Phase 2: Xử lý lõi (Core Logic)
* **Trọng tâm Mobile:** Cài đặt thuật toán Geofence.
* **Trọng tâm Backend:** Hoàn thiện giao diện Web CMS quản lý POI.
* **Mục tiêu đầu ra:** Luồng dữ liệu khép kín: CMS nhập liệu -> Database -> API -> Mobile.

### Phase 3: Trải nghiệm (Enhancement)
* **Trọng tâm Mobile:** Tích hợp Text-to-Speech (TTS) & Audio.
* **Trọng tâm Backend:** Phát triển tính năng tạo QR Code trên CMS.
* **Mục tiêu đầu ra:** Hệ thống tự động phát âm thanh khi đến nơi; CMS xuất được mã QR.

### Phase 4: Nghiệm thu (Finalization)
* **Trọng tâm Mobile:** Hỗ trợ Test & Fix bug.
* **Trọng tâm Backend:** Hỗ trợ Test & Fix bug.
* **Mục tiêu đầu ra:** Merge Code, kiểm thử toàn bộ hệ thống và bàn giao sản phẩm.

---

## 5. Kiến trúc Hệ thống (System Architecture)
Hệ thống được thiết kế theo mô hình Client-Server, giao tiếp hoàn toàn qua RESTful API để đảm bảo tính độc lập và dễ mở rộng:
* **Client 1 (Mobile App):** Xây dựng bằng .NET MAUI. Gửi HTTP Request lên API để lấy danh sách tọa độ và nội dung thuyết minh.
* **Client 2 (Web CMS):** Xây dựng bằng ASP.NET Core MVC. Giao diện để Admin tương tác (Thêm, Sửa, Xóa).
* **Server (Backend API):** ASP.NET Core Web API. Đóng vai trò cầu nối, xử lý logic và bảo mật (CORS).
* **Database:** SQL Server, giao tiếp với API thông qua Entity Framework Core (Code-First).

## 6. Sơ đồ Cơ sở dữ liệu (Database Schema)
Dự án sử dụng cấu trúc cơ sở dữ liệu tinh gọn, tập trung vào bảng cốt lõi là POIs (Points of Interest - Điểm đến):
* `Id` (int, Primary Key): Mã định danh tự tăng.
* `Name` (string): Tên địa điểm (VD: Chợ Bến Thành).
* `Description` (string): Nội dung thuyết minh sẽ được chuyển thành giọng nói (TTS).
* `Latitude` (double): Vĩ độ GPS.
* `Longitude` (double): Kinh độ GPS.

## 7. Luồng người dùng (User Flow)
Hệ thống phục vụ 2 luồng trải nghiệm song song:

**Luồng của Admin (Ban quản lý):**
1. Đăng nhập Web CMS.
2. Mở trang Quản lý Địa điểm.
3. Nhập thông tin, tọa độ và mô tả cho điểm đến mới -> Lưu vào Database.
4. (Tùy chọn) Nhấn nút tạo và in mã QR để dán tại điểm tham quan.

**Luồng của Khách du lịch:**
1. Mở TourApp trên điện thoại, cấp quyền truy cập Vị trí (Location).
2. App tự động tải danh sách các điểm đến từ API và vẽ Marker lên bản đồ Mapsui.
3. Du khách di chuyển. Trình theo dõi GPS liên tục cập nhật vị trí.
4. Khi du khách bước vào bán kính 20 mét của một POI (Geofence trigger).
5. App tự động lấy trường `Description` và phát âm thanh qua loa (TTS).

## 8. Tiêu chí Thành công (Success Metrics)
Dự án được đánh giá là hoàn thiện khi đáp ứng các chỉ số sau:
* **Độ trễ API:** Các thao tác tải danh sách điểm đến trên App/Web phản hồi dưới 1 giây.
* **Độ chính xác GPS/Geofence:** Sai số nhận diện khi bước vào vùng POI nằm trong mức cho phép (bán kính < 20 mét).
* **Trải nghiệm người dùng:** Âm thanh TTS phát rõ ràng, đúng thời điểm, không bị giật lag khi chuyển đổi giữa các điểm đến.
* **Bảo mật cơ bản:** Chỉ định đúng các nguồn (Origins) được phép gọi API thông qua cấu hình CORS.

---

## 9. Quy chuẩn Kỹ thuật & Quản lý Mã nguồn (GitHub Workflow)
Để đảm bảo Phase 4 (Merge Code) diễn ra suôn sẻ, team tuân thủ nghiêm ngặt quy trình làm việc trên GitHub Desktop:
1. Clone repository về máy cá nhân.
2. Luôn bắt đầu từ nhánh gốc: Nhấn `Current branch: main`.
3. Tạo nhánh làm việc độc lập: Nhấn `New branch` (Ví dụ: `feature/api`, `feature/map-gps`).
4. Nhấn `Create branch` và bắt đầu lập trình.
5. Chỉ thực hiện Pull Request (PR) để gộp code khi tính năng đã hoàn thiện nội bộ.

---


