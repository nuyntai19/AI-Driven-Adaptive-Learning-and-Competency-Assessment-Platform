@echo off
title EduTwin 1-Click Launcher
cd /d "%~dp0"

echo ================================================================================
echo                         EduTwin 1-Click Launcher
echo        AI-Driven Adaptive Learning ^& Competency Assessment Platform
echo ================================================================================
echo.
echo [*] Launching full application stack (MySQL, Backend API, Web Client, Adminer)...
echo.

call "%~dp0run.bat" 1
