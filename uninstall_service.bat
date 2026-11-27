@echo off
setlocal
fltmc >nul 2>&1
if errorlevel 1 (
    set "RGB_CONTROL_UNINSTALLER=%~f0"
    powershell.exe -NoProfile -Command "try { Start-Process -FilePath $env:RGB_CONTROL_UNINSTALLER -Verb RunAs -ErrorAction Stop } catch { Write-Error $_; exit 1 }"
    exit /b
)
sc.exe query RGBControlService >nul 2>&1
if errorlevel 1 (
    echo RGB Control service is already removed.
    pause
    exit /b 0
)
sc.exe stop RGBControlService >nul 2>&1
for /L %%I in (1,1,15) do (
    sc.exe query RGBControlService | findstr /C:"STOPPED" >nul && goto service_stopped
    timeout /t 1 /nobreak >nul
)
echo The service did not stop. Try again after rebooting.
pause
exit /b 1
:service_stopped
sc.exe delete RGBControlService
if errorlevel 1 (
    echo Could not remove the service.
    pause
    exit /b 1
)
del /Q "%ProgramFiles%\RGBControl\RGBControl.exe" >nul 2>&1
rmdir "%ProgramFiles%\RGBControl" >nul 2>&1
del /Q "%ProgramData%\Microsoft\Windows\Start Menu\Programs\RGB Control.lnk" >nul 2>&1
echo Service removed. Your theme settings remain in %%ProgramData%%\RGBControl.
pause
exit /b 0
