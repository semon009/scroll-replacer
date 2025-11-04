using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace ScrollReplacer
{
    public partial class Form1 : Form
    {
        // Windows API for mouse control
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        // Windows API for low-level keyboard hook
        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);

        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const int WHEEL_DELTA = 120;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Control variables
        private List<Keys> scrollUpKeys = new List<Keys>();
        private List<Keys> scrollDownKeys = new List<Keys>();
        private int scrollSpeed = 3;

        // Global keyboard hook
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc hookCallback;
        private HashSet<Keys> currentlyPressedKeys = new HashSet<Keys>();

        // System tray
        private NotifyIcon trayIcon;

        // Key recording
        private bool isRecordingUpKey = false;
        private bool isRecordingDownKey = false;
        private Label upKeysDisplay;
        private Label downKeysDisplay;
        private HashSet<Keys> recordedKeys = new HashSet<Keys>();

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            hookCallback = HookCallback;
            
            // Default keys - ScrollLock و Pause (نادرة جداً)
            scrollUpKeys.Add(Keys.Scroll);
            scrollDownKeys.Add(Keys.Pause);
        }

        private void InitializeCustomComponents()
        {
            // Setup main window
            this.Text = "بديل السكرول - Scroll Replacer";
            this.Size = new Size(450, 570);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);

            // Title label
            Label titleLabel = new Label
            {
                Text = "🖱️ بديل عجلة الماوس",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 20),
                Size = new Size(410, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(titleLabel);

            // Recommended keys info
            Label recommendLabel = new Label
            {
                Text = "💡 أزرار مقترحة (نادرة الاستخدام):\nScrollLock • Pause • Insert • CapsLock • FN",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(102, 226, 134),
                Location = new Point(20, 65),
                Size = new Size(410, 35),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(recommendLabel);

            // Quick preset buttons
            Panel presetPanel = new Panel
            {
                Location = new Point(20, 105),
                Size = new Size(410, 40),
                BackColor = Color.FromArgb(35, 35, 38)
            };

            Label presetLabel = new Label
            {
                Text = "اختصارات سريعة:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(5, 10),
                Size = new Size(100, 20)
            };
            presetPanel.Controls.Add(presetLabel);

            Button preset1 = CreatePresetButton("Scroll/Pause", 110, 5, () => {
                scrollUpKeys.Clear(); scrollUpKeys.Add(Keys.Scroll);
                scrollDownKeys.Clear(); scrollDownKeys.Add(Keys.Pause);
                UpdateKeyDisplay();
            });
            Button preset2 = CreatePresetButton("CapsLock/Insert", 210, 5, () => {
                scrollUpKeys.Clear(); scrollUpKeys.Add(Keys.CapsLock);
                scrollDownKeys.Clear(); scrollDownKeys.Add(Keys.Insert);
                UpdateKeyDisplay();
            });
            Button preset3 = CreatePresetButton("[ / ]", 320, 5, () => {
                scrollUpKeys.Clear(); scrollUpKeys.Add(Keys.OemOpenBrackets);
                scrollDownKeys.Clear(); scrollDownKeys.Add(Keys.OemCloseBrackets);
                UpdateKeyDisplay();
            });
            
            presetPanel.Controls.Add(preset1);
            presetPanel.Controls.Add(preset2);
            presetPanel.Controls.Add(preset3);
            this.Controls.Add(presetPanel);

            // Key selection section
            GroupBox keysGroup = new GroupBox
            {
                Text = "⌨️ اختر المفاتيح المخصصة (يمكن أكثر من مفتاح معاً)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 155),
                Size = new Size(410, 180)
            };

            // Scroll Up Keys
            Label upKeyLabel = new Label
            {
                Text = "مفاتيح التكبير (Scroll Up):",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.LightGray,
                Location = new Point(210, 25),
                Size = new Size(180, 25),
                TextAlign = ContentAlignment.MiddleRight
            };
            keysGroup.Controls.Add(upKeyLabel);

            upKeysDisplay = new Label
            {
                Text = "Scroll",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 226, 134),
                BackColor = Color.FromArgb(30, 30, 32),
                Location = new Point(15, 50),
                Size = new Size(380, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
            keysGroup.Controls.Add(upKeysDisplay);

            Button btnRecordUp = new Button
            {
                Text = "🎯 سجل",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(102, 126, 234),
                ForeColor = Color.White,
                Location = new Point(15, 85),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRecordUp.FlatAppearance.BorderSize = 0;
            btnRecordUp.Click += BtnRecordUp_Click;
            keysGroup.Controls.Add(btnRecordUp);

            Button btnClearUp = new Button
            {
                Text = "🗑️ مسح",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                Location = new Point(125, 85),
                Size = new Size(70, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearUp.FlatAppearance.BorderSize = 0;
            btnClearUp.Click += (s, e) => { scrollUpKeys.Clear(); UpdateKeyDisplay(); };
            keysGroup.Controls.Add(btnClearUp);

            // Scroll Down Keys
            Label downKeyLabel = new Label
            {
                Text = "مفاتيح التصغير (Scroll Down):",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.LightGray,
                Location = new Point(210, 125),
                Size = new Size(180, 25),
                TextAlign = ContentAlignment.MiddleRight
            };
            keysGroup.Controls.Add(downKeyLabel);

            downKeysDisplay = new Label
            {
                Text = "Pause",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 140, 140),
                BackColor = Color.FromArgb(30, 30, 32),
                Location = new Point(15, 145),
                Size = new Size(380, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
            keysGroup.Controls.Add(downKeysDisplay);

            Button btnRecordDown = new Button
            {
                Text = "🎯 سجل",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(118, 75, 162),
                ForeColor = Color.White,
                Location = new Point(215, 85),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRecordDown.FlatAppearance.BorderSize = 0;
            btnRecordDown.Click += BtnRecordDown_Click;
            keysGroup.Controls.Add(btnRecordDown);

            Button btnClearDown = new Button
            {
                Text = "🗑️ مسح",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                Location = new Point(325, 85),
                Size = new Size(70, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearDown.FlatAppearance.BorderSize = 0;
            btnClearDown.Click += (s, e) => { scrollDownKeys.Clear(); UpdateKeyDisplay(); };
            keysGroup.Controls.Add(btnClearDown);

            this.Controls.Add(keysGroup);

            // Scroll speed section
            GroupBox speedGroup = new GroupBox
            {
                Text = "⚡ سرعة التكبير/التصغير",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 355),
                Size = new Size(410, 100)
            };

            TrackBar speedTrack = new TrackBar
            {
                Minimum = 1,
                Maximum = 10,
                Value = 3,
                TickFrequency = 1,
                Location = new Point(20, 35),
                Size = new Size(270, 45)
            };

            Label speedValue = new Label
            {
                Text = "3x",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 126, 234),
                Location = new Point(300, 35),
                Size = new Size(80, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            speedTrack.ValueChanged += (s, e) =>
            {
                scrollSpeed = speedTrack.Value;
                speedValue.Text = scrollSpeed + "x";
            };

            speedGroup.Controls.Add(speedTrack);
            speedGroup.Controls.Add(speedValue);
            this.Controls.Add(speedGroup);

            // Start button
            Button startBtn = new Button
            {
                Text = "▶️ تشغيل التطبيق",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                BackColor = Color.FromArgb(102, 126, 234),
                ForeColor = Color.White,
                Location = new Point(105, 475),
                Size = new Size(240, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            startBtn.FlatAppearance.BorderSize = 0;
            startBtn.Click += (s, e) =>
            {
                StartScrollReplacement();
                this.WindowState = FormWindowState.Minimized;
            };
            this.Controls.Add(startBtn);

            // Setup system tray icon
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Scroll Replacer - يعمل في الخلفية"
            };

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("إظهار النافذة", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add("إيقاف", null, (s, e) => Application.Exit());
            trayIcon.ContextMenuStrip = trayMenu;

            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };

            // Handle form closing
            this.FormClosing += Form1_FormClosing;
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;
            
            UpdateKeyDisplay();
        }

        private Button CreatePresetButton(string text, int x, int y, Action onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Location = new Point(x, y),
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void BtnRecordUp_Click(object sender, EventArgs e)
        {
            isRecordingUpKey = true;
            isRecordingDownKey = false;
            scrollUpKeys.Clear();
            recordedKeys.Clear();
            upKeysDisplay.Text = "▶ اضغط المفاتيح معاً... (Enter للحفظ، Esc للإلغاء)";
            upKeysDisplay.ForeColor = Color.Yellow;
        }

        private void BtnRecordDown_Click(object sender, EventArgs e)
        {
            isRecordingDownKey = true;
            isRecordingUpKey = false;
            scrollDownKeys.Clear();
            recordedKeys.Clear();
            downKeysDisplay.Text = "▶ اضغط المفاتيح معاً... (Enter للحفظ، Esc للإلغاء)";
            downKeysDisplay.ForeColor = Color.Yellow;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (isRecordingUpKey || isRecordingDownKey)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;

                if (e.KeyCode == Keys.Enter)
                {
                    if (isRecordingUpKey)
                    {
                        scrollUpKeys.AddRange(recordedKeys);
                        isRecordingUpKey = false;
                    }
                    else if (isRecordingDownKey)
                    {
                        scrollDownKeys.AddRange(recordedKeys);
                        isRecordingDownKey = false;
                    }
                    recordedKeys.Clear();
                    UpdateKeyDisplay();
                    return;
                }

                if (e.KeyCode == Keys.Escape)
                {
                    isRecordingUpKey = false;
                    isRecordingDownKey = false;
                    recordedKeys.Clear();
                    UpdateKeyDisplay();
                    return;
                }

                // Add key to recording
                if (!recordedKeys.Contains(e.KeyCode))
                {
                    recordedKeys.Add(e.KeyCode);
                    
                    string keysText = string.Join(" + ", recordedKeys.Select(k => GetKeyDisplayName(k)));
                    
                    if (isRecordingUpKey)
                    {
                        upKeysDisplay.Text = keysText + " (Enter للحفظ)";
                    }
                    else if (isRecordingDownKey)
                    {
                        downKeysDisplay.Text = keysText + " (Enter للحفظ)";
                    }
                }
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (isRecordingUpKey || isRecordingDownKey)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private string GetKeyDisplayName(Keys key)
        {
            switch (key)
            {
                case Keys.LControlKey: return "LCtrl";
                case Keys.RControlKey: return "RCtrl";
                case Keys.LShiftKey: return "LShift";
                case Keys.RShiftKey: return "RShift";
                case Keys.LMenu: return "LAlt";
                case Keys.RMenu: return "RAlt";
                case Keys.Scroll: return "ScrollLock";
                case Keys.Pause: return "Pause";
                case Keys.CapsLock: return "CapsLock";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                default: return key.ToString();
            }
        }

        private void UpdateKeyDisplay()
        {
            // Update Up Keys Display
            if (scrollUpKeys.Count > 0)
            {
                upKeysDisplay.Text = string.Join(" + ", scrollUpKeys.Select(k => GetKeyDisplayName(k)));
                upKeysDisplay.ForeColor = Color.FromArgb(102, 226, 134);
            }
            else
            {
                upKeysDisplay.Text = "لم يتم اختيار مفاتيح";
                upKeysDisplay.ForeColor = Color.Gray;
            }

            // Update Down Keys Display
            if (scrollDownKeys.Count > 0)
            {
                downKeysDisplay.Text = string.Join(" + ", scrollDownKeys.Select(k => GetKeyDisplayName(k)));
                downKeysDisplay.ForeColor = Color.FromArgb(255, 140, 140);
            }
            else
            {
                downKeysDisplay.Text = "لم يتم اختيار مفاتيح";
                downKeysDisplay.ForeColor = Color.Gray;
            }
        }

        private void SimulateScroll(int delta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    currentlyPressedKeys.Add(key);

                    // Check if all scroll up keys are pressed
                    if (scrollUpKeys.Count > 0 && scrollUpKeys.All(k => currentlyPressedKeys.Contains(k)))
                    {
                        SimulateScroll(scrollSpeed * WHEEL_DELTA);
                    }
                    // Check if all scroll down keys are pressed
                    else if (scrollDownKeys.Count > 0 && scrollDownKeys.All(k => currentlyPressedKeys.Contains(k)))
                    {
                        SimulateScroll(-scrollSpeed * WHEEL_DELTA);
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    currentlyPressedKeys.Remove(key);
                }
            }

            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        private void StartScrollReplacement()
        {
            if (scrollUpKeys.Count == 0 && scrollDownKeys.Count == 0)
            {
                MessageBox.Show(
                    "يرجى اختيار المفاتيح أولاً!",
                    "تنبيه",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            // Install global keyboard hook
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                hookID = SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, LoadLibrary(curModule.ModuleName), 0);
            }

            string upKeysText = scrollUpKeys.Count > 0 ? string.Join(" + ", scrollUpKeys.Select(k => GetKeyDisplayName(k))) : "غير محدد";
            string downKeysText = scrollDownKeys.Count > 0 ? string.Join(" + ", scrollDownKeys.Select(k => GetKeyDisplayName(k))) : "غير محدد";

            MessageBox.Show(
                "تم تشغيل البرنامج بنجاح! ✅\n\n" +
                $"• مفاتيح التكبير: {upKeysText}\n" +
                $"• مفاتيح التصغير: {downKeysText}\n" +
                "• البرنامج يعمل في الخلفية\n\n" +
                "للوصول للإعدادات: اضغط على أيقونة البرنامج في Taskbar",
                "جاهز للعمل!",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookID);
            }

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }
    }
}