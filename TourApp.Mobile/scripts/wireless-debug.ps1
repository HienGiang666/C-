#Requires -Version 5.1
<#
.SYNOPSIS
    Kết nối Android device qua WiFi cho wireless debugging
.EXAMPLE
    .\wireless-debug.ps1 -DeviceIP 192.168.1.10
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$DeviceIP,
    
    [int]$Port = 5555,
    
    [switch]$Pair,
    [int]$PairPort = 42037,
    [string]$PairCode
)

Write-Host "=== Android Wireless Debugging Setup ===" -ForegroundColor Cyan

# 1. Kiểm tra ADB
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) {
    # Tìm trong Android SDK
    $sdkPaths = @(
        "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
        "${env:ProgramFiles(x86)}\Android\android-sdk\platform-tools\adb.exe",
        "$env:ProgramFiles\Android\android-sdk\platform-tools\adb.exe"
    )
    foreach ($path in $sdkPaths) {
        if (Test-Path $path) {
            $adbPath = $path
            break
        }
    }
    if (-not $adbPath) {
        Write-Error "Không tìm thấy ADB. Vui lòng cài Android SDK hoặc thêm vào PATH"
        exit 1
    }
} else {
    $adbPath = $adb.Source
}

Write-Host "ADB found: $adbPath" -ForegroundColor Green

# 2. Kill ADB server cũ (nếu lỗi)
& $adbPath kill-server 2>$null
Start-Sleep -Seconds 1
& $adbPath start-server

# 3. Pair nếu cần (Android 11+)
if ($Pair) {
    if (-not $PairCode) {
        Write-Host ""
        Write-Host "Lấy Pairing Code từ điện thoại:" -ForegroundColor Yellow
        Write-Host "Settings → Developer options → Wireless debugging → Pair code pairing" -ForegroundColor Yellow
        $PairCode = Read-Host "Nhập Pairing Code (6 số)"
    }
    
    Write-Host "Pairing với $DeviceIP`:$PairPort ..." -ForegroundColor Cyan
    & $adbPath pair "$DeviceIP`:$PairPort" $PairCode
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Pair thất bại. Kiểm tra IP và Pairing Code."
        exit 1
    }
}

# 4. Connect
Write-Host "Connecting to $DeviceIP`:$Port ..." -ForegroundColor Cyan
& $adbPath connect "$DeviceIP`:$Port"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Connect thất bại."
    Write-Host ""
    Write-Host "Thử cách khác (cần cắm dây 1 lần):" -ForegroundColor Yellow
    Write-Host "1. Cắm USB, bật USB Debugging"
    Write-Host "2. Chạy: adb tcpip 5555"
    Write-Host "3. Rút dây, chạy: adb connect ${DeviceIP}:5555"
    exit 1
}

# 5. Verify
Write-Host ""
Write-Host "Connected devices:" -ForegroundColor Green
& $adbPath devices -l | Select-String "device$"

Write-Host ""
Write-Host "✅ Success! Giờ bạn có thể chọn device trong Visual Studio và deploy app" -ForegroundColor Green
Write-Host ""
Write-Host "Lệnh hữu ích:" -ForegroundColor Cyan
Write-Host "  adb logcat -s DOTNET    # Xem log .NET MAUI"
Write-Host "  adb disconnect          # Ngắt kết nối"
