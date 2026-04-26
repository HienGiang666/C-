---
description: Build APK for Android release
tags: [build, android, apk, release]
---

# Build APK Release cho TourApp.Mobile

## Bước 1: Build APK Release

// turbo
```powershell
cd TourApp.Mobile
dotnet build TourApp.Mobile.csproj -f net9.0-android -c Release -p:AndroidPackageFormat=apk /p:EmbedAssembliesIntoApk=true
```

## Bước 2: Tìm file APK output

File APK sẽ được tạo tại:
```
TourApp.Mobile\bin\Release\net9.0-android\com.companyname.tourapp.mobile-Signed.apk
```

## Bước 3: Copy APK ra thư mục dễ tìm (tùy chọn)

```powershell
Copy-Item "TourApp.Mobile\bin\Release\net9.0-android\com.companyname.tourapp.mobile-Signed.apk" -Destination "TourApp.Mobile\bin\Release\TourApp-v1.0.apk"
```

## Thông số build quan trọng

- **Target Framework**: `net9.0-android`
- **Configuration**: `Release`
- **Package Format**: `apk`
- **Application ID**: `com.companyname.tourapp.mobile`
- **Version**: `1.0`

## Lưu ý

- Lần build đầu tiên sẽ mất nhiều thời gian (tải Android SDK, dependencies)
- Các lần build sau sẽ nhanh hơn
- Đảm bảo Android SDK đã được cài đặt qua Visual Studio Installer
