@echo off
pushd "%~dp0"
if errorlevel 1 goto error
call build_windows.bat
if errorlevel 1 goto error
if not exist "RGBControl.exe" goto error
set "APP_DIR=%LOCALAPPDATA%\RGBControl"
if not exist "%APP_DIR%" mkdir "%APP_DIR%"
if errorlevel 1 goto error
copy /Y "RGBControl.exe" "%APP_DIR%\RGBControl.exe" >nul
if errorlevel 1 goto error
start "" "%APP_DIR%\RGBControl.exe"
popd
exit /b 0

:error
echo Launch failed. Check the build output and local app directory.
pause
exit /b 1
