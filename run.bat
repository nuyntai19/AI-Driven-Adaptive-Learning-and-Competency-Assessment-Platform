@echo off
setlocal enabledelayedexpansion
title EduTwin Platform Launcher

cd /d "%~dp0"

:: -----------------------------------------------------------------------------
:: Command-Line Argument Dispatch
:: -----------------------------------------------------------------------------
if "%~1"=="1" goto DOCKER_START
if /i "%~1"=="docker" goto DOCKER_START
if "%~1"=="2" goto LOCAL_START
if /i "%~1"=="local" goto LOCAL_START
if "%~1"=="3" goto RUN_TESTS
if /i "%~1"=="test" goto RUN_TESTS
if "%~1"=="4" goto OPEN_BROWSER
if /i "%~1"=="browser" goto OPEN_BROWSER
if "%~1"=="5" goto VIEW_LOGS
if /i "%~1"=="logs" goto VIEW_LOGS
if "%~1"=="6" goto STOP_SERVICES
if /i "%~1"=="stop" goto STOP_SERVICES
if "%~1"=="0" goto EXIT_SCRIPT
if /i "%~1"=="exit" goto EXIT_SCRIPT

:: -----------------------------------------------------------------------------
:: Main Interactive Menu
:: -----------------------------------------------------------------------------
:MENU
cls
echo ================================================================================
echo                            EduTwin Platform Launcher
echo        AI-Driven Adaptive Learning ^& Competency Assessment Platform
echo ================================================================================
echo.
echo   [1] Quick Start with Docker Compose (Recommended - Full Stack)
echo       - Automatically builds ^& starts MySQL, API, Web, and Adminer.
echo       - Auto-seeds DB, runs health checks, and opens browser.
echo.
echo   [2] Local Development Mode (Hot Reload)
echo       - Starts MySQL in Docker + Local .NET API ^& Vite Dev Server.
echo.
echo   [3] Run Automated Test Suite
echo       - Executes .NET backend tests ^& Frontend tests.
echo.
echo   [4] Open Web Application in Browser (http://localhost:3000)
echo.
echo   [5] View Live Container Logs (docker compose logs -f)
echo.
echo   [6] Stop All Services (docker compose down)
echo.
echo   [0] Exit
echo ================================================================================
echo.
set "CHOICE=1"
set /p "CHOICE=Select an option [1-6, 0] (Default is 1): "

:: Remove any trailing spaces from CHOICE
for /f "tokens=* delims= " %%a in ("!CHOICE!") do set "CHOICE=%%a"

if "%CHOICE%"=="1" goto DOCKER_START
if "%CHOICE%"=="2" goto LOCAL_START
if "%CHOICE%"=="3" goto RUN_TESTS
if "%CHOICE%"=="4" goto OPEN_BROWSER
if "%CHOICE%"=="5" goto VIEW_LOGS
if "%CHOICE%"=="6" goto STOP_SERVICES
if "%CHOICE%"=="0" goto EXIT_SCRIPT

echo [!] Invalid choice "%CHOICE%". Defaulting to Docker Compose Start...
ping 127.0.0.1 -n 2 >nul
goto DOCKER_START

:: -----------------------------------------------------------------------------
:: Helper: Ensure .env exists with valid secrets
:: -----------------------------------------------------------------------------
:ENSURE_ENV
if not exist ".env" (
    echo [*] .env file not found. Creating .env from .env.example with secure dev defaults...
    if exist ".env.example" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command ^
            "$c = Get-Content '.env.example' -Raw;" ^
            "$c = $c -replace 'YOUR_DEVELOPMENT_SIGNING_KEY_HERE', 'EduTwin_Development_Signing_Key_2026_Secure32BytesKey!';" ^
            "$c = $c -replace 'change_me_to_a_secure_seed_password', 'SeedPassword123!';" ^
            "$c = $c -replace 'change_me_to_a_secure_platform_admin_password', 'PlatformAdmin123!';" ^
            "Set-Content -Path '.env' -Value $c -Encoding UTF8;"
        echo [OK] Created .env with verified development signing keys and seed credentials.
    ) else (
        echo [ERROR] Error: .env.example not found!
    )
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "$c = Get-Content '.env' -Raw;" ^
        "if ($c -match 'YOUR_DEVELOPMENT_SIGNING_KEY_HERE') {" ^
        "  $c = $c -replace 'YOUR_DEVELOPMENT_SIGNING_KEY_HERE', 'EduTwin_Development_Signing_Key_2026_Secure32BytesKey!';" ^
        "  Set-Content -Path '.env' -Value $c -Encoding UTF8;" ^
        "  Write-Host '[OK] Updated placeholder signing key in .env with valid key.';" ^
        "}"
)
exit /b 0

