@echo off
REM Build script for Family Tree Builder
REM Builds: Debug exe, Release exe (published), and MSI installer
REM Can be run from any directory.

REM Always work relative to the script's own location
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo ========================================
echo  Family Tree Builder - Full Build
echo ========================================
echo.

REM Step 1: Restore NuGet packages
echo [1/4] Restoring packages...
dotnet restore "%ROOT%\family tree maker.sln"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Package restore failed
    pause
    exit /b 1
)
echo.

REM Step 2: Build Debug configuration (for development/testing)
echo [2/4] Building Debug configuration...
dotnet build "%ROOT%\family tree maker.sln" -c Debug --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Debug build failed
    pause
    exit /b 1
)
echo.

REM Step 3: Publish Release (self-contained win-x64 exe + all dependencies)
echo [3/4] Publishing Release (self-contained)...

REM Kill any running instance that may be locking files in the publish folder
taskkill /F /IM FamilyTreeBuilder.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo   Closed running FamilyTreeBuilder instance.
    timeout /t 1 /nobreak >nul
)

dotnet publish "%ROOT%\FamilyTreeApp\FamilyTreeApp.csproj" -c Release -r win-x64 --self-contained true -o "%ROOT%\publish" --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish failed
    pause
    exit /b 1
)
echo.

REM Step 4: Build MSI installer
echo [4/4] Building MSI installer...
pushd "%ROOT%\Installer"
wix build Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o FamilyTreeBuilder.msi
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: MSI build failed
    popd
    pause
    exit /b 1
)
popd
echo.

echo ========================================
echo  Build complete!
echo ========================================
echo.
echo  Debug exe:
echo    FamilyTreeApp\bin\Debug\net9.0-windows\FamilyTreeApp.exe
echo.
echo  Release (published):
echo    publish\FamilyTreeBuilder.exe
echo.
echo  Installer:
echo    Installer\FamilyTreeBuilder.msi
echo.
dir /b "%ROOT%\Installer\*.msi"
echo.

pause
