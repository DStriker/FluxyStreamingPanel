@echo off
if not exist ".env" (
    echo .env not found - generating secrets...
    powershell -ExecutionPolicy Bypass -File "scripts\init-secrets.ps1"
    if errorlevel 1 (
        echo Failed to generate .env
        pause
        exit /b 1
    )
    echo.
)
docker compose up -d
pause