:: -----------------------------------------------------------------------------
:: Helper: Print Demo Accounts
:: -----------------------------------------------------------------------------
:SHOW_DEMO_ACCOUNTS
echo.
echo ================================================================================
echo                         PRE-SEEDED ACCOUNTS FOR LOGIN
echo ================================================================================
echo   [1] PLATFORM ROOT TENANT (Center Code: PLATFORM)
echo       - Role: Platform Admin
echo         Username: platform.admin      Password: PlatformAdmin123!
echo.
echo   [2] CENTER A (Center Code: EDUTWIN_A - Trung tam EduTwin A)
echo       - Role: Center Manager
echo         Username: manager             Password: SeedPassword123!
echo       - Role: Math Teacher
echo         Username: teacher.math        Password: SeedPassword123!
echo       - Role: English Teacher
echo         Username: teacher.english     Password: SeedPassword123!
echo       - Role: Students (5 students)
echo         Usernames: student01, student02, student03, student04, student05
echo         Password:  SeedPassword123!
echo.
echo   [3] CENTER B (Center Code: EDUTWIN_B - Trung tam EduTwin B)
echo       - Role: Center Manager
echo         Username: manager             Password: SeedPassword123!
echo       - Role: Math Teacher
echo         Username: teacher.math        Password: SeedPassword123!
echo       - Role: English Teacher
echo         Username: teacher.english     Password: SeedPassword123!
echo       - Role: Students (5 students)
echo         Usernames: student01, student02, student03, student04, student05
echo         Password:  SeedPassword123!
echo ================================================================================
echo.
exit /b 0

:: -----------------------------------------------------------------------------
:: 1. Docker Compose Mode
:: -----------------------------------------------------------------------------
:DOCKER_START
echo.
echo ================================================================================
echo  Starting EduTwin via Docker Compose (Full Stack)
echo ================================================================================
call :ENSURE_ENV

echo [*] Checking Docker daemon status...
docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker daemon is not running or Docker Desktop is not started.
    echo [WARN] Please launch Docker Desktop and try again.
    echo.
    echo Press any key to return to menu...
    pause >nul
    goto MENU
)

echo [*] Building and launching containers in background (MySQL, API, Web, Adminer)...
docker compose up -d --build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker Compose encountered an error while starting services.
    pause
    goto MENU
)

echo [*] Waiting for API and Web services to become healthy...
set /a ATTEMPTS=0
:WAIT_LOOP
set /a ATTEMPTS+=1
ping 127.0.0.1 -n 3 >nul
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "try { $r = Invoke-RestMethod -Uri 'http://localhost:5000/api/v1/health/live' -TimeoutSec 2 -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
if %ERRORLEVEL% equ 0 goto SERVICES_READY

if %ATTEMPTS% geq 25 (
    echo.
    echo [WARN] Health check timed out, but containers may still be initializing.
    goto SERVICES_READY
)
<nul set /p =.
goto WAIT_LOOP

