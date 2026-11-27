@echo off
setlocal
fltmc >nul 2>&1
if errorlevel 1 (
    set "RGB_CONTROL_INSTALLER=%~f0"
    powershell.exe -NoProfile -Command "try { Start-Process -FilePath $env:RGB_CONTROL_INSTALLER -Verb RunAs -ErrorAction Stop } catch { Write-Error $_; exit 1 }"
    exit /b
)
pushd "%~dp0"
if errorlevel 1 goto failed
if exist "RGBControl.cs" (
    call build_windows.bat
    if errorlevel 1 goto failed
)
if not exist "RGBControl.exe" (
    echo RGBControl.exe is missing. Download the Windows release ZIP or build from source.
    goto failed
)

set "APP_DIR=%ProgramFiles%\RGBControl"
set "SETTINGS_DIR=%ProgramData%\RGBControl"
if not exist "%APP_DIR%" mkdir "%APP_DIR%"
if errorlevel 1 goto failed
if not exist "%SETTINGS_DIR%" mkdir "%SETTINGS_DIR%"
if errorlevel 1 goto failed
icacls "%SETTINGS_DIR%" /grant "*S-1-5-32-545:(OI)(CI)M" >nul
if errorlevel 1 goto failed

sc.exe query RGBControlService >nul 2>&1
if not errorlevel 1 (
    sc.exe stop RGBControlService >nul 2>&1
    for /L %%I in (1,1,15) do (
        sc.exe query RGBControlService | findstr /C:"STOPPED" >nul && goto service_stopped
        timeout /t 1 /nobreak >nul
    )
    echo Existing service did not stop. Try again after rebooting.
    goto failed
)
:service_stopped
copy /Y "RGBControl.exe" "%APP_DIR%\RGBControl.exe" >nul
if errorlevel 1 goto failed

sc.exe query RGBControlService >nul 2>&1
if errorlevel 1 (
    sc.exe create RGBControlService binPath= "\"%APP_DIR%\RGBControl.exe\" --service" start= auto DisplayName= "RGB Control Lighting"
) else (
    sc.exe config RGBControlService binPath= "\"%APP_DIR%\RGBControl.exe\" --service" start= auto DisplayName= "RGB Control Lighting"
)
if errorlevel 1 goto failed
sc.exe failure RGBControlService reset= 86400 actions= restart/5000/restart/5000/restart/5000 >nul
sc.exe start RGBControlService
if errorlevel 1 goto failed
powershell.exe -NoProfile -Command "$link = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\RGB Control.lnk')); $link.TargetPath = (Join-Path $env:ProgramFiles 'RGBControl\RGBControl.exe'); $link.Save()"
if errorlevel 1 goto failed
echo RGB Control installed and started. Open RGB Control from the Start menu.
popd
pause
exit /b 0

:failed
echo Installation failed. Check the message above.
popd
pause
exit /b 1
