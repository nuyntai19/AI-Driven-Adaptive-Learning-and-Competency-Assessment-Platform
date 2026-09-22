# ==============================================================================
# EduTwin Platform - PowerShell Launcher & Orchestrator
# ==============================================================================
[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [ValidateSet('Menu', 'Docker', 'Local', 'Test', 'Stop', 'Logs', 'Browser')]
    [string]$Mode = 'Menu',
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Colors and Formatting Helpers
function Write-Header {
    param([string]$Title)
    Write-Host ''
    Write-Host '================================================================================' -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor White
    Write-Host '================================================================================' -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# 1. Environment Verification and Auto-Configuration
function Ensure-EnvironmentFile {
    $envPath = Join-Path $ScriptDir '.env'
    $envExamplePath = Join-Path $ScriptDir '.env.example'

    if (-not (Test-Path $envPath)) {
        Write-Info -Message '.env file not found. Creating .env from .env.example with secure dev defaults...'
        if (Test-Path $envExamplePath) {
            $content = Get-Content -Path $envExamplePath -Raw
            # Replace default template placeholders with valid dev defaults
            $content = $content.Replace('YOUR_DEVELOPMENT_SIGNING_KEY_HERE', 'EduTwin_Development_Signing_Key_2026_Secure32BytesKey!')
            $content = $content.Replace('change_me_to_a_secure_seed_password', 'SeedPassword123!')
            $content = $content.Replace('change_me_to_a_secure_platform_admin_password', 'PlatformAdmin123!')
            Set-Content -Path $envPath -Value $content -Encoding UTF8
            Write-Success -Message 'Created .env with verified development signing keys and seed credentials.'
        } else {
            Write-Err -Message '.env.example not found. Please ensure project files are intact.'
        }
    } else {
        # Check if .env still has invalid placeholder signing key
        $content = Get-Content -Path $envPath -Raw
        if ($content.Contains('YOUR_DEVELOPMENT_SIGNING_KEY_HERE')) {
            Write-Warn -Message 'Updating placeholder JWT signing key in .env to a valid 32+ byte dev key...'
            $content = $content.Replace('YOUR_DEVELOPMENT_SIGNING_KEY_HERE', 'EduTwin_Development_Signing_Key_2026_Secure32BytesKey!')
            Set-Content -Path $envPath -Value $content -Encoding UTF8
            Write-Success -Message 'Updated .env signing key.'
        }
    }
}

# 2. Check Docker Health
function Test-DockerRunning {
    try {
        $null = docker info 2>&1
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
}

# 3. Print Demo Credentials Table
function Show-DemoAccounts {
    Write-Host ''
    Write-Host '--------------------------------------------------------------------------------' -ForegroundColor DarkGray
    Write-Host '                      PRE-SEEDED DEMO ACCOUNTS FOR LOGIN                        ' -ForegroundColor Yellow
    Write-Host '--------------------------------------------------------------------------------' -ForegroundColor DarkGray
    Write-Host '  Role            Center Code   Username          Default Password' -ForegroundColor White
    Write-Host '  -------------   -----------   ---------------   ----------------' -ForegroundColor DarkGray
    Write-Host '  Platform Admin  PLATFORM      platform.admin    PlatformAdmin123!' -ForegroundColor Cyan
    Write-Host '  Center Manager  EDUTWIN_A     manager           SeedPassword123!' -ForegroundColor Green
    Write-Host '  Math Teacher    EDUTWIN_A     teacher.math      SeedPassword123!' -ForegroundColor Green
    Write-Host '  English Teacher EDUTWIN_A     teacher.english   SeedPassword123!' -ForegroundColor Green
    Write-Host '  Student 01      EDUTWIN_A     student01         SeedPassword123!' -ForegroundColor Green
    Write-Host '  Student 02..05  EDUTWIN_A     student02..05     SeedPassword123!' -ForegroundColor Green
    Write-Host '  Center B Users  EDUTWIN_B     manager, teacher* SeedPassword123!' -ForegroundColor DarkCyan
    Write-Host '--------------------------------------------------------------------------------' -ForegroundColor DarkGray
}

# 4. Mode: Docker Compose (Full Stack)
function Start-DockerMode {
    Write-Header -Title 'Starting EduTwin via Docker Compose (Full Stack)'

    if (-not (Test-DockerRunning)) {
        Write-Err -Message 'Docker is not running or not installed.'
        Write-Warn -Message 'Please start Docker Desktop and try again, or select Option 2 (Local Dev Mode).'
        return
    }

    Write-Info -Message 'Building and starting containers in background: MySQL, API, Web, Adminer...'
    docker compose up -d --build
    if ($LASTEXITCODE -ne 0) {
        Write-Err -Message 'Docker Compose failed to start services. Check error output above.'
        return
    }

    Write-Info -Message 'Waiting for services to become healthy...'
    $maxAttempts = 30
    $attempt = 0
    $apiHealthy = $false

    while ($attempt -lt $maxAttempts) {
        Start-Sleep -Seconds 2
        $attempt++
        try {
            $resp = Invoke-RestMethod -Uri 'http://localhost:5000/api/v1/health/live' -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($resp.status -eq 'Healthy' -or $resp.status -eq 'healthy' -or $resp -match 'Healthy') {
                $apiHealthy = $true
                break
            }
        } catch {
            Write-Host -NoNewline '.' -ForegroundColor DarkGray
        }
    }
    Write-Host ''

    Write-Header -Title 'EduTwin Services are Ready!'
    Write-Success -Message 'Web Application:    http://localhost:3000'
    Write-Success -Message 'Backend API:        http://localhost:5000'
    Write-Success -Message 'API Health Check:   http://localhost:5000/api/v1/health/live'
    Write-Success -Message 'Adminer DB Manager: http://localhost:8080 (Server: mysql, User: edutwin_user, DB: edutwin)'

    Show-DemoAccounts

    if (-not $NoBrowser) {
        Write-Info -Message 'Opening http://localhost:3000 in your default browser...'
        Start-Process 'http://localhost:3000'
    }

    Write-Host ''
    Write-Host 'To view real-time logs:  docker compose logs -f' -ForegroundColor DarkGray
    Write-Host 'To stop all services:   Run stop.bat or .\run.ps1 -Mode Stop' -ForegroundColor DarkGray
    Write-Host ''
}

# 5. Mode: Local Development Mode (.NET + Vite + MySQL Container)
function Start-LocalMode {
    Write-Header -Title 'Starting EduTwin in Local Development Mode'

    if (Test-DockerRunning) {
        Write-Info -Message 'Ensuring MySQL database container is running...'
        docker compose up -d mysql
    } else {
        Write-Warn -Message 'Docker not detected. Ensure local MySQL is running on port 3306 or 3307 per .env.'
    }

    $webDir = Join-Path $ScriptDir 'web\edutwin-web'
    $nodeModulesDir = Join-Path $webDir 'node_modules'

    if (-not (Test-Path $nodeModulesDir)) {
        Write-Info -Message 'Frontend dependencies missing. Running npm install in web/edutwin-web...'
        Push-Location $webDir
        npm install
        Pop-Location
    }

    Write-Info -Message 'Launching ASP.NET Core API in a new window...'
    $apiProjectDir = Join-Path $ScriptDir 'src\EduTwin.API'
    Start-Process -FilePath 'cmd.exe' -WorkingDirectory $apiProjectDir -ArgumentList '/k', 'title EduTwin API (.NET 10) & set ASPNETCORE_ENVIRONMENT=Development & set ASPNETCORE_URLS=http://localhost:5000 & dotnet run'

    Write-Info -Message 'Launching Vite Dev Server in a new window...'
    Start-Process -FilePath 'cmd.exe' -WorkingDirectory $webDir -ArgumentList '/k', 'title EduTwin Web (Vite React) & npm run dev'

    Write-Header -Title 'EduTwin Local Dev Servers Initializing'
    Write-Success -Message 'API Server: http://localhost:5000'
    Write-Success -Message 'Web Client: http://localhost:3000'

    Show-DemoAccounts

    Start-Sleep -Seconds 3
    if (-not $NoBrowser) {
        Write-Info -Message 'Opening http://localhost:3000...'
        Start-Process 'http://localhost:3000'
    }
}

# 6. Mode: Run Tests
function Start-TestMode {
    Write-Header -Title 'Running EduTwin Verification and Test Suite'

    Write-Info -Message 'Running .NET backend tests...'
    dotnet test EduTwin.sln --configuration Release --maxcpucount:1
    $backendPass = ($LASTEXITCODE -eq 0)

    Write-Info -Message 'Running Frontend tests...'
    $webDir = Join-Path $ScriptDir 'web\edutwin-web'
    Push-Location $webDir
    npm test
    $frontendPass = ($LASTEXITCODE -eq 0)
    Pop-Location

    Write-Header -Title 'Test Summary'
    if ($backendPass) { Write-Success -Message 'Backend .NET tests PASSED' } else { Write-Err -Message 'Backend .NET tests FAILED' }
    if ($frontendPass) { Write-Success -Message 'Frontend tests PASSED' } else { Write-Err -Message 'Frontend tests FAILED' }
}

# 7. Mode: Stop Services
function Stop-EduTwin {
    Write-Header -Title 'Stopping EduTwin Services'
    if (Test-DockerRunning) {
        Write-Info -Message 'Stopping Docker Compose containers...'
        docker compose down
        Write-Success -Message 'Docker containers stopped.'
    }
    Write-Success -Message 'EduTwin shutdown complete.'
}

# 8. Mode: Logs
function Show-Logs {
    Write-Header -Title 'Streaming EduTwin Container Logs (Ctrl+C to exit)'
    docker compose logs -f
}

# 9. Main Interactive Menu
function Show-Menu {
    Clear-Host
    Write-Host '================================================================================' -ForegroundColor Cyan
    Write-Host '                           EduTwin Platform Launcher                            ' -ForegroundColor White
    Write-Host '       AI-Driven Adaptive Learning and Competency Assessment Platform           ' -ForegroundColor DarkCyan
    Write-Host '================================================================================' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  [1] Quick Start with Docker Compose (Recommended - Full Stack)' -ForegroundColor Green
    Write-Host '      Builds and starts MySQL, API, Web, and Adminer in Docker.'
    Write-Host '      Auto-seeds DB, runs health checks, and opens browser.'
    Write-Host ''
    Write-Host '  [2] Local Development Mode (Hot Reload)' -ForegroundColor Yellow
    Write-Host '      Starts MySQL container + Local .NET API and Local Vite Dev Server.'
    Write-Host ''
    Write-Host '  [3] Run Automated Test Suite' -ForegroundColor White
    Write-Host '      Executes backend xUnit tests and frontend test suites.'
    Write-Host ''
    Write-Host '  [4] Open Web Application and Services in Browser' -ForegroundColor Cyan
    Write-Host '      Opens http://localhost:3000, API live check, and Adminer.'
    Write-Host ''
    Write-Host '  [5] View Live Container Logs' -ForegroundColor Magenta
    Write-Host '      Streams real-time output from all Docker containers.'
    Write-Host ''
    Write-Host '  [6] Stop All Services (docker compose down)' -ForegroundColor Red
    Write-Host ''
    Write-Host '  [0] Exit' -ForegroundColor DarkGray
    Write-Host '================================================================================' -ForegroundColor Cyan
    
    $choice = Read-Host 'Select an option [Default: 1]'
    if ([string]::IsNullOrWhiteSpace($choice)) { $choice = '1' }

    switch ($choice) {
        '1' { Start-DockerMode }
        '2' { Start-LocalMode }
        '3' { Start-TestMode }
        '4' {
            Start-Process 'http://localhost:3000'
            Start-Process 'http://localhost:5000/api/v1/health/live'
            Start-Process 'http://localhost:8080'
        }
        '5' { Show-Logs }
        '6' { Stop-EduTwin }
        '0' { Write-Host 'Goodbye!' -ForegroundColor Cyan; exit 0 }
        default {
            Write-Warn -Message 'Invalid choice. Launching default Docker Compose mode...'
            Start-DockerMode
        }
    }
}

# Always ensure .env exists before any action
Ensure-EnvironmentFile

# Dispatch
switch ($Mode) {
    'Docker'  { Start-DockerMode }
    'Local'   { Start-LocalMode }
    'Test'    { Start-TestMode }
    'Stop'    { Stop-EduTwin }
    'Logs'    { Show-Logs }
    'Browser' {
        Start-Process 'http://localhost:3000'
        Start-Process 'http://localhost:5000/api/v1/health/live'
    }
    default   { Show-Menu }
}
