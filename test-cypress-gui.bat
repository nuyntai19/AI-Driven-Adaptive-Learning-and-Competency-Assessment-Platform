@echo off
setlocal enabledelayedexpansion
title EduTwin - Cypress E2E GUI Runner

cd /d "%~dp0"

:: Set up embedded portable Node.js and Cypress cache
set "NODE_DIR=%~dp0test\.runtime\node-v22.23.4-win-x64"
set "PATH=%NODE_DIR%;%PATH%"
set "CYPRESS_CACHE_FOLDER=%~dp0test\.runtime\cypress-cache"
set "ELECTRON_RUN_AS_NODE="

:MENU
cls
echo ================================================================================
echo                     EduTwin Cypress E2E GUI Runner
echo ================================================================================
echo.
echo   [1] Chay TOAN BO test tren trinh duyet Google Chrome (Headed Mode)
echo       - Tu dong bat Chrome, chay tuan tu tat ca API va UI specs.
echo       - Tu dong khoi tao stack va don dep sach se sau khi hoan tat.
echo.
echo   [2] Mo Bang dieu khien Cypress tuong tac (Cypress Interactive GUI)
echo       - Mo cua so Cypress App de ban tu do click "Run all specs" hoac tung spec.
echo       - Ho tro debug DOM snapshot, xem console va network request truc tiep.
echo       - Tu dong don dep stack khi ban dong Cypress App.
echo.
echo   [3] Chay TOAN BO test tren trinh duyet Microsoft Edge (Headed Mode)
echo.
echo   [0] Thoat
echo ================================================================================
echo.
set "CHOICE=1"
set /p "CHOICE=Nhap lua chon [1, 2, 3, 0] (Mac dinh la 1): "

for /f "tokens=* delims= " %%a in ("!CHOICE!") do set "CHOICE=%%a"

if "%CHOICE%"=="1" goto RUN_CHROME
if "%CHOICE%"=="2" goto OPEN_CYPRESS_APP
if "%CHOICE%"=="3" goto RUN_EDGE
if "%CHOICE%"=="0" exit /b 0

echo [!] Lua chon khong hop le.
ping 127.0.0.1 -n 2 >nul
goto MENU

:RUN_CHROME
echo.
echo ================================================================================
echo  Dang khoi dong tat ca test Cypress tren Google Chrome (Headed)...
echo ================================================================================
cd /d "%~dp0test"
node scripts\run-cypress.cjs --headed --browser chrome
cd /d "%~dp0"
echo.
echo Nhan phim bat ky de quay lai menu...
pause >nul
goto MENU

:RUN_EDGE
echo.
echo ================================================================================
echo  Dang khoi dong tat ca test Cypress tren Microsoft Edge (Headed)...
echo ================================================================================
cd /d "%~dp0test"
node scripts\run-cypress.cjs --headed --browser edge
cd /d "%~dp0"
echo.
echo Nhan phim bat ky de quay lai menu...
pause >nul
goto MENU

:OPEN_CYPRESS_APP
echo.
echo ================================================================================
echo  Khoi dong moi truong va mo Bang dieu khien Cypress Interactive UI...
echo ================================================================================
echo [*] Dang khoi tao Acceptance Stack (API + Web + Disposable DB)...
node "%~dp0scripts\e2e\acceptance_stack.cjs" start
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Khong the khoi tao stack test. Kiem tra Docker da bat chua.
    pause
    goto MENU
)

echo.
echo [OK] Acceptance Stack da san sang tai http://127.0.0.1:3002
echo [*] Dang khoi chay Cypress Interactive App...
echo [!] LUU Y: Khi ban dong cua so Cypress App, chuong trinh se tu dong thu hoi va don dep database test.
echo.

cd /d "%~dp0test"
node node_modules\cypress\bin\cypress open --config-file cypress.config.cjs

echo.
echo [*] Dang don dep Acceptance Stack va thu hoi database test...
cd /d "%~dp0"
node "%~dp0scripts\e2e\acceptance_stack.cjs" stop
echo [OK] Da don dep hoan tat!
echo.
echo Nhan phim bat ky de quay lai menu...
pause >nul
goto MENU
