@echo off
title Stop EduTwin Services
cd /d "%~dp0"

echo ================================================================================
echo                     Stopping EduTwin Services
echo ================================================================================
echo.
echo [*] Stopping Docker Compose containers (Preserving database volume)...
docker compose down
echo.
echo [OK] All EduTwin services have been safely stopped.
echo.
timeout /t 3 >nul
exit /b 0
