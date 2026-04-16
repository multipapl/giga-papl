@echo off
setlocal

cd /d "%~dp0"

set "PROJECT=%CD%\src\BlenderToolbox.App\BlenderToolbox.App.csproj"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet SDK was not found in PATH.
    echo Install .NET SDK and try again.
    pause
    exit /b 1
)

if not exist "%PROJECT%" (
    echo [ERROR] Project file was not found:
    echo %PROJECT%
    pause
    exit /b 1
)

dotnet run --project "%PROJECT%" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo Process finished with exit code %EXIT_CODE%.
    pause
)

exit /b %EXIT_CODE%
