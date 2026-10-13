@echo off
setlocal

rem Put this file in the repo root (next to the SshSenderGui folder) on your dev PC.
cd /d "%~dp0"

set "PROJ=%~dp0SshSenderGui\SshSenderGui.csproj"
set "OUT_DIR=%~dp0publish"

if not exist "%PROJ%" (
    echo Could not find %PROJ%
    echo Make sure publish.bat is in the repo root, next to the SshSenderGui folder.
    pause
    exit /b 1
)

echo === Publishing standalone exe ===
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT_DIR%"
if errorlevel 1 (
    echo.
    echo Publish failed. See the messages above.
    pause
    exit /b 1
)

echo.
echo Done. Copy this file to any Windows PC and double-click it:
echo %OUT_DIR%\SshSenderGui.exe
explorer "%OUT_DIR%"

endlocal