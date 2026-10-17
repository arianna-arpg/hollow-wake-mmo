@echo off
setlocal
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Install Node.js LTS, then open Play.cmd again.
  pause
  exit /b 1
)
node scripts/play-local.mjs %*
if errorlevel 1 pause
