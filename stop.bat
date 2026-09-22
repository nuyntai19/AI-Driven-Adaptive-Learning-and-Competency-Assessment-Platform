@echo off
title Stop EduTwin Services
cd /d "%~dp0"

echo ================================================================================
echo                     Stopping EduTwin Services
echo ================================================================================
echo.
echo [*] Stopping Docker Compose containers...
docker compose down
echo.
echo [OK] All Docker containers have been stopped and cleaned up.
echo.
timeout /t 3 >nul
exit /b 0
