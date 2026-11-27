# RGBControl

RGBControl is a Windows desktop app and background service for the Gigabyte RGB Fusion 2 USB lighting controller. It controls static colors and individual LEDs on D_LED addressable headers without OpenRGB, RGB Fusion, Python, a third-party driver, or an account. It does not send network requests.

The app has been tested on a Gigabyte B760M Gaming Plus WiFi DDR4 motherboard with an `IT5701-GIGABYTE` controller (USB hardware ID `048D:5702`) and a Thermalright Aqua Elite 360 V3 ARGB cooler. Other boards, controllers, and coolers are untested. The app connects only to a controller that reports the expected hardware ID and identity.

## Install on Windows

1. Open this repository's **Releases** page and download `RGBControl-Windows.zip` from the latest release. Download the ZIP asset, not GitHub's source-code ZIP.
2. Extract the entire ZIP to a local folder.
3. Double-click `install_service.bat` in the extracted `RGBControl` folder and accept the Windows administrator prompt.
4. Open **RGB Control** from the Start menu. Click **Dynamic themes...**, choose a theme, and click **Save and apply**.

The installer copies one `RGBControl.exe` to Program Files and registers `RGBControlService` to start with Windows. Windows needs .NET Framework 4.x to run the app. No Python or OpenRGB installation is needed. The release is unsigned, so Windows may identify the publisher as unknown.

To update, download and extract a newer release, then run its `install_service.bat` again. To remove the app and service, double-click `uninstall_service.bat` from an extracted release. Theme settings remain in `%ProgramData%\RGBControl` so a later installation can reuse them.

## Use

- **Dynamic themes:** Rainbow, Comet, Ocean, and Twinkle. Choose a color, speed, brightness, ARGB header, and LED count. The service applies the saved theme at startup and after wake.
- **Static color:** Click **Connect**, choose a motherboard lighting channel or **All channels**, pick a color, and click **Apply color**. Manual controls pause the service's animation until you save a dynamic theme again.
- **Six-color pattern:** Pick six colors and click **Apply six colors** on D_LED 1 or D_LED 2. The colors can repeat or form six sections.
- **Find LED count:** Click **Find LED count...** to light one address at a time. Save the last visible address as the theme's LED count. A splitter can make several fans show the same address, so this finds independently controllable positions on the signal rather than every physical LED.
- **Power events:** The service sends an off command before shutdown, sleep, or hibernation and reconnects after wake. Hardware can reset to its own effect during a power transition; the service retries its connection after wake.

The service writes its latest status to `%ProgramData%\RGBControl\service-status.txt` and stores theme settings in `%ProgramData%\RGBControl\theme.ini`. The status confirms that USB reports were sent; it cannot confirm what the LEDs visibly display.

The cooler's lighting connector must use a compatible 3-pin 5V D_LED header. The [Gigabyte motherboard manual](https://download.gigabyte.com/FileList/Manual/mb_manual_b760m-series_1004_e.pdf) distinguishes this from a 12V LED_C header. Fan power is separate. RGBControl controls lighting, not fan or pump speed.

## Build from source

On Windows, clone or download the source and double-click `build_windows.bat`. It uses the .NET Framework C# compiler included with Windows to build `RGBControl.exe` from the three C# source files. Run `install_service.bat` to build and install the service from source. `run_windows.bat` builds and launches the desktop app without installing the service.

A push to `main` checks that the Windows build succeeds and publishes the first `v0.1.0` release if it does not exist yet. Later version tags build a fresh executable and publish `RGBControl-Windows.zip` through GitHub Actions. The local `RGBControl.exe` is ignored by Git and is never used for the release build.

## Protocol and license

The USB report format follows [Gigabyte RGB Fusion 2 reverse-engineering notes](https://github.com/aynX-skii/GigabyteRGBController/blob/main/docs/gcc-reverse-engineering.md) and the [documented per-LED packet structure](https://gitlab.com/CalcProgrammer1/OpenRGB/-/raw/master/Controllers/GigabyteRGBFusion2USBController/GigabyteRGBFusion2USBController.cpp). These are protocol references; RGBControl does not use OpenRGB code or libraries at runtime. Device discovery uses the [Windows HID APIs](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/finding-and-opening-a-hid-collection).

Source code is available under the [MIT License](LICENSE).
