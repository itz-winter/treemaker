@echo off
REM Build script for Family Tree Builder MSI Installer
REM Run this from the solution root directory

echo ========================================
echo Family Tree Builder - Build Installer
echo ========================================
echo.

REM Step 1: Build and Publish the application
echo [1/3] Publishing application...
cd FamilyTreeApp
dotnet publish -c Release -r win-x64 --self-contained true -o "..\publish"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to publish application
    exit /b 1
)
cd ..
echo.

REM Step 2: Build the MSI installer
echo [2/3] Building MSI installer...
cd Installer
REM Include the Util extension to provide WixShellExec / util custom actions
wix build Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o FamilyTreeBuilder.msi
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build MSI installer
    exit /b 1
)
cd ..
echo.

REM Step 3: Show result
echo [3/3] Build complete!
echo.
echo ========================================
echo Installer created at:
echo   Installer\FamilyTreeBuilder.msi
echo ========================================
echo.

dir /b Installer\*.msi