:SERVICES_READY
echo.
echo ================================================================================
echo                     EduTwin Services are Ready!
echo ================================================================================
echo   [OK] Web Application:    http://localhost:3000
echo   [OK] Backend API:        http://localhost:5000
echo   [OK] API Health Check:   http://localhost:5000/api/v1/health/live
echo   [OK] Adminer DB Manager: http://localhost:8080 (Server: mysql, User: edutwin_user)

call :SHOW_DEMO_ACCOUNTS

echo [*] Opening Web Application in your default browser...
start http://localhost:3000

echo.
echo Options:
echo   - Type 5 to stream container logs
echo   - Run stop.bat or select option 6 to shutdown services
echo.
pause
goto MENU

:: -----------------------------------------------------------------------------
:: 2. Local Dev Mode
:: -----------------------------------------------------------------------------
:LOCAL_START
echo.
echo ================================================================================
echo  Starting EduTwin in Local Development Mode
echo ================================================================================
call :ENSURE_ENV

echo [*] Starting MySQL container via Docker Compose...
docker compose up -d mysql >nul 2>&1

if not exist "web\edutwin-web\node_modules" (
    echo [*] Frontend dependencies not found. Installing node packages...
    cd web\edutwin-web
    call npm install
    cd ..\..
)

echo [*] Launching ASP.NET Core API server in a separate window...
start "EduTwin API Server (.NET 10)" cmd /k "cd /d "%~dp0src\EduTwin.API" && set ASPNETCORE_ENVIRONMENT=Development && set ASPNETCORE_URLS=http://localhost:5000 && dotnet run"

echo [*] Launching Vite React Frontend server in a separate window...
start "EduTwin Web Client (Vite React)" cmd /k "cd /d "%~dp0web\edutwin-web" && npm run dev"

echo.
echo ================================================================================
echo  Local Development Servers Started!
echo ================================================================================
echo   [OK] Web Client: http://localhost:3000
echo   [OK] API Server: http://localhost:5000

call :SHOW_DEMO_ACCOUNTS

ping 127.0.0.1 -n 4 >nul
start http://localhost:3000

echo Press any key to return to menu...
pause >nul
goto MENU

:: -----------------------------------------------------------------------------
:: 3. Run Automated Tests
:: -----------------------------------------------------------------------------
:RUN_TESTS
echo.
echo ================================================================================
echo  Running EduTwin Verification ^& Test Suite
echo ================================================================================
call :ENSURE_ENV

echo [*] Running .NET backend test suite...
dotnet test EduTwin.sln --configuration Release --maxcpucount:1

echo.
echo [*] Running Frontend tests...
cd web\edutwin-web
call npm test
cd ..\..

echo.
echo Press any key to return to menu...
pause >nul
goto MENU

:: -----------------------------------------------------------------------------
:: 4. Open Browser
:: -----------------------------------------------------------------------------
:OPEN_BROWSER
echo [*] Opening EduTwin services in default browser...
start http://localhost:3000
start http://localhost:5000/api/v1/health/live
start http://localhost:8080
goto MENU

:: -----------------------------------------------------------------------------
:: 5. View Logs
:: -----------------------------------------------------------------------------
:VIEW_LOGS
echo.
echo [*] Streaming Docker Compose logs (Press Ctrl+C to stop stream)...
docker compose logs -f
goto MENU

:: -----------------------------------------------------------------------------
:: 6. Stop Services
:: -----------------------------------------------------------------------------
:STOP_SERVICES
echo.
echo ================================================================================
echo  Stopping EduTwin Services
echo ================================================================================
echo [*] Stopping and removing Docker containers...
docker compose down
echo [OK] All containers stopped.
ping 127.0.0.1 -n 3 >nul
goto MENU

:: -----------------------------------------------------------------------------
:: 0. Exit
:: -----------------------------------------------------------------------------
:EXIT_SCRIPT
echo Exiting EduTwin Launcher. Goodbye!
ping 127.0.0.1 -n 2 >nul
exit /b 0
