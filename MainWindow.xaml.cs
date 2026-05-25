using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using SMT.EVEData;

namespace SMTAlert
{
    /// <summary>
    /// Main control window for SMTAlert. Provides access to overlay, ZKB monitor, and settings.
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<AlertCharacter, OverlayWindow> _overlayWindows = new();
        private ZKBMonitorWindow _zkbWindow;
        private SettingsWindow _settingsWindow;
        private AlertChannelWindow _alertChannelWindow;
        private System.Windows.Forms.NotifyIcon _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            App.AppWindow = this;
            Topmost = App.Config.AlwaysOnTop;

            // Window position + intercept minimize + hook
            SourceInitialized += (s, e) =>
            {
                LoadWindowPosition();
                var hwnd = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProcHook);
            };

            // System tray
            InitializeTrayIcon();

            // Character change updates
            App.CharacterMgr.CharactersChanged += OnCharactersChanged;
            App.Config.PropertyChanged += OnConfigChanged;
            foreach (var c in App.CharacterMgr.Characters)
                c.PropertyChanged += OnCharPropertyChanged;
            OnCharactersChanged();

            // Restore previously open windows
            Dispatcher.BeginInvoke(new Action(() => RestoreWindowStates()));
        }

        private void InitializeTrayIcon()
        {
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "logo.ico");
            Icon trayIcon;
            if (System.IO.File.Exists(iconPath))
                trayIcon = new Icon(iconPath);
            else
                trayIcon = SystemIcons.Application;

            var showText = (string)TryFindResource("App_TrayShow") ?? "Show";
            var exitText = (string)TryFindResource("App_TrayExit") ?? "Exit";

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add(showText, null, (s, e) => RestoreFromTray());
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitText, null, (s, e) => ExitApplication());

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = trayIcon,
                Text = "SMT Alert",
                ContextMenuStrip = contextMenu,
                Visible = true
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
            Application.Current.Shutdown();
        }

        private void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlertConfig.AlwaysOnTop))
                Dispatcher.Invoke(() => Topmost = App.Config.AlwaysOnTop);
        }

        private void OnCharPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlertCharacter.Location) ||
                e.PropertyName == nameof(AlertCharacter.IsOnline) ||
                e.PropertyName == nameof(AlertCharacter.AlertEnabled) ||
                e.PropertyName == nameof(AlertCharacter.AlertRange) ||
                e.PropertyName == nameof(AlertCharacter.IsMonitored))
            {
                Dispatcher.Invoke(() => RefreshMonitoredList());
            }
        }

        private void OnCharactersChanged()
        {
            RefreshMonitoredList();
        }

        private void RefreshMonitoredList()
        {
            var monitored = App.CharacterMgr.Characters.Where(c => c.IsMonitored).ToList();
            CharItems.ItemsSource = monitored;
            StatusText.Visibility = monitored.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateTitle();
        }

        public void UpdateTitle()
        {
            var monitored = App.CharacterMgr.Characters.Where(c => c.IsMonitored).ToList();
            if (monitored.Count > 1)
                Title = $"SMT Alert - {monitored.Count} {(EveManager.CurrentLanguage == "zh-CN" ? "个角色" : "chars")}";
            else if (monitored.Count == 1)
                Title = $"SMT Alert - {monitored[0].Name}";
            else
                Title = "SMT Alert";

            if (_notifyIcon != null)
                _notifyIcon.Text = Title;
        }

        // --- Button handlers ---
        private void BtnOverlay_Click(object sender, RoutedEventArgs e)
        {
            var c = (AlertCharacter)((System.Windows.Controls.Button)sender).Tag;
            if (c == null) return;

            if (_overlayWindows.TryGetValue(c, out var existing) && existing != null)
            {
                existing.Close();
                _overlayWindows.Remove(c);
                return;
            }

            var overlay = new OverlayWindow(c) { Owner = this };
            overlay.Closed += (s, a) =>
            {
                if (c != null)
                    _overlayWindows.Remove(c);
            };
            _overlayWindows[c] = overlay;
            overlay.Show();
        }

        private void BtnZKB_Click(object sender, RoutedEventArgs e)
        {
            if (_zkbWindow != null)
            {
                _zkbWindow.Close();
                _zkbWindow = null;
                return;
            }
            _zkbWindow = new ZKBMonitorWindow { Owner = this };
            _zkbWindow.Closed += (s, a) => _zkbWindow = null;
            _zkbWindow.Show();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Focus();
                return;
            }
            _settingsWindow = new SettingsWindow { Owner = this };
            _settingsWindow.Closed += (s, a) => _settingsWindow = null;
            _settingsWindow.ShowDialog();
        }

        private void BtnAlertChannel_Click(object sender, RoutedEventArgs e)
        {
            if (_alertChannelWindow != null)
            {
                _alertChannelWindow.Close();
                _alertChannelWindow = null;
                return;
            }
            _alertChannelWindow = new AlertChannelWindow();
            _alertChannelWindow.Closed += (s, a) => _alertChannelWindow = null;
            _alertChannelWindow.Show();
            _alertChannelWindow.Activate();
        }

        // --- Window management ---
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save window states
            SaveWindowStates();

            foreach (var ov in _overlayWindows.Values.ToList())
                ov?.Close();
            _overlayWindows.Clear();
            _zkbWindow?.Close();
            _alertChannelWindow?.Close();
            EveManager.Instance?.ShutDown();
            App.CharacterMgr?.Shutdown();
            App.Config?.Save();
            StoreWindowPosition();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (App.Config.MinimizeToTray && WindowState == WindowState.Minimized)
            {
                Hide();
                foreach (var ov in _overlayWindows.Values)
                {
                    if (ov != null && ov.WindowState == WindowState.Minimized)
                        ov.WindowState = WindowState.Normal;
                }
                if (_zkbWindow != null && _zkbWindow.WindowState == WindowState.Minimized)
                    _zkbWindow.WindowState = WindowState.Normal;
                if (_alertChannelWindow != null && _alertChannelWindow.WindowState == WindowState.Minimized)
                    _alertChannelWindow.WindowState = WindowState.Normal;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern long GetWindowLongPtrW(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern long SetWindowLongPtrW(IntPtr hWnd, int nIndex, long dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int WM_NCLBUTTONDOWN = 0x00A1;
            const int HTSYSMENU = 3;
            const int HTCAPTION = 2;
            const int SC_MINIMIZE = 0xF020;

            if (msg == WM_NCLBUTTONDOWN && wParam.ToInt64() == HTSYSMENU)
            {
                SendMessage(hwnd, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), lParam);
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_SYSCOMMAND && wParam.ToInt64() == SC_MINIMIZE && App.Config.MinimizeToTray)
            {
                handled = true;
                Hide();
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;

            const int GWL_STYLE = -16;
            const long WS_MAXIMIZEBOX = 0x00010000L;
            var style = GetWindowLongPtrW(hwnd, GWL_STYLE);
            if ((style & WS_MAXIMIZEBOX) != 0)
            {
                style &= ~WS_MAXIMIZEBOX;
                SetWindowLongPtrW(hwnd, GWL_STYLE, style);
                const uint SWP_NOSIZE = 0x0001;
                const uint SWP_NOMOVE = 0x0002;
                const uint SWP_NOZORDER = 0x0004;
                const uint SWP_FRAMECHANGED = 0x0020;
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
        }

        // --- Window position persistence ---
        private void LoadWindowPosition()
        {
            string placement = Properties.Settings.Default.MainWindow_placement;
            if (!string.IsNullOrEmpty(placement))
            {
                WindowPlacement.SetPlacement(new WindowInteropHelper(this).Handle, placement);
            }
        }

        private void StoreWindowPosition()
        {
            Properties.Settings.Default.MainWindow_placement =
                WindowPlacement.GetPlacement(new WindowInteropHelper(this).Handle);
            Properties.Settings.Default.Save();
        }

        private void SaveWindowStates()
        {
            var s = Properties.Settings.Default;
            s.OverlayWindow_Open = _overlayWindows.Count > 0;
            s.OverlayWindow_CharacterName = _overlayWindows.Count > 0 && _overlayWindows.First().Key != null
                ? _overlayWindows.First().Key.Name : "";
            s.ZKBMonitorWindow_Open = _zkbWindow != null;
            s.AlertChannelWindow_Open = _alertChannelWindow != null;
            s.MainWindow_MinimizedToTray = WindowState == WindowState.Minimized || !IsVisible;
            s.Save();
        }

        private void RestoreWindowStates()
        {
            var s = Properties.Settings.Default;

            if (s.MainWindow_MinimizedToTray && App.Config.MinimizeToTray)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }

            if (s.OverlayWindow_Open && !string.IsNullOrEmpty(s.OverlayWindow_CharacterName))
            {
                var character = App.CharacterMgr.Characters
                    .FirstOrDefault(c => c.Name == s.OverlayWindow_CharacterName);
                if (character != null)
                {
                    var overlay = new OverlayWindow(character) { Owner = this };
                    overlay.Closed += (snd, a) => _overlayWindows.Remove(character);
                    _overlayWindows[character] = overlay;
                    overlay.Show();
                }
            }

            if (s.ZKBMonitorWindow_Open)
            {
                _zkbWindow = new ZKBMonitorWindow { Owner = this };
                _zkbWindow.Closed += (snd, a) => _zkbWindow = null;
                _zkbWindow.Show();
            }

            if (s.AlertChannelWindow_Open)
            {
                try
                {
                    _alertChannelWindow = new AlertChannelWindow();
                    _alertChannelWindow.Closed += (snd, a) => _alertChannelWindow = null;
                    _alertChannelWindow.Show();
                }
                catch { s.AlertChannelWindow_Open = false; s.Save(); }
            }
        }
    }
}
