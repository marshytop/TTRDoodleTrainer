using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClickerTool
{
    public class MainForm : Form
    {
        private const int TargetCount = 16;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_RECORD = 1;
        private const int HOTKEY_START = 2;
        private const int HOTKEY_CLEAR = 3;
        private const int HOTKEY_STOP = 4;
        private const int HOTKEY_MINIMIZE = 5;

        private const uint MOD_NOREPEAT = 0x4000;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private readonly List<Point> _points = new();
        private readonly Random _rng = new();

        private bool _recording;
        private bool _running;
        private int _rounds;
        private int _currentStep;
        private DateTime _startTime;
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        private readonly Label _titleLabel = new();
        private readonly Label _statusLabel = new();
        private readonly Label _pointsLabel = new();
        private readonly Label _roundsLabel = new();
        private readonly Label _elapsedLabel = new();
        private readonly Label _stepLabel = new();
        private readonly ListBox _listBox = new();
        private readonly Button _btnRecord = new();
        private readonly Button _btnStart = new();
        private readonly Button _btnClear = new();
        private readonly Button _btnMinimize = new();
        private readonly Button _btnStop = new();
        private readonly System.Windows.Forms.Timer _uiTimer = new();

        private readonly LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        public MainForm()
        {
            _proc = HookCallback;

            AutoScaleMode = AutoScaleMode.None;

            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "tray.ico");
                if (File.Exists(iconPath))
                {
                    using var ico = new Icon(iconPath);
                    Icon = (Icon)ico.Clone();
                    ShowIcon = true;
                }
                else
                {
                    Icon = SystemIcons.Application;
                    ShowIcon = true;
                }
            }
            catch
            {
                Icon = SystemIcons.Application;
                ShowIcon = true;
            }

            Text = "ToonTown Rewritten Doodle Trainer";
            Size = new Size(440, 660);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(20, 20);
            TopMost = false;
            MaximizeBox = false;
            MinimizeBox = true;
            MinimumSize = new Size(440, 660);
            MaximumSize = new Size(440, 660);
            ShowInTaskbar = true;

            InitializeUi();

            _hookId = SetHook(_proc);

            _uiTimer.Interval = 200;
            _uiTimer.Tick += (_, _) => RefreshUi();
            _uiTimer.Start();

            Resize += MainForm_Resize;
        }

        private void InitializeUi()
        {
            _titleLabel.Text = "ToonTown Rewritten Doodle Trainer";
            _titleLabel.AutoSize = false;
            _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            _titleLabel.Font = new Font(FontFamily.GenericSansSerif, 11F, FontStyle.Bold);
            _titleLabel.Location = new Point(14, 10);
            _titleLabel.Size = new Size(ClientSize.Width - 28, 22);
            _titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _statusLabel.AutoSize = true;
            _statusLabel.Location = new Point(14, 42);
            _statusLabel.Text = "Ready.";

            _pointsLabel.AutoSize = true;
            _pointsLabel.Location = new Point(14, 68);

            _roundsLabel.AutoSize = true;
            _roundsLabel.Location = new Point(14, 93);

            _stepLabel.AutoSize = true;
            _stepLabel.Location = new Point(14, 118);

            _elapsedLabel.AutoSize = true;
            _elapsedLabel.Location = new Point(14, 143);

            _listBox.Location = new Point(14, 175);
            _listBox.Size = new Size(390, 330);
            _listBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            var buttonFont = new Font(FontFamily.GenericSansSerif, 8F, FontStyle.Regular);

            _btnRecord.Text = "Record 16 clicks (F5)";
            _btnRecord.Size = new Size(120, 32);
            _btnRecord.Font = buttonFont;
            _btnRecord.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnRecord.Click += (_, _) => BeginRecording();

            _btnStart.Text = "Start loop (F6)";
            _btnStart.Size = new Size(92, 32);
            _btnStart.Font = buttonFont;
            _btnStart.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnStart.Click += (_, _) => StartLoop();

            _btnClear.Text = "Clear (F7)";
            _btnClear.Size = new Size(70, 32);
            _btnClear.Font = buttonFont;
            _btnClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnClear.Click += (_, _) => ClearPoints();

            _btnMinimize.Text = "Minimize (F8)";
            _btnMinimize.Size = new Size(84, 32);
            _btnMinimize.Font = buttonFont;
            _btnMinimize.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnMinimize.Click += (_, _) => MinimizeWindow();

            _btnStop.Text = "Stop (F9)";
            _btnStop.Size = new Size(390, 32);
            _btnStop.Font = buttonFont;
            _btnStop.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnStop.AutoSize = false;
            _btnStop.MinimumSize = new Size(390, 32);
            _btnStop.MaximumSize = new Size(390, 32);
            _btnStop.Click += (_, _) => StopLoop();

            Controls.AddRange(new Control[]
            {
                _titleLabel, _statusLabel, _pointsLabel, _roundsLabel, _stepLabel, _elapsedLabel,
                _listBox, _btnRecord, _btnStart, _btnClear, _btnMinimize, _btnStop
            });

            CenterButtons();
            RefreshUi();
        }

        private void CenterButtons()
        {
            int spacing = 8;

            int totalWidth =
                _btnRecord.Width +
                _btnStart.Width +
                _btnClear.Width +
                _btnMinimize.Width +
                (spacing * 3);

            int startX = Math.Max(14, (ClientSize.Width - totalWidth) / 2);
            int y = 520;

            _btnRecord.Location = new Point(startX, y);
            _btnStart.Location = new Point(_btnRecord.Right + spacing, y);
            _btnClear.Location = new Point(_btnStart.Right + spacing, y);
            _btnMinimize.Location = new Point(_btnClear.Right + spacing, y);

            _btnStop.Location = new Point(
                (ClientSize.Width - _btnStop.Width) / 2,
                562
            );
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            _titleLabel.Width = ClientSize.Width - 28;
            CenterButtons();
        }

        private void MinimizeWindow()
        {
            WindowState = FormWindowState.Minimized;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RegisterHotKey(Handle, HOTKEY_RECORD, MOD_NOREPEAT, (uint)Keys.F5);
            RegisterHotKey(Handle, HOTKEY_START, MOD_NOREPEAT, (uint)Keys.F6);
            RegisterHotKey(Handle, HOTKEY_CLEAR, MOD_NOREPEAT, (uint)Keys.F7);
            RegisterHotKey(Handle, HOTKEY_STOP, MOD_NOREPEAT, (uint)Keys.F9);
            RegisterHotKey(Handle, HOTKEY_MINIMIZE, MOD_NOREPEAT, (uint)Keys.F8);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _uiTimer.Stop();

            UnregisterHotKey(Handle, HOTKEY_RECORD);
            UnregisterHotKey(Handle, HOTKEY_START);
            UnregisterHotKey(Handle, HOTKEY_CLEAR);
            UnregisterHotKey(Handle, HOTKEY_STOP);
            UnregisterHotKey(Handle, HOTKEY_MINIMIZE);

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            _cts?.Cancel();
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();

                if (id == HOTKEY_RECORD)
                {
                    BeginRecording();
                }
                else if (id == HOTKEY_START)
                {
                    StartLoop();
                }
                else if (id == HOTKEY_CLEAR)
                {
                    ClearPoints();
                }
                else if (id == HOTKEY_STOP)
                {
                    StopLoop();
                }
                else if (id == HOTKEY_MINIMIZE)
                {
                    MinimizeWindow();
                }
            }

            base.WndProc(ref m);
        }

        private void BeginRecording()
        {
            if (_running)
            {
                StopLoop();
            }

            _points.Clear();
            _listBox.Items.Clear();
            _recording = true;
            _statusLabel.Text = "Recording... click 16 spots in order.";
            _currentStep = 0;
            _rounds = 0;
            RefreshUi();
        }

        private void StartLoop()
        {
            if (_points.Count != TargetCount)
            {
                MessageBox.Show($"You need exactly {TargetCount} recorded clicks first.", "Not ready",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_running)
                return;

            _cts = new CancellationTokenSource();
            _running = true;
            _startTime = DateTime.Now;
            _statusLabel.Text = "Running...";
            _runTask = RunLoopAsync(_cts.Token);
            RefreshUi();
        }

        private void StopLoop()
        {
            _recording = false;

            if (_running)
            {
                _cts?.Cancel();
                _running = false;
                _statusLabel.Text = "Stopped.";
            }

            RefreshUi();
        }

        private void ClearPoints()
        {
            if (_running)
                StopLoop();

            _recording = false;
            _points.Clear();
            _listBox.Items.Clear();
            _rounds = 0;
            _currentStep = 0;
            _statusLabel.Text = "Cleared.";
            RefreshUi();
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < _points.Count; i++)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        _currentStep = i + 1;
                        ClickAt(_points[i]);

                        double delaySeconds = 0.5 + (_rng.NextDouble() * 0.5);

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }

                    if (!token.IsCancellationRequested)
                    {
                        _rounds++;
                        _currentStep = 0;
                    }
                }
            }
            finally
            {
                _running = false;
                BeginInvoke(new Action(() =>
                {
                    _statusLabel.Text = "Stopped.";
                    RefreshUi();
                }));
            }
        }

        private void ClickAt(Point p)
        {
            SetCursorPos(p.X, p.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private void RefreshUi()
        {
            _pointsLabel.Text = $"Recorded: {_points.Count}/{TargetCount}";
            _roundsLabel.Text = $"Rounds completed: {_rounds}";
            _stepLabel.Text = $"Current step: {_currentStep}/{TargetCount}";
            _elapsedLabel.Text = _running
                ? $"Elapsed: {(DateTime.Now - _startTime):hh\\:mm\\:ss}"
                : "Elapsed: 00:00:00";
        }

        private void AddRecordedPoint(Point p)
        {
            if (!_recording || _points.Count >= TargetCount)
                return;

            _points.Add(p);
            _listBox.Items.Add($"{_points.Count,2}: X={p.X}, Y={p.Y}");

            if (_points.Count >= TargetCount)
            {
                _recording = false;
                _statusLabel.Text = "Recorded 16 points. Press Start loop.";
            }

            RefreshUi();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _recording && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var p = new Point(info.pt.x, info.pt.y);

                if (!IsDisposed)
                {
                    try
                    {
                        BeginInvoke(new Action(() => AddRecordedPoint(p)));
                    }
                    catch
                    {
                        // Ignore shutdown race conditions
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
            return SetWindowsHookEx(WH_MOUSE_LL, proc, moduleHandle, 0);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}