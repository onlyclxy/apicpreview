@echo off
echo Building Image Viewer...
echo.

dotnet --version >nul 2>&1
if errorlevel 1 (
    echo Error: dotnet command not found
    echo Please install .NET SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Cleaning old files...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

echo Building project...
dotnet build apicpreview.csproj --configuration Debug

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================
    echo        BUILD SUCCESSFUL!
    echo ========================================
    echo.
    echo Executable: bin\Debug\apicpreview.exe
    dir "bin\Debug\apicpreview.exe" | findstr "apicpreview.exe"
    echo.
    echo Ready to run!
    echo.
    set /p "run=Run now? (y/n): "
    if /i "%run%"=="y" (
        echo Starting application...
        start "" "bin\Debug\apicpreview.exe"
    )
) else (
    echo.
    echo ========================================
    echo        BUILD FAILED!
    echo ========================================
    echo.
    echo Please check for errors
)

echo.
pause 