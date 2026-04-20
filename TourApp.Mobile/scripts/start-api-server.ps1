#Requires -Version 5.1
<#
.SYNOPSIS
    Tự động lấy IP WiFi, cập nhật ApiService.cs và chạy API server
.EXAMPLE
    .\scripts\start-api-server.ps1
#>

$ErrorActionPreference = "Stop"
Write-Host "=== Start API Server with WiFi IP ===" -ForegroundColor Cyan

# 1. Lấy IP WiFi
Write-Host "Detecting WiFi IP..." -ForegroundColor Yellow
$wifiIP = $null
$subnet = $null

try {
    foreach ($adapter in Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.Name -notlike '*Loopback*' }) {
        $ipInfo = Get-NetIPAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | 
                  Where-Object { $_.IPAddress -match '^(192\.168\.|10\.|172\.(1[6-9]|2[0-9]|3[01])\.)' } |
                  Select-Object -First 1
        if ($ipInfo) {
            $wifiIP = $ipInfo.IPAddress
            $subnet = $wifiIP -replace '\.\d+$', ''
            break
        }
    }
} catch {
    # Fallback to ipconfig
    $ipconfig = ipconfig | findstr /i "IPv4"
    foreach ($line in $ipconfig) {
        if ($line -match '(192\.168\.\d+\.\d+|10\.\d+\.\d+\.\d+|172\.(1[6-9]|2[0-9]|3[01])\.\d+\.\d+)') {
            $wifiIP = $matches[1]
            $subnet = $wifiIP -replace '\.\d+$', ''
            break
        }
    }
}

if (-not $wifiIP) {
    Write-Error "Khong tim thay IP WiFi. Dam bao may tinh dang ket noi WiFi."
    exit 1
}

Write-Host "WiFi IP detected: $wifiIP" -ForegroundColor Green
Write-Host "Subnet: $subnet" -ForegroundColor Green

# 2. Cập nhật ApiService.cs với IP đúng
$apiServicePath = "$PSScriptRoot\..\TourApp.Mobile\Services\ApiService.cs"
if (Test-Path $apiServicePath) {
    $content = Get-Content $apiServicePath -Raw
    $oldPattern = 'private const string DefaultUrl = "http://192\.168\.\d+\.\d+:5254"'
    $newValue = "private const string DefaultUrl = `"http://$wifiIP`:5254`""
    
    if ($content -match $oldPattern) {
        $content = $content -replace $oldPattern, $newValue
        Set-Content $apiServicePath $content -NoNewline
        Write-Host "Updated ApiService.cs with IP: $wifiIP" -ForegroundColor Green
    } else {
        # Thử pattern khác nếu IP đã được sửa trước đó
        $oldPattern2 = 'private const string DefaultUrl = "http://[^"]+"'
        if ($content -match $oldPattern2) {
            $content = $content -replace $oldPattern2, $newValue
            Set-Content $apiServicePath $content -NoNewline
            Write-Host "Updated ApiService.cs with new IP: $wifiIP" -ForegroundColor Green
        }
    }
}

# 3. Mở Firewall port 5254
Write-Host "Opening firewall port 5254..." -ForegroundColor Yellow
$firewallRule = Get-NetFirewallRule -DisplayName "TourApp API 5254" -ErrorAction SilentlyContinue
if (-not $firewallRule) {
    New-NetFirewallRule -DisplayName "TourApp API 5254" -Direction Inbound -Protocol TCP -LocalPort 5254 -Action Allow | Out-Null
    Write-Host "Firewall rule created" -ForegroundColor Green
} else {
    Write-Host "Firewall rule already exists" -ForegroundColor Green
}

# 4. Hiển thị thông tin
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  IP may tinh: $wifiIP" -ForegroundColor Green
Write-Host "  API URL: http://$wifiIP`:5254" -ForegroundColor Green
Write-Host "  Swagger: http://$wifiIP`:5254/swagger" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Nhap IP nay vao app (nhan nut ⚙️ tren Map):" -ForegroundColor Yellow
Write-Host "  http://$wifiIP`:5254" -ForegroundColor White -BackgroundColor DarkGreen
Write-Host ""

# 5. Chạy API server
$apiPath = "$PSScriptRoot\..\TourApp.API"
Write-Host "Starting API server..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

try {
    Set-Location $apiPath
    dotnet run --urls "http://0.0.0.0:5254" --launch-profile http
} finally {
    Set-Location $PSScriptRoot\..
}
