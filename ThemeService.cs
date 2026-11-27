using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RGBControl
{
    internal sealed class ThemeSettings
    {
        internal const string ServiceName = "RGBControlService";
        internal static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RGBControl");
        internal static readonly string PathName = Path.Combine(Folder, "theme.ini");
        internal static readonly string StatusPath = Path.Combine(Folder, "service-status.txt");
        internal string Theme = "Rainbow";
        internal int LedCount = 12;
        internal int Header = 0;
        internal int Speed = 5;
        internal int Brightness = 80;
        internal Color ThemeColor = Color.FromArgb(80, 210, 255);
        internal bool UseSecondCometColor;
        internal Color CometTrailColor = Color.FromArgb(255, 80, 180);

        internal static ThemeSettings Load()
        {
            ThemeSettings settings = new ThemeSettings();
            if (!File.Exists(PathName)) return settings;
            foreach (string line in File.ReadAllLines(PathName))
            {
                int split = line.IndexOf('=');
                if (split < 1) continue;
                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1).Trim();
                int number;
                if (key == "Theme" && ValidTheme(value)) settings.Theme = value;
                else if (key == "LedCount" && int.TryParse(value, out number) && number >= 1 && number <= 256)
                    settings.LedCount = number;
                else if (key == "Header" && int.TryParse(value, out number) && number >= 0 && number <= 1)
                    settings.Header = number;
                else if (key == "Speed" && int.TryParse(value, out number) && number >= 1 && number <= 10)
                    settings.Speed = number;
                else if (key == "Brightness" && int.TryParse(value, out number) && number >= 0 && number <= 100)
                    settings.Brightness = number;
                else if (key == "Color" && value.Length == 7 && value[0] == '#' &&
                    int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number))
                    settings.ThemeColor = Color.FromArgb((number >> 16) & 255, (number >> 8) & 255, number & 255);
                else if (key == "CometTwoColors") settings.UseSecondCometColor = value == "1";
                else if (key == "CometTrailColor" && value.Length == 7 && value[0] == '#' &&
                    int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number))
                    settings.CometTrailColor = Color.FromArgb((number >> 16) & 255, (number >> 8) & 255, number & 255);
            }
            return settings;
        }

        private static bool ValidTheme(string value)
        {
            return value == "Rainbow" || value == "Comet" || value == "Ocean" ||
                value == "Twinkle" || value == "Manual" || value == "Off";
        }

        internal void Save()
        {
            Directory.CreateDirectory(Folder);
            string temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string content = "Theme=" + Theme + "\r\nLedCount=" + LedCount.ToString(CultureInfo.InvariantCulture) +
                "\r\nHeader=" + Header.ToString(CultureInfo.InvariantCulture) +
                "\r\nSpeed=" + Speed.ToString(CultureInfo.InvariantCulture) +
                "\r\nBrightness=" + Brightness.ToString(CultureInfo.InvariantCulture) +
                "\r\nColor=" + string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}",
                    ThemeColor.R, ThemeColor.G, ThemeColor.B) +
                "\r\nCometTwoColors=" + (UseSecondCometColor ? "1" : "0") +
                "\r\nCometTrailColor=" + string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}",
                    CometTrailColor.R, CometTrailColor.G, CometTrailColor.B) + "\r\n";
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                if (File.Exists(PathName)) File.Replace(temporary, PathName, null);
                else File.Move(temporary, PathName);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        internal static bool IsServiceRunning()
        {
            try
            {
                using (ServiceController service = new ServiceController(ServiceName))
                    return service.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }

        internal static void PauseServiceForManual()
        {
            bool serviceWasRunning = IsServiceRunning();
            ThemeSettings settings = Load();
            settings.Theme = "Manual";
            settings.Save();
            if (serviceWasRunning) Thread.Sleep(350);
        }
    }

    internal sealed class FrameTimerResolution : IDisposable
    {
        // Windows timer precision is raised only while a dynamic theme is active.
        private bool active;
        private bool requested;

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);

        internal void Start()
        {
            if (requested) return;
            requested = true;
            active = TimeBeginPeriod(1) == 0;
        }

        internal void Stop()
        {
            if (active) TimeEndPeriod(1);
            active = false;
            requested = false;
        }

        public void Dispose() { Stop(); }
    }

    internal sealed class ThemeForm : Form
    {
        private readonly ComboBox theme = new ComboBox();
        private readonly ComboBox header = new ComboBox();
        private readonly NumericUpDown ledCount = new NumericUpDown();
        private readonly NumericUpDown speed = new NumericUpDown();
        private readonly NumericUpDown brightness = new NumericUpDown();
        private readonly Label status = new Label();
        private readonly Button colorButton = new Button();
        private readonly CheckBox cometTwoColors = new CheckBox();
        private readonly Button trailColorButton = new Button();
        private readonly Label trailHelp = new Label();
        private readonly Action settingsSaved;
        private Color selectedColor = Color.FromArgb(80, 210, 255);
        private Color selectedTrailColor = Color.FromArgb(255, 80, 180);

        internal ThemeForm(Action onSettingsSaved)
        {
            settingsSaved = onSettingsSaved;
            Text = "Dynamic lighting themes";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ClientSize = new Size(420, 485);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            AddLabel("Theme", 20, 20, 150);
            theme.SetBounds(20, 45, 380, 25);
            theme.DropDownStyle = ComboBoxStyle.DropDownList;
            theme.Items.AddRange(new object[] { "Rainbow", "Comet", "Ocean", "Twinkle", "Manual", "Off" });
            theme.SelectedIndexChanged += delegate { UpdateCometControls(); };
            Controls.Add(theme);

            AddLabel("ARGB header", 20, 85, 110);
            header.SetBounds(20, 108, 110, 25);
            header.DropDownStyle = ComboBoxStyle.DropDownList;
            header.Items.AddRange(new object[] { "D_LED 1", "D_LED 2" });
            Controls.Add(header);

            AddLabel("LEDs", 150, 85, 70);
            ledCount.SetBounds(150, 108, 70, 25);
            ledCount.Minimum = 1;
            ledCount.Maximum = 256;
            Controls.Add(ledCount);

            AddLabel("Speed", 240, 85, 70);
            speed.SetBounds(240, 108, 70, 25);
            speed.Minimum = 1;
            speed.Maximum = 10;
            Controls.Add(speed);

            AddLabel("Brightness", 330, 85, 80);
            brightness.SetBounds(330, 108, 70, 25);
            brightness.Minimum = 0;
            brightness.Maximum = 100;
            Controls.Add(brightness);

            Label details = new Label();
            details.SetBounds(20, 155, 380, 70);
            details.Text = "Rainbow: moving spectrum     Comet: moving light\r\n" +
                "Ocean: color waves               Twinkle: scattered sparks\r\n" +
                "Manual: use main window         Off: darken all lights";
            Controls.Add(details);

            AddLabel("Theme color", 20, 220, 150);
            colorButton.SetBounds(20, 245, 145, 34);
            colorButton.Text = "Pick color";
            colorButton.FlatStyle = FlatStyle.Flat;
            colorButton.Click += delegate { PickThemeColor(); };
            Controls.Add(colorButton);
            Label colorHelp = new Label();
            colorHelp.SetBounds(180, 245, 220, 40);
            colorHelp.Text = "Rainbow uses this as its starting hue; other themes use the color directly.";
            Controls.Add(colorHelp);

            cometTwoColors.SetBounds(20, 295, 380, 25);
            cometTwoColors.Text = "Use a second color for the Comet trail";
            cometTwoColors.CheckedChanged += delegate { UpdateCometControls(); };
            Controls.Add(cometTwoColors);
            trailColorButton.SetBounds(20, 325, 145, 34);
            trailColorButton.Text = "Pick trail color";
            trailColorButton.FlatStyle = FlatStyle.Flat;
            trailColorButton.Click += delegate { PickTrailColor(); };
            Controls.Add(trailColorButton);
            trailHelp.SetBounds(180, 325, 220, 40);
            trailHelp.Text = "The head uses the theme color; its tail fades through this color.";
            Controls.Add(trailHelp);

            Button save = new Button();
            save.SetBounds(20, 390, 165, 32);
            save.Text = "Save and apply";
            save.Click += delegate { SaveSettings(); };
            Controls.Add(save);
            status.SetBounds(20, 435, 380, 40);
            status.Text = ThemeSettings.IsServiceRunning()
                ? "Service is running. Changes apply within a second."
                : "App only: themes play while RGB Control is open.";
            Controls.Add(status);

            try
            {
                ThemeSettings current = ThemeSettings.Load();
                theme.SelectedItem = current.Theme;
                if (theme.SelectedIndex < 0) theme.SelectedIndex = 0;
                header.SelectedIndex = current.Header;
                ledCount.Value = current.LedCount;
                speed.Value = current.Speed;
                brightness.Value = current.Brightness;
                selectedColor = current.ThemeColor;
                selectedTrailColor = current.CometTrailColor;
                cometTwoColors.Checked = current.UseSecondCometColor;
            }
            catch (Exception error)
            {
                theme.SelectedIndex = 0;
                header.SelectedIndex = 0;
                ledCount.Value = 12;
                speed.Value = 5;
                brightness.Value = 80;
                status.Text = error.Message;
            }
            UpdateColorButton(colorButton, selectedColor);
            UpdateColorButton(trailColorButton, selectedTrailColor);
            UpdateCometControls();
        }

        private void AddLabel(string value, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = value;
            label.SetBounds(x, y, width, 20);
            Controls.Add(label);
        }

        private static void UpdateColorButton(Button button, Color color)
        {
            button.BackColor = color;
            button.ForeColor = (color.R * 299 + color.G * 587 + color.B * 114) / 1000 < 128
                ? Color.White : Color.Black;
        }

        private void UpdateCometControls()
        {
            bool cometSelected = theme.Text == "Comet";
            cometTwoColors.Enabled = cometSelected;
            trailColorButton.Enabled = cometSelected && cometTwoColors.Checked;
            trailHelp.Enabled = cometSelected;
        }

        private void PickThemeColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = selectedColor;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedColor = dialog.Color;
                    UpdateColorButton(colorButton, selectedColor);
                }
            }
        }

        private void PickTrailColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = selectedTrailColor;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedTrailColor = dialog.Color;
                    UpdateColorButton(trailColorButton, selectedTrailColor);
                }
            }
        }

        private void SaveSettings()
        {
            try
            {
                ThemeSettings settings = new ThemeSettings();
                settings.Theme = theme.Text;
                settings.Header = header.SelectedIndex;
                settings.LedCount = (int)ledCount.Value;
                settings.Speed = (int)speed.Value;
                settings.Brightness = (int)brightness.Value;
                settings.ThemeColor = selectedColor;
                settings.UseSecondCometColor = cometTwoColors.Checked;
                settings.CometTrailColor = selectedTrailColor;
                settings.Save();
                if (settingsSaved != null) settingsSaved();
                if (settings.Theme == "Manual")
                    status.Text = "Saved. Use the main window to control the lights.";
                else if (settings.Theme == "Off")
                    status.Text = "Saved. Lights will be turned off.";
                else
                    status.Text = ThemeSettings.IsServiceRunning()
                        ? "Saved. The service will apply this theme shortly."
                        : "Saved. The app will play this theme while it is open.";
            }
            catch (Exception error) { status.Text = error.Message; }
        }
    }

    internal sealed class RgbLightingService : ServiceBase
    {
        internal const int TargetFps = 60;
        private readonly object gate = new object();
        private Thread worker;
        private HidRgbController controller;
        private volatile bool running;
        private volatile bool suspended;
        private string lastStatus;
        private long nextRecoveryReopen;
        private int recoveryReopensRemaining;
        private int recoveryReopensCompleted;

        internal RgbLightingService()
        {
            ServiceName = ThemeSettings.ServiceName;
            CanStop = true;
            CanShutdown = true;
            CanHandlePowerEvent = true;
        }

        protected override void OnStart(string[] args)
        {
            running = true;
            suspended = false;
            ScheduleRecovery(false);
            WriteStatus("Service started; connecting to the RGB controller.");
            worker = new Thread(Animate);
            worker.IsBackground = true;
            worker.Name = "RGB animation";
            worker.Start();
        }

        protected override void OnStop()
        {
            StopAndDarken();
        }

        protected override void OnShutdown()
        {
            StopAndDarken();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (powerStatus == PowerBroadcastStatus.QuerySuspend ||
                powerStatus == PowerBroadcastStatus.Suspend)
            {
                suspended = true;
                bool darkened = DarkenAndClose();
                WriteStatus(darkened ? "Off command sent before suspend or hibernation." :
                    "Could not reach the RGB controller before suspend or hibernation.");
            }
            else if (powerStatus == PowerBroadcastStatus.QuerySuspendFailed ||
                powerStatus == PowerBroadcastStatus.ResumeAutomatic ||
                powerStatus == PowerBroadcastStatus.ResumeCritical ||
                powerStatus == PowerBroadcastStatus.ResumeSuspend)
            {
                ScheduleRecovery(true);
                suspended = false;
                WriteStatus("Wake notification received; reconnecting now and again after USB settles.");
            }
            return true;
        }

        private void StopAndDarken()
        {
            running = false;
            suspended = true;
            if (worker != null) worker.Join(1000);
            bool darkened = DarkenAndClose();
            WriteStatus(darkened ? "Off command sent before service stop or shutdown." :
                "Could not reach the RGB controller before service stop or shutdown.");
        }

        private bool DarkenAndClose()
        {
            lock (gate)
            {
                try
                {
                    if (controller == null) controller = HidRgbController.Open();
                    controller.SetColor(-1, Color.Black, 0);
                    return true;
                }
                catch { return false; }
                finally
                {
                    if (controller != null) controller.Dispose();
                    controller = null;
                }
            }
        }

        private void ReleaseController()
        {
            lock (gate)
            {
                if (controller != null) controller.Dispose();
                controller = null;
            }
        }

        private void ScheduleRecovery(bool afterWake)
        {
            lock (gate)
            {
                if (controller != null) controller.Dispose();
                controller = null;
                recoveryReopensRemaining = afterWake ? 2 : 0;
                recoveryReopensCompleted = 0;
                nextRecoveryReopen = afterWake
                    ? Stopwatch.GetTimestamp() + 3L * Stopwatch.Frequency : 0;
            }
        }

        private void Animate()
        {
            double animationSeconds = 0;
            bool sentOff = false;
            double cometPosition = 0;
            long lastTick = Stopwatch.GetTimestamp();
            long fpsWindowStart = lastTick;
            int framesInWindow = 0;
            double measuredFps = 0;
            double longestGapMilliseconds = 0;
            double measuredLongestGapMilliseconds = 0;
            long previousFrameSentAt = 0;
            ThemeSettings settings = null;
            long nextSettingsRead = 0;
            long nextDirectRefresh = 0;
            string lastDirectRefresh = "pending";
            string lastEffectReset = "pending";
            using (FrameTimerResolution timerResolution = new FrameTimerResolution())
            while (running)
            {
                if (suspended)
                {
                    timerResolution.Stop();
                    lastTick = Stopwatch.GetTimestamp();
                    fpsWindowStart = lastTick;
                    framesInWindow = 0;
                    measuredFps = 0;
                    longestGapMilliseconds = measuredLongestGapMilliseconds = 0;
                    previousFrameSentAt = 0;
                    Thread.Sleep(100);
                    continue;
                }

                if (settings == null || Stopwatch.GetTimestamp() >= nextSettingsRead)
                {
                    try
                    {
                        settings = ThemeSettings.Load();
                        nextSettingsRead = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 10;
                    }
                    catch (Exception error)
                    {
                        timerResolution.Stop();
                        WriteStatus("Could not read theme settings: " + error.Message);
                        Thread.Sleep(500);
                        continue;
                    }
                }

                if (settings.Theme == "Manual")
                {
                    timerResolution.Stop();
                    lastTick = Stopwatch.GetTimestamp();
                    fpsWindowStart = lastTick;
                    framesInWindow = 0;
                    measuredFps = 0;
                    longestGapMilliseconds = measuredLongestGapMilliseconds = 0;
                    previousFrameSentAt = 0;
                    ReleaseController();
                    WriteStatus("Manual mode. The desktop window controls the LEDs.");
                    sentOff = false;
                    Thread.Sleep(150);
                    continue;
                }
                if (settings.Theme == "Off")
                {
                    timerResolution.Stop();
                    lastTick = Stopwatch.GetTimestamp();
                    fpsWindowStart = lastTick;
                    framesInWindow = 0;
                    measuredFps = 0;
                    longestGapMilliseconds = measuredLongestGapMilliseconds = 0;
                    previousFrameSentAt = 0;
                    if (!sentOff)
                    {
                        sentOff = DarkenAndClose();
                        WriteStatus(sentOff ? "Off theme applied." : "Could not reach the RGB controller to turn off lights.");
                    }
                    Thread.Sleep(150);
                    continue;
                }
                sentOff = false;
                timerResolution.Start();

                try
                {
                    long now = Stopwatch.GetTimestamp();
                    double seconds = Math.Min(0.25, (now - lastTick) / (double)Stopwatch.Frequency);
                    lastTick = now;
                    animationSeconds += seconds;
                    if (settings.Theme == "Comet")
                        cometPosition = (cometPosition + seconds * (1.2 + settings.Speed * 0.7)) % settings.LedCount;
                    Color[] pixels = MakeFrame(settings, animationSeconds, cometPosition);
                    bool reassertDirectMode = now >= nextDirectRefresh;
                    bool sentFrame = false;
                    lock (gate)
                    {
                        if (!suspended && running)
                        {
                            if (recoveryReopensRemaining > 0 &&
                                Stopwatch.GetTimestamp() >= nextRecoveryReopen)
                            {
                                if (controller != null) controller.Dispose();
                                controller = null;
                                recoveryReopensRemaining--;
                                recoveryReopensCompleted++;
                                if (recoveryReopensRemaining > 0)
                                    nextRecoveryReopen = Stopwatch.GetTimestamp() + 7L * Stopwatch.Frequency;
                            }
                            bool openedController = controller == null;
                            if (openedController)
                            {
                                controller = HidRgbController.Open();
                                reassertDirectMode = true;
                            }
                            controller.SetPixels(settings.Header, pixels, "Auto", settings.Brightness,
                                reassertDirectMode);
                            sentFrame = true;
                            framesInWindow++;
                            long frameSentAt = Stopwatch.GetTimestamp();
                            if (previousFrameSentAt != 0)
                                longestGapMilliseconds = Math.Max(longestGapMilliseconds,
                                    (frameSentAt - previousFrameSentAt) * 1000.0 / Stopwatch.Frequency);
                            previousFrameSentAt = frameSentAt;
                            if (frameSentAt - fpsWindowStart >= Stopwatch.Frequency)
                            {
                                measuredFps = framesInWindow * (double)Stopwatch.Frequency /
                                    (frameSentAt - fpsWindowStart);
                                measuredLongestGapMilliseconds = longestGapMilliseconds;
                                longestGapMilliseconds = 0;
                                framesInWindow = 0;
                                fpsWindowStart = frameSentAt;
                            }
                            if (openedController)
                                lastEffectReset = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                            if (reassertDirectMode)
                            {
                                nextDirectRefresh = Stopwatch.GetTimestamp() + 3L * Stopwatch.Frequency;
                                lastDirectRefresh = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                            }
                            WriteStatus("Running " + settings.Theme + " on D_LED " + (settings.Header + 1) +
                                " with " + settings.LedCount + " LED positions. Controller effects cleared at " +
                                lastEffectReset + "; direct mode refreshed at " + lastDirectRefresh +
                                ". Sent " + measuredFps.ToString("F1", CultureInfo.InvariantCulture) + " fps" +
                                "; longest frame gap " +
                                measuredLongestGapMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms" +
                                ". Recovery reconnects " + recoveryReopensCompleted + "/" +
                                (recoveryReopensCompleted + recoveryReopensRemaining) + ". " +
                                "USB reports sent; LED output is not verified.");
                        }
                    }
                    if (!sentFrame) continue;
                    double frameMilliseconds = (Stopwatch.GetTimestamp() - now) * 1000.0 / Stopwatch.Frequency;
                    int waitMilliseconds = (int)Math.Ceiling(1000.0 / TargetFps - frameMilliseconds);
                    if (waitMilliseconds > 0) Thread.Sleep(waitMilliseconds);
                }
                catch (Exception error)
                {
                    timerResolution.Stop();
                    WriteStatus("Could not send RGB frame: " + error.Message);
                    ReleaseController();
                    Thread.Sleep(1000);
                }
            }
        }

        private void WriteStatus(string message)
        {
            if (message == lastStatus) return;
            lastStatus = message;
            try
            {
                Directory.CreateDirectory(ThemeSettings.Folder);
                File.WriteAllText(ThemeSettings.StatusPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + message);
            }
            catch { }
        }

        internal static Color[] MakeFrame(ThemeSettings settings, double animationSeconds, double cometPosition)
        {
            int count = settings.LedCount;
            Color[] pixels = new Color[count];
            int twinkleStep = (int)Math.Floor(animationSeconds * (0.5 + settings.Speed * 0.2));
            for (int i = 0; i < count; i++)
            {
                if (settings.Theme == "Rainbow")
                {
                    pixels[i] = Hsv((settings.ThemeColor.GetHue() + i * 360.0 / count +
                        animationSeconds * (12 + settings.Speed * 12)) % 360, 1, 1);
                }
                else if (settings.Theme == "Comet")
                {
                    double glow = 0;
                    double bestDistance = 0;
                    for (int wrap = -1; wrap <= 1; wrap++)
                    {
                        double distance = cometPosition - i + wrap * count;
                        double width = distance >= 0 ? 1.7 : 0.65;
                        double candidate = Math.Exp(-0.5 * Math.Pow(distance / width, 2));
                        if (candidate > glow)
                        {
                            glow = candidate;
                            bestDistance = distance;
                        }
                    }
                    Color cometColor = settings.UseSecondCometColor
                        ? BlendColors(settings.ThemeColor, settings.CometTrailColor,
                            Math.Min(1.0, Math.Max(0, bestDistance / 2.0)))
                        : settings.ThemeColor;
                    pixels[i] = ScaleColor(cometColor, 0.01 + 0.99 * glow);
                }
                else if (settings.Theme == "Ocean")
                {
                    double wave = (Math.Sin(i * 0.85 - animationSeconds * (0.4 + settings.Speed * 0.2)) + 1) / 2;
                    pixels[i] = ScaleColor(settings.ThemeColor, 0.15 + wave * 0.85);
                }
                else
                {
                    int seed = unchecked(i * 1103515245 + twinkleStep * 12345);
                    int spark = (seed ^ (seed >> 13)) & 15;
                    pixels[i] = ScaleColor(settings.ThemeColor,
                        spark == 0 ? 1.0 : spark == 1 ? 0.35 : 0.01);
                }
            }
            return pixels;
        }

        private static Color Hsv(double hue, double saturation, double value)
        {
            double chroma = value * saturation;
            double x = chroma * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            double r = 0, g = 0, b = 0;
            if (hue < 60) { r = chroma; g = x; }
            else if (hue < 120) { r = x; g = chroma; }
            else if (hue < 180) { g = chroma; b = x; }
            else if (hue < 240) { g = x; b = chroma; }
            else if (hue < 300) { r = x; b = chroma; }
            else { r = chroma; b = x; }
            double m = value - chroma;
            return Color.FromArgb((int)Math.Round((r + m) * 255),
                (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        private static Color ScaleColor(Color color, double level)
        {
            return Color.FromArgb((int)Math.Round(color.R * level),
                (int)Math.Round(color.G * level), (int)Math.Round(color.B * level));
        }

        private static Color BlendColors(Color first, Color second, double amount)
        {
            return Color.FromArgb(
                (int)Math.Round(first.R + (second.R - first.R) * amount),
                (int)Math.Round(first.G + (second.G - first.G) * amount),
                (int)Math.Round(first.B + (second.B - first.B) * amount));
        }
    }
}
