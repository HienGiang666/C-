@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ==========================================
echo  Khoi dong API Server (Auto detect IP)
echo ==========================================
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0start-api-server.ps1"
pause
