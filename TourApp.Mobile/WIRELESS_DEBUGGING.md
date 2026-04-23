# Hướng Dẫn Chạy App Không Cần Cắm Dây (Wireless Debugging)

## ⚡ QUICK START (Nhanh nhất)

```bash
# 1. Chạy script để tự động lấy IP, mở firewall và khởi động API
.\scripts\start-api-server.bat

# 2. Script sẽ hiển thị IP. Nhập IP đó vào app (nút ⚙️ trên Map)
#    VD: http://192.168.1.10:5254

# 3. Deploy app từ Visual Studio (chọn device wireless)
```

---

## Yêu Cầu
- Điện thoại Android và máy tính **cùng kết nối chung WiFi**
- Điện thoại chạy Android 11+ (hoặc Android 10 với workaround)
- Visual Studio 2022 với .NET MAUI workload

---

## 🔴 Cách Fix Lỗi "Không thể kết nối máy chủ"

Nếu app báo không kết nối được API, kiểm tra:

1. **API đã chạy chưa?** Chạy lệnh trên máy tính:
   ```powershell
   cd TourApp.API
   dotnet run --urls "http://0.0.0.0:5254"
   ```

2. **IP đúng chưa?** Trên máy tính, mở Command Prompt:
   ```
   ipconfig
   ```
   Tìm dòng `IPv4 Address` của WiFi adapter (VD: `192.168.1.10`)

3. **Nhập IP vào app:** Trong app, nhấn nút **⚙️ (Settings)** trên Map, nhập:
   ```
   http://192.168.1.10:5254
   ```
   (Thay `192.168.1.10` bằng IP thực tế của máy tính)

4. **Firewall chặn?** Mở PowerShell Admin:
   ```powershell
   New-NetFirewallRule -DisplayName "TourApp API 5254" -Direction Inbound -Protocol TCP -LocalPort 5254 -Action Allow
   ```

---

## Bước 1: Bật Wireless Debugging trên Điện Thoại

### Trên Android 11+ (Khuyến nghị)
1. Mở **Settings** → **About phone** → Tap **Build number** 7 lần để bật Developer Options
2. Quay lại **Settings** → **System** → **Developer options**
3. Tìm **Wireless debugging** và BẬT nó
4. Chọn **Pair code pairing** → Ghi nhớ **Pairing code** và **IP:Port** (VD: `192.168.1.10:42037`)

### Trên Android 10 (Cũ hơn)
Cần cắm dây 1 lần để enable ADB over WiFi:
```bash
# Cắm dây USB, bật USB Debugging rồi chạy:
adb tcpip 5555
adb connect 192.168.1.10:5555  # IP của điện thoại
# Rút dây, giờ có thể debug wireless
```

---

## Bước 2: Pair Điện Thoại với Visual Studio

### Cách 1: Dùng ADB Command Line (Khuyến nghị)
```bash
# 1. Kiểm tra ADB đã cài chưa
adb version

# 2. Pair với điện thoại (dùng Pairing code từ Bước 1)
adb pair 192.168.1.10:42037
# Nhập pairing code khi được hỏi (VD: 123456)

# 3. Connect
adb connect 192.168.1.10:38973  # Port sẽ khác sau khi pair

# 4. Kiểm tra đã connect chưa
adb devices
# Output: 192.168.1.10:38973 device
```

### Cách 2: Dùng Visual Studio GUI
1. Visual Studio → **Tools** → **Android** → **Android Adb Command Prompt**
2. Chạy các lệnh `adb pair` và `adb connect` như trên
3. Hoặc dùng **Android Device Manager** để scan devices

---

## Bước 3: Chạy API Server trên IP WiFi

App sẽ tự động tìm API qua WiFi, nhưng cần đảm bảo API server chạy đúng IP:

### Cách 1: Dùng IP hiện tại trong code
File `TourApp.Mobile/Services/ApiService.cs` dòng 10 đã có default:
```csharp
private const string DefaultUrl = "http://192.168.1.5:5254";
```

### Cách 2: Chạy API với URL cụ thể
Trong `TourApp.API` folder:
```bash
dotnet run --urls "http://0.0.0.0:5254"
```
Hoặc sửa `launchSettings.json`:
```json
"applicationUrl": "http://0.0.0.0:5254"
```

### Cách 3: Từ app, nhấn nút ⚙️ (Settings) trên Map
- Nhập IP: `http://192.168.1.X:5254` (IP máy tính chạy API)
- App sẽ lưu lại và dùng cho lần sau

---

## Bước 4: Deploy App từ Visual Studio

1. Chọn **Debug** → **Target Device** → Chọn device wireless (`192.168.1.10:xxx`)
2. Nhấn **F5** hoặc **Start Debugging**
3. App sẽ cài đặt và chạy trên điện thoại qua WiFi!

---

## Troubleshooting

### Lỗi "Device not found"
```bash
# Kiểm tra ADB
taskkill /f /im adb.exe  # Kill ADB cũ
adb start-server
adb connect 192.168.1.10:5555
```

### Lỗi "Connection refused" từ app
- Kiểmảo Windows Firewall cho phép port 5254
- Chạy API với `dotnet run --urls "http://0.0.0.0:5254"`
- Từ điện thoại, thử mở browser truy cập `http://192.168.1.5:5254/api/poi`

### Lỗi SSL/Certificate
App đã cấu hình bypass SSL cho development trong `ApiService.cs:426-437`

### Tìm IP máy tính (Windows)
```powershell
ipconfig
# Tìm dòng IPv4 Address của WiFi adapter
```

---

## Lệnh Hữu Ích

```bash
# List devices
adb devices -l

# Wireless debug status
adb shell getprop ro.boot.wifidebugging

# Reconnect nếu mất kết nối
adb disconnect
adb connect 192.168.1.10:5555

# Logcat từ device
adb logcat -s "DOTNET"
```
