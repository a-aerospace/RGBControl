# RGBControl

<img src="assets/RGBControl.svg" alt="RGBControl rainbow fan icon" width="96">

RGBControl controls lighting on compatible Gigabyte motherboard USB RGB controllers. The Windows app can set static colors, address individual LEDs on D_LED headers, and play moving themes without OpenRGB, RGB Fusion, Python, a third-party driver, or an account. It makes no network requests.

The app has been tested on a Gigabyte B760M Gaming Plus WiFi DDR4 motherboard with an `IT5701-GIGABYTE` controller (USB hardware ID `048D:5702`) and a Thermalright Aqua Elite 360 V3 ARGB cooler. Other hardware is untested. RGBControl connects only to a controller reporting the expected hardware ID and identity.

## Install on Windows

1. From [Releases](https://github.com/a-aerospace/RGBControl/releases/latest), download `RGBControl-Setup-0.2.4.exe` and run it. This one file contains the app and installer; it does not need an extracted ZIP or separate script.
2. Read the installer page explaining what RGBControl does and what it can access.
3. Keep **Install the background service** checked for themes at Windows startup and lighting control during sleep, hibernation, and shutdown. Uncheck it for **app only**: themes play while the RGB Control window is open, with no background service. The separate **Create a desktop shortcut** box is optional and starts unchecked.
4. Open **RGB Control** from the Start menu. Choose a theme in **Dynamic themes...** and click **Save and apply**.

The installer puts the executable in `%ProgramFiles%\RGBControl`, creates a Start menu shortcut, and stores theme settings and service status in `%ProgramData%\RGBControl`. If selected, the service runs as Windows LocalSystem so it can control lighting before sign-in. The installer uses administrator access to write to Program Files and register or remove the service. It appears in **Windows Settings → Apps → Installed apps** as **RGBControl**, where it can be uninstalled. Theme settings are retained after uninstall so a later installation can reuse them.

RGBControl reads the compatible USB lighting controller's identity and color calibration and sends lighting commands to its connected RGB headers. It can turn those lights on or off. It does not control fan or pump speed, use the internet, collect telemetry, or require an account. The release is unsigned, so Windows may show an unknown publisher notice. Windows needs .NET Framework 4.x to run the app.

## Use

- **Dynamic themes:** Rainbow, Comet, Ocean, and Twinkle. Choose a color, speed, brightness, ARGB header, and LED count. With the service, themes continue after closing the app and resume after wake. In app-only mode they play while the app window is open. The animation targets 60 frames per second; actual speed depends on the USB controller and the number of LEDs.
- **Two-color Comet:** In **Dynamic themes...**, select **Comet**, check **Use a second color for the Comet trail**, and pick the trail color. The head uses the theme color; the tail blends toward the second color as it fades. Leave the box unchecked for the original single-color effect.
- **Static color:** Click **Connect**, select a lighting channel or **All channels**, pick a color, and click **Apply color**. Manual controls pause a running theme until you save a dynamic theme again.
- **Six-color pattern:** Pick six colors and click **Apply six colors** on D_LED 1 or D_LED 2. The colors can repeat or form six sections.
- **Find LED count:** Click **Find LED count...** to light one address at a time. Save the last visible address as the LED count for themes. A splitter can make several fans show the same address, so this finds independently controllable positions on the signal rather than every physical LED.

The service writes its latest status, measured frame rate, and longest recent frame gap to `%ProgramData%\RGBControl\service-status.txt`. This confirms USB reports were sent; it cannot confirm what the LEDs visibly display. In app-only mode, no service status file is updated.

The cooler's lighting connector must use a compatible 3-pin 5V D_LED header. The [Gigabyte motherboard manual](https://download.gigabyte.com/FileList/Manual/mb_manual_b760m-series_1004_e.pdf) distinguishes this from a 12V LED_C header. Fan power is separate.

## Build from source

On Windows, run `build_windows.bat` to compile `RGBControl.exe` using the .NET Framework C# compiler. Install [Inno Setup 6](https://jrsoftware.org/inno-setup/downloads) and compile `RGBControl.iss` with `ISCC.exe` to make the single-file installer. The GitHub Actions workflow builds both on a Windows runner, verifies service and app-only installation, and publishes `RGBControl-Setup-0.2.4.exe` as the release asset. The local `RGBControl.exe` and generated installer are ignored by Git. The icon source is in `assets/RGBControl.svg`; `python3 tools/generate_icon.py` regenerates the `.ico` and preview PNG using only the Python standard library.

Legacy `install_service.bat` and `uninstall_service.bat` remain in source for existing users of the previous ZIP release. New users should use the installer executable.

## Protocol and license

The USB report format follows [Gigabyte RGB Fusion 2 reverse-engineering notes](https://github.com/aynX-skii/GigabyteRGBController/blob/main/docs/gcc-reverse-engineering.md) and the [documented per-LED packet structure](https://gitlab.com/CalcProgrammer1/OpenRGB/-/raw/master/Controllers/GigabyteRGBFusion2USBController/GigabyteRGBFusion2USBController.cpp). These are protocol references; RGBControl does not depend on OpenRGB at runtime. Device discovery uses the [Windows HID APIs](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/finding-and-opening-a-hid-collection).

Source code is available under the [MIT License](LICENSE).
