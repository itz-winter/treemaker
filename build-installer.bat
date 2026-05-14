@echo off
REM Build script for Family Tree Builder
REM Builds: Debug exe, Release exe (published), and MSI installer
REM Run this from the solution root directory

echo ========================================
echo  Family Tree Builder - Full Build
echo ========================================
echo.

REM Step 1: Restore NuGet packages
echo [1/4] Restoring packages...
dotnet restore "family tree maker.sln"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Package restore failed
    exit /b 1
)
echo.

REM Step 2: Build Debug configuration (for development/testing)
echo [2/4] Building Debug configuration...
dotnet build "family tree maker.sln" -c Debug --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Debug build failed
    exit /b 1
)
echo.

REM Step 3: Publish Release (self-contained win-x64 exe + all dependencies)
echo [3/4] Publishing Release (self-contained)...
dotnet publish FamilyTreeApp\FamilyTreeApp.csproj -c Release -r win-x64 --self-contained true -o "publish" --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish failed
    exit /b 1
)
echo.

REM Step 4: Build MSI installer
echo [4/4] Building MSI installer...
cd Installer
wix build Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o FamilyTreeBuilder.msi
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: MSI build failed
    cd ..
    exit /b 1
)
cd ..
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
dir /b Installer\*.msi
echo.
