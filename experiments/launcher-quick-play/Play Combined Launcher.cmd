@echo off
setlocal
cd /d "%~dp0..\.."
node experiments/launcher-quick-play/play-combined.mjs %*
if errorlevel 1 pause
