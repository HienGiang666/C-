@echo off
chcp 65001 >nul
echo ==========================================
echo  Android Wireless Debugging - Quick Connect
echo ==========================================
echo.

REM Tìm IP WiFi của máy tính
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /i "IPv4" ^| findstr /i "192.168. 10.0. 172."') do (
    set PC_IP=%%a
    goto :found
)
:found
echo IP may tinh cua ban: %PC_IP%
echo.

if "%1"=="" (
    echo Usage: wireless-debug.bat [IP_DIEN_THOAI] [PAIR_CODE]
    echo.
    echo Vi du: wireless-debug.bat 192.168.1.10 123456
    echo.
    echo De lay IP dien thoai:
    echo   Settings -^> About phone -^> Status -^> IP address
    echo.
    pause
    exit /b 1
)

set PHONE_IP=%1
set PAIR_CODE=%2

echo Dien thoai IP: %PHONE_IP%

if not "%PAIR_CODE%"=="" (
    echo Pairing voi code: %PAIR_CODE%
    adb pair %PHONE_IP%:42037 %PAIR_CODE%
)

echo Connecting to %PHONE_IP%:5555 ...
adb connect %PHONE_IP%:5555

if %ERRORLEVEL% neq 0 (
    echo.
    echo [LOI] Khong ket noi duoc. Thu cach nay:
    echo 1. Cam day USB, bat USB Debugging
    echo 2. Chay: adb tcpip 5555
    echo 3. Rut day, chay lai script nay
    pause
    exit /b 1
)

echo.
echo [OK] Ket noi thanh cong!
echo.
adb devices
echo.
echo Gio ban co the mo Visual Studio va chon device de deploy app.
pause
