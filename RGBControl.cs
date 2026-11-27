using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace RGBControl
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--service")
            {
                ServiceBase.Run(new RgbLightingService());
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private HidRgbController controller;
        private readonly Label device = new Label();
        private readonly Label status = new Label();
        private readonly ComboBox channel = new ComboBox();
        private readonly TextBox hex = new TextBox();
        private readonly Panel preview = new Panel();
        private readonly TrackBar brightness = new TrackBar();
        private readonly Button connect = new Button();
        private readonly Button apply = new Button();
        private readonly Button off = new Button();
        private readonly Button[] patternButtons = new Button[6];
        private readonly Color[] patternColors = {
            Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.Cyan, Color.Magenta
        };
        private readonly NumericUpDown ledCount = new NumericUpDown();
        private readonly ComboBox strip = new ComboBox();
        private readonly ComboBox patternMode = new ComboBox();
        private readonly ComboBox colorOrder = new ComboBox();
        private readonly Button applyPattern = new Button();
        private readonly Button themes = new Button();
        private readonly Button findLeds = new Button();

        internal MainForm()
        {
            Text = "RGB Control — Gigabyte direct USB";
            ClientSize = new Size(560, 685);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            device.SetBounds(20, 20, 415, 25);
            device.Text = "No controller connected";
            Controls.Add(device);
            connect.SetBounds(445, 17, 95, 28);
            connect.Text = "Connect";
            connect.Click += delegate { Connect(); };
            Controls.Add(connect);

            AddLabel("Lighting channel", 20, 68, 170);
            channel.SetBounds(20, 90, 520, 25);
            channel.DropDownStyle = ComboBoxStyle.DropDownList;
            channel.Items.Add("All channels");
            for (int i = 1; i <= 8; i++) channel.Items.Add("Channel " + i);
            channel.SelectedIndex = 0;
            Controls.Add(channel);
            AddLabel("To isolate the cooler, try channels one at a time with a distinct color. " +
                     "The channel number depends on the board.", 20, 123, 520, 40);

            AddLabel("Color (#RRGGBB)", 20, 178, 125);
            hex.SetBounds(145, 174, 100, 26);
            hex.Text = "#3585FF";
            hex.TextChanged += delegate { UpdatePreview(); };
            Controls.Add(hex);
            preview.SetBounds(258, 175, 36, 25);
            preview.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(preview);
            Button pick = new Button();
            pick.SetBounds(310, 173, 110, 28);
            pick.Text = "Pick color";
            pick.Click += delegate { PickColor(); };
            Controls.Add(pick);

            string[] names = { "Red", "Orange", "Green", "Blue", "Purple", "White" };
            string[] colors = { "#FF3030", "#FF8C00", "#28D65C", "#3585FF", "#A95CFF", "#FFFFFF" };
            for (int i = 0; i < names.Length; i++)
            {
                Button preset = new Button();
                preset.SetBounds(20 + i * 88, 215, 82, 29);
                preset.Text = names[i];
                string chosen = colors[i];
                preset.Click += delegate { hex.Text = chosen; };
                Controls.Add(preset);
            }

            AddLabel("Brightness (both modes)", 20, 265, 130);
            brightness.SetBounds(165, 255, 330, 45);
            brightness.Minimum = 0;
            brightness.Maximum = 100;
            brightness.Value = 100;
            brightness.TickFrequency = 10;
            brightness.ValueChanged += delegate { UpdatePreview(); };
            Controls.Add(brightness);

            apply.SetBounds(20, 315, 120, 30);
            apply.Text = "Apply color";
            apply.Click += delegate { ApplyColor(); };
            Controls.Add(apply);
            off.SetBounds(150, 315, 100, 30);
            off.Text = "Turn off";
            off.Click += delegate { TurnOff(); };
            Controls.Add(off);
            AddLabel("Six colors on an addressable header", 20, 365, 400);
            for (int i = 0; i < patternButtons.Length; i++)
            {
                int position = i;
                Button button = new Button();
                button.SetBounds(20 + i * 88, 395, 82, 42);
                button.Text = "Color " + (i + 1);
                button.FlatStyle = FlatStyle.Flat;
                button.Click += delegate { PickPatternColor(position); };
                patternButtons[i] = button;
                Controls.Add(button);
                UpdatePatternButton(i);
            }
            AddLabel("Click each color to edit it.", 20, 442, 360);

            AddLabel("ARGB header", 20, 475, 145);
            strip.SetBounds(20, 497, 145, 25);
            strip.DropDownStyle = ComboBoxStyle.DropDownList;
            strip.Items.Add("D_LED 1");
            strip.Items.Add("D_LED 2");
            strip.SelectedIndex = 0;
            Controls.Add(strip);

            AddLabel("LEDs to fill", 185, 475, 110);
            ledCount.SetBounds(185, 497, 100, 25);
            ledCount.Minimum = 6;
            ledCount.Maximum = 256;
            ledCount.Value = 32;
            Controls.Add(ledCount);

            AddLabel("Color layout", 305, 475, 235);
            patternMode.SetBounds(305, 497, 235, 25);
            patternMode.DropDownStyle = ComboBoxStyle.DropDownList;
            patternMode.Items.Add("Repeat colors 1–6");
            patternMode.Items.Add("Six sections");
            patternMode.SelectedIndex = 0;
            Controls.Add(patternMode);

            AddLabel("LED color order", 20, 535, 135);
            colorOrder.SetBounds(155, 532, 130, 25);
            colorOrder.DropDownStyle = ComboBoxStyle.DropDownList;
            colorOrder.Items.AddRange(new object[] { "Auto", "RGB", "RBG", "GRB", "GBR", "BRG", "BGR" });
            colorOrder.SelectedIndex = 0;
            Controls.Add(colorOrder);
            AddLabel("Use Auto first. Change this if red/green/blue are swapped.", 20, 565, 520);

            applyPattern.SetBounds(20, 595, 150, 30);
            applyPattern.Text = "Apply six colors";
            applyPattern.Click += delegate { ApplyPattern(); };
            Controls.Add(applyPattern);
            themes.SetBounds(190, 595, 150, 30);
            themes.Text = "Dynamic themes...";
            themes.Click += delegate { using (ThemeForm form = new ThemeForm()) form.ShowDialog(this); };
            Controls.Add(themes);
            findLeds.SetBounds(350, 595, 180, 30);
            findLeds.Text = "Find LED count...";
            findLeds.Click += delegate { using (LedScanForm form = new LedScanForm(controller)) form.ShowDialog(this); };
            Controls.Add(findLeds);

            status.SetBounds(20, 635, 520, 45);
            status.Text = "Click Connect to find the motherboard RGB controller.";
            Controls.Add(status);
            SetConnected(false);
            UpdatePreview();
        }

        private void AddLabel(string text, int x, int y, int width, int height = 25)
        {
            Label label = new Label();
            label.SetBounds(x, y, width, height);
            label.Text = text;
            Controls.Add(label);
        }

        private void SetConnected(bool value)
        {
            apply.Enabled = value;
            off.Enabled = value;
            applyPattern.Enabled = value;
            findLeds.Enabled = value;
        }

        private static Color ParseColor(string text)
        {
            Match match = Regex.Match(text.Trim(), "^#?([0-9A-Fa-f]{6})$");
            if (!match.Success) throw new ArgumentException("Enter a six-digit color, such as #FF6600.");
            string value = match.Groups[1].Value;
            return Color.FromArgb(
                Convert.ToInt32(value.Substring(0, 2), 16),
                Convert.ToInt32(value.Substring(2, 2), 16),
                Convert.ToInt32(value.Substring(4, 2), 16));
        }

        private void UpdatePreview()
        {
            try
            {
                Color color = ParseColor(hex.Text);
                double level = brightness.Value / 100.0;
                preview.BackColor = Color.FromArgb(
                    (int)Math.Round(color.R * level),
                    (int)Math.Round(color.G * level),
                    (int)Math.Round(color.B * level));
            }
            catch (ArgumentException) { preview.BackColor = SystemColors.Control; }
        }

        private void PickColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                try { dialog.Color = ParseColor(hex.Text); }
                catch (ArgumentException) { dialog.Color = Color.RoyalBlue; }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    hex.Text = string.Format("#{0:X2}{1:X2}{2:X2}",
                        dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
        }

        private void UpdatePatternButton(int position)
        {
            Color color = patternColors[position];
            patternButtons[position].BackColor = color;
            patternButtons[position].ForeColor = (color.R * 299 + color.G * 587 + color.B * 114) / 1000 < 128
                ? Color.White : Color.Black;
        }

        private void PickPatternColor(int position)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = patternColors[position];
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    patternColors[position] = dialog.Color;
                    UpdatePatternButton(position);
                }
            }
        }

        private void Connect()
        {
            connect.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                if (controller != null) controller.Dispose();
                controller = HidRgbController.Open();
                device.Text = controller.DeviceName;
                status.Text = "Connected. Apply a single color or a six-color ARGB pattern.";
                SetConnected(true);
            }
            catch (Exception error)
            {
                controller = null;
                device.Text = "No controller connected";
                status.Text = error.Message;
                SetConnected(false);
            }
            finally
            {
                connect.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyColor()
        {
            try
            {
                ThemeSettings.PauseServiceForManual();
                controller.SetColor(channel.SelectedIndex - 1, ParseColor(hex.Text), brightness.Value);
                status.Text = "Color command sent. Check the selected lights.";
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        private void TurnOff()
        {
            try
            {
                ThemeSettings.PauseServiceForManual();
                controller.SetColor(channel.SelectedIndex - 1, Color.Black, 0);
                status.Text = "Off command sent.";
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        private void ApplyPattern()
        {
            try
            {
                ThemeSettings.PauseServiceForManual();
                controller.SetPattern(strip.SelectedIndex, patternColors, (int)ledCount.Value,
                    patternMode.SelectedIndex == 1, colorOrder.Text, brightness.Value);
                status.Text = "Six-color command sent. Check the cooler LEDs.";
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (controller != null) controller.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal sealed class HidRgbController : IDisposable
    {
        private const ushort VendorId = 0x048D;
        private const ushort ProductId = 0x5702;
        private const ushort UsagePage = 0xFF89;
        private const ushort Usage = 0x00CC;
        private const int ReportId = 0xCC;
        private IntPtr handle;
        private readonly int reportLength;
        private readonly byte supportFlags;
        private readonly byte[] stripCountBytes = new byte[3];
        private readonly byte[][] stripCalibration = new byte[2][];
        private bool ownsLighting;
        private byte disabledStripEffects;
        private bool directOutputInitialized;
        internal readonly string DeviceName;

        private HidRgbController(IntPtr deviceHandle, int length, string name, byte flags, byte[] info)
        {
            handle = deviceHandle;
            reportLength = length;
            DeviceName = name;
            supportFlags = flags;
            Array.Copy(info, 8, stripCountBytes, 0, 3);
            stripCalibration[0] = new byte[] { info[44], info[45], info[46] };
            stripCalibration[1] = new byte[] { info[48], info[49], info[50] };
        }

        internal static HidRgbController Open()
        {
            Guid guid;
            Native.HidD_GetHidGuid(out guid);
            IntPtr devices = Native.SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (devices == Native.InvalidHandle) throw new System.ComponentModel.Win32Exception();
            try
            {
                for (uint index = 0; ; index++)
                {
                    Native.InterfaceData data = new Native.InterfaceData();
                    data.Size = Marshal.SizeOf(typeof(Native.InterfaceData));
                    if (!Native.SetupDiEnumDeviceInterfaces(devices, IntPtr.Zero, ref guid, index, ref data))
                    {
                        if (Marshal.GetLastWin32Error() == 259) break;
                        throw new System.ComponentModel.Win32Exception();
                    }
                    uint needed;
                    Native.SetupDiGetDeviceInterfaceDetail(devices, ref data, IntPtr.Zero,
                        0, out needed, IntPtr.Zero);
                    if (needed < 8) continue;
                    IntPtr detail = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!Native.SetupDiGetDeviceInterfaceDetail(devices, ref data, detail,
                            needed, out needed, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (string.IsNullOrEmpty(path)) continue;
                        IntPtr probe = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                        if (probe == Native.InvalidHandle) continue;
                        int length = 0;
                        bool matches = false;
                        try
                        {
                            Native.Attributes attrs = new Native.Attributes();
                            attrs.Size = Marshal.SizeOf(typeof(Native.Attributes));
                            if (!Native.HidD_GetAttributes(probe, ref attrs)) continue;
                            if (attrs.VendorId != VendorId || attrs.ProductId != ProductId) continue;
                            IntPtr preparsed;
                            if (!Native.HidD_GetPreparsedData(probe, out preparsed)) continue;
                            try
                            {
                                IntPtr caps = Marshal.AllocHGlobal(64);
                                try
                                {
                                    if (Native.HidP_GetCaps(preparsed, caps) < 0) continue;
                                    ushort usage = (ushort)Marshal.ReadInt16(caps, 0);
                                    ushort page = (ushort)Marshal.ReadInt16(caps, 2);
                                    length = (ushort)Marshal.ReadInt16(caps, 8);
                                    matches = usage == Usage && page == UsagePage;
                                }
                                finally { Marshal.FreeHGlobal(caps); }
                            }
                            finally { Native.HidD_FreePreparsedData(preparsed); }
                        }
                        finally { Native.CloseHandle(probe); }
                        if (!matches) continue;
                        if (length < 64) throw new InvalidOperationException("RGB controller reports are shorter than expected.");
                        IntPtr writable = Native.CreateFile(path, 0xC0000000, 3,
                            IntPtr.Zero, 3, 0, IntPtr.Zero);
                        if (writable == Native.InvalidHandle)
                            throw new UnauthorizedAccessException("RGB controller found, but Windows refused write access. Close other lighting software.");
                        try
                        {
                            byte[] request = new byte[length];
                            request[0] = ReportId;
                            request[1] = 0x60;
                            if (!Native.HidD_SetFeature(writable, request, request.Length))
                                throw new System.ComponentModel.Win32Exception();
                            Thread.Sleep(20);
                            byte[] response = new byte[length];
                            response[0] = ReportId;
                            if (!Native.HidD_GetFeature(writable, response, response.Length))
                                throw new System.ComponentModel.Win32Exception();
                            if (response[1] != 1)
                                throw new InvalidOperationException("The controller returned an unexpected info report.");
                            string name = Encoding.ASCII.GetString(response, 12, 28).Split('\0')[0];
                            if (!name.StartsWith("IT5701") && !name.StartsWith("IT5702"))
                                throw new InvalidOperationException("The device did not identify as an IT5701/5702 controller.");
                            string firmware = string.Format("{0}.{1}.{2}.{3}",
                                response[4], response[5], response[6], response[7]);
                            return new HidRgbController(writable, length,
                                name + " (firmware " + firmware + ")", response[11], response);
                        }
                        catch { Native.CloseHandle(writable); throw; }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { Native.SetupDiDestroyDeviceInfoList(devices); }
            throw new System.IO.FileNotFoundException(
                "No ITE 048D:5702 RGB controller was found on this Windows PC.");
        }

        private void Send(byte[] report)
        {
            if (handle == Native.InvalidHandle) throw new ObjectDisposedException("RGB controller");
            byte[] buffer = new byte[reportLength];
            Array.Copy(report, buffer, report.Length);
            if (!Native.HidD_SetFeature(handle, buffer, buffer.Length))
                throw new System.ComponentModel.Win32Exception();
            Thread.Sleep(20);
        }

        private void TakeLightingControl(bool force)
        {
            if (force || !ownsLighting)
            {
                if ((supportFlags & 1) != 0) Send(new byte[] { ReportId, 0x47, 0 });
                if ((supportFlags & 2) != 0) Send(new byte[] { ReportId, 0x48, 0 });
                ownsLighting = true;
            }
        }

        private void InitializeDirectOutput()
        {
            // A resumed controller can accept HID reports while still running its
            // built-in effect. Clear the staged zone effects before selecting direct LEDs.
            TakeLightingControl(true);
            for (byte command = 0x20; command <= 0x27; command++)
                Send(new byte[] { ReportId, command, 0 });
            Send(new byte[] { ReportId, 0x28, 0xFF });
            Thread.Sleep(100);
            disabledStripEffects = 0;
            directOutputInitialized = true;
        }

        internal void SetColor(int channel, Color color, int brightness)
        {
            if (channel < -1 || channel > 7) throw new ArgumentOutOfRangeException("channel");
            if (brightness < 0 || brightness > 100) throw new ArgumentOutOfRangeException("brightness");
            directOutputInitialized = false;
            TakeLightingControl(false);
            if (disabledStripEffects != 0)
            {
                Send(new byte[] { ReportId, 0x32, 0 });
                disabledStripEffects = 0;
            }
            int first = channel == -1 ? 0 : channel;
            int last = channel == -1 ? 7 : channel;
            for (int zone = first; zone <= last; zone++)
            {
                byte[] stage = new byte[64];
                stage[0] = ReportId;
                stage[1] = (byte)(0x20 + zone);
                stage[2] = (byte)(1 << zone);
                stage[11] = 1;
                stage[12] = (byte)Math.Round(brightness * 90.0 / 100);
                stage[14] = color.B;
                stage[15] = color.G;
                stage[16] = color.R;
                Send(stage);
            }
            Send(new byte[] { ReportId, 0x28, 0xFF });
        }

        private static int CapacityCode(int count)
        {
            if (count <= 32) return 0;
            if (count <= 64) return 1;
            return 2;
        }

        private string ResolveColorOrder(int stripIndex, string selected)
        {
            if (selected != "Auto") return selected;
            byte[] positions = stripCalibration[stripIndex];
            int blue = positions[0];
            int green = positions[1];
            int red = positions[2];
            if (red > 2 || green > 2 || blue > 2 || red == green || red == blue || green == blue)
                return "GRB";
            char[] order = new char[3];
            order[red] = 'R';
            order[green] = 'G';
            order[blue] = 'B';
            return new string(order);
        }

        internal void SetPattern(int stripIndex, Color[] colors, int count,
            bool sections, string selectedOrder, int brightness)
        {
            if (colors == null || colors.Length != 6) throw new ArgumentException("Six colors are required.");
            if (count < 6 || count > 256) throw new ArgumentOutOfRangeException("count");
            Color[] pixels = new Color[count];
            for (int ledIndex = 0; ledIndex < count; ledIndex++)
            {
                int colorIndex = sections ? ledIndex * 6 / count : ledIndex % 6;
                pixels[ledIndex] = colors[colorIndex];
            }
            SetPixels(stripIndex, pixels, selectedOrder, brightness);
        }

        internal void SetPixels(int stripIndex, Color[] pixels, string selectedOrder, int brightness,
            bool reassertDirectMode = false)
        {
            if (stripIndex < 0 || stripIndex > 1) throw new ArgumentOutOfRangeException("stripIndex");
            if (pixels == null || pixels.Length < 1 || pixels.Length > 256)
                throw new ArgumentException("Choose between 1 and 256 LED colors.");
            if (brightness < 0 || brightness > 100) throw new ArgumentOutOfRangeException("brightness");
            string order = ResolveColorOrder(stripIndex, selectedOrder);
            if (order.Length != 3 || order.IndexOf('R') < 0 || order.IndexOf('G') < 0 || order.IndexOf('B') < 0)
                throw new ArgumentException("Choose a valid LED color order.");
            if (!directOutputInitialized) InitializeDirectOutput();
            else TakeLightingControl(reassertDirectMode);

            int shift = stripIndex * 4;
            int currentCode = (stripCountBytes[0] >> shift) & 15;
            int requestedCode = CapacityCode(pixels.Length);
            if (currentCode > 4 || currentCode < requestedCode ||
                (reassertDirectMode && requestedCode > 0))
            {
                byte newCountByte = (byte)((stripCountBytes[0] & ~(15 << shift)) | (requestedCode << shift));
                Send(new byte[] { ReportId, 0x34, newCountByte, stripCountBytes[1], stripCountBytes[2] });
                stripCountBytes[0] = newCountByte;
            }

            byte effectMask = (byte)(1 << stripIndex);
            if (reassertDirectMode || (disabledStripEffects & effectMask) == 0)
            {
                byte newEffectMask = (byte)(disabledStripEffects | effectMask);
                Send(new byte[] { ReportId, 0x32, newEffectMask });
                disabledStripEffects = newEffectMask;
                Thread.Sleep(50);
            }

            for (int start = 0; start < pixels.Length; start += 19)
            {
                int packetLeds = Math.Min(19, pixels.Length - start);
                byte[] packet = new byte[64];
                packet[0] = ReportId;
                packet[1] = (byte)(0x58 + stripIndex);
                int byteOffset = start * 3;
                packet[2] = (byte)(byteOffset & 0xFF);
                packet[3] = (byte)(byteOffset >> 8);
                packet[4] = (byte)(packetLeds * 3);
                for (int i = 0; i < packetLeds; i++)
                {
                    Color color = pixels[start + i];
                    int offset = 5 + i * 3;
                    packet[offset + order.IndexOf('R')] = (byte)Math.Round(color.R * brightness / 100.0);
                    packet[offset + order.IndexOf('G')] = (byte)Math.Round(color.G * brightness / 100.0);
                    packet[offset + order.IndexOf('B')] = (byte)Math.Round(color.B * brightness / 100.0);
                }
                Send(packet);
            }
            Send(new byte[] { ReportId, 0x28, 0xFF });
        }

        public void Dispose()
        {
            if (handle != Native.InvalidHandle)
            {
                Native.CloseHandle(handle);
                handle = Native.InvalidHandle;
            }
        }
    }

    internal static class Native
    {
        internal static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        internal struct InterfaceData
        {
            internal int Size;
            internal Guid InterfaceClassGuid;
            internal int Flags;
            internal IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Attributes
        {
            internal int Size;
            internal ushort VendorId;
            internal ushort ProductId;
            internal ushort VersionNumber;
        }

        [DllImport("hid.dll")]
        internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_GetAttributes(IntPtr handle, ref Attributes attributes);
        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsed);
        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_FreePreparsedData(IntPtr preparsed);
        [DllImport("hid.dll")]
        internal static extern int HidP_GetCaps(IntPtr preparsed, IntPtr caps);
        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_SetFeature(IntPtr handle, byte[] buffer, int length);
        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_GetFeature(IntPtr handle,
            [In, Out] byte[] buffer, int length);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator,
            IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceSet, IntPtr device,
            ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceSet,
            ref InterfaceData data, IntPtr detail, uint detailSize, out uint required,
            IntPtr deviceInfo);
        [DllImport("setupapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceSet);
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr CreateFile(string name, uint access, uint share,
            IntPtr security, uint creation, uint flags, IntPtr templateFile);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
