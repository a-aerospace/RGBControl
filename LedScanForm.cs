using System;
using System.Drawing;
using System.Windows.Forms;

namespace RGBControl
{
    internal sealed class LedScanForm : Form
    {
        private readonly HidRgbController controller;
        private readonly ComboBox header = new ComboBox();
        private readonly NumericUpDown scanLimit = new NumericUpDown();
        private readonly NumericUpDown address = new NumericUpDown();
        private readonly NumericUpDown lastVisible = new NumericUpDown();
        private readonly Button auto = new Button();
        private readonly Label status = new Label();
        private readonly Timer timer = new Timer();
        private ThemeSettings resumeSettings;
        private bool wasServiceRunning;
        private bool active;

        internal LedScanForm(HidRgbController connectedController)
        {
            controller = connectedController;
            Text = "Find addressable LEDs";
            ClientSize = new Size(465, 345);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            Label instructions = new Label();
            instructions.SetBounds(20, 15, 425, 40);
            instructions.Text = "Light one LED address at a time. When an address stays dark, the previous visible number is your likely count.";
            Controls.Add(instructions);

            AddLabel("ARGB header", 20, 65, 125);
            header.SetBounds(20, 90, 125, 25);
            header.DropDownStyle = ComboBoxStyle.DropDownList;
            header.Items.AddRange(new object[] { "D_LED 1", "D_LED 2" });
            Controls.Add(header);

            AddLabel("Scan up to", 165, 65, 110);
            scanLimit.SetBounds(165, 90, 105, 25);
            scanLimit.Minimum = 1;
            scanLimit.Maximum = 256;
            Controls.Add(scanLimit);

            AddLabel("LED address", 295, 65, 135);
            address.SetBounds(295, 90, 135, 25);
            address.Minimum = 1;
            address.Maximum = 256;
            address.ValueChanged += delegate { if (active) ShowCurrent(); };
            Controls.Add(address);
            scanLimit.ValueChanged += delegate
            {
                decimal previousAddress = address.Value;
                address.Maximum = scanLimit.Value;
                if (active && address.Value == previousAddress) ShowCurrent();
            };

            Button start = new Button();
            start.SetBounds(20, 135, 95, 32);
            start.Text = "Start";
            start.Click += delegate { StartScan(); };
            Controls.Add(start);

            Button previous = new Button();
            previous.SetBounds(125, 135, 95, 32);
            previous.Text = "Previous";
            previous.Click += delegate { PreviousLed(); };
            Controls.Add(previous);

            Button next = new Button();
            next.SetBounds(230, 135, 95, 32);
            next.Text = "Next";
            next.Click += delegate { NextLed(); };
            Controls.Add(next);

            auto.SetBounds(335, 135, 110, 32);
            auto.Text = "Auto scan";
            auto.Click += delegate { ToggleAuto(); };
            Controls.Add(auto);

            status.SetBounds(20, 185, 425, 50);
            status.Text = "Press Start. Watch for the first address that does not light a new LED.";
            Controls.Add(status);

            AddLabel("Last visible LED number", 20, 245, 180);
            lastVisible.SetBounds(20, 270, 95, 25);
            lastVisible.Minimum = 1;
            lastVisible.Maximum = 256;
            Controls.Add(lastVisible);

            Button saveCount = new Button();
            saveCount.SetBounds(130, 267, 155, 30);
            saveCount.Text = "Save count for themes";
            saveCount.Click += delegate { SaveCount(); };
            Controls.Add(saveCount);

            Button stop = new Button();
            stop.SetBounds(345, 267, 100, 30);
            stop.Text = "Stop scan";
            stop.Click += delegate { StopScan(); };
            Controls.Add(stop);

            Label wiringNote = new Label();
            wiringNote.SetBounds(20, 310, 425, 30);
            wiringNote.Text = "A splitter may make the same LED address light on several fans.";
            Controls.Add(wiringNote);

            timer.Interval = 700;
            timer.Tick += delegate { NextLed(); };
            try
            {
                ThemeSettings settings = ThemeSettings.Load();
                header.SelectedIndex = settings.Header;
                scanLimit.Value = Math.Max(32, settings.LedCount);
                lastVisible.Value = settings.LedCount;
            }
            catch
            {
                header.SelectedIndex = 0;
                scanLimit.Value = 32;
                lastVisible.Value = 12;
            }
        }

        private void AddLabel(string value, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = value;
            label.SetBounds(x, y, width, 20);
            Controls.Add(label);
        }

        private void StartScan()
        {
            try
            {
                if (!active)
                {
                    wasServiceRunning = ThemeSettings.IsServiceRunning();
                    if (wasServiceRunning) resumeSettings = ThemeSettings.Load();
                    ThemeSettings.PauseServiceForManual();
                    active = true;
                    header.Enabled = false;
                }
                if (address.Value == 1) ShowCurrent();
                else address.Value = 1;
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        private void ShowCurrent()
        {
            if (!active) return;
            try
            {
                int count = (int)scanLimit.Value;
                Color[] pixels = new Color[count];
                for (int i = 0; i < count; i++) pixels[i] = Color.Black;
                pixels[(int)address.Value - 1] = Color.White;
                controller.SetPixels(header.SelectedIndex, pixels, "Auto", 100);
                status.Text = "Showing LED " + address.Value + " of " + scanLimit.Value +
                    ". If it stays dark, the last lit address is your count.";
            }
            catch (Exception error)
            {
                timer.Stop();
                auto.Text = "Auto scan";
                status.Text = error.Message;
            }
        }

        private void PreviousLed()
        {
            if (!active) { StartScan(); return; }
            if (address.Value > 1) address.Value--;
        }

        private void NextLed()
        {
            if (!active) { StartScan(); return; }
            if (address.Value < scanLimit.Value) address.Value++;
            else
            {
                timer.Stop();
                auto.Text = "Auto scan";
                status.Text = "Reached the scan limit. Increase it if the last LED still lit.";
            }
        }

        private void ToggleAuto()
        {
            if (timer.Enabled)
            {
                timer.Stop();
                auto.Text = "Auto scan";
                return;
            }
            if (!active) StartScan();
            if (!active) return;
            timer.Start();
            auto.Text = "Pause auto";
        }

        private void SaveCount()
        {
            try
            {
                ThemeSettings settings = ThemeSettings.Load();
                settings.LedCount = (int)lastVisible.Value;
                settings.Save();
                if (resumeSettings != null) resumeSettings.LedCount = settings.LedCount;
                status.Text = "Saved " + settings.LedCount + " LED positions for dynamic themes.";
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        private void StopScan()
        {
            timer.Stop();
            auto.Text = "Auto scan";
            if (!active) return;
            try
            {
                Color[] pixels = new Color[(int)scanLimit.Value];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.Black;
                controller.SetPixels(header.SelectedIndex, pixels, "Auto", 100);
            }
            catch (Exception error) { status.Text = error.Message; }
            finally
            {
                active = false;
                header.Enabled = true;
                if (wasServiceRunning && resumeSettings != null)
                {
                    try
                    {
                        if (ThemeSettings.IsServiceRunning() && ThemeSettings.Load().Theme == "Manual")
                            resumeSettings.Save();
                    }
                    catch (Exception error) { status.Text = error.Message; }
                }
                wasServiceRunning = false;
                resumeSettings = null;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopScan();
            timer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
