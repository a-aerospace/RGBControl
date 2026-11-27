@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 goto failed
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto failed
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:RGBControl.exe /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll RGBControl.cs ThemeService.cs LedScanForm.cs
if errorlevel 1 goto failed
echo Built RGBControl.exe
popd
exit /b 0

:failed
echo Could not build RGBControl.exe using the Windows .NET Framework compiler.
if not defined CI pause
popd
exit /b 1
