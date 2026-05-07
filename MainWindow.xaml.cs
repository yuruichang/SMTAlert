using System.ComponentModel;
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
        private OverlayWindow _overlayWindow;
        private ZKBMonitorWindow _zkbWindow;
        private SettingsWindow _settingsWindow;
        private AlertChannelWindow _alertChannelWindow;
        private AlertCharacter _trackedCharacter;

        public MainWindow()
        {
            InitializeComponent();
            App.AppWindow = this;
            Topmost = App.Config.AlwaysOnTop;

            // Window position
            SourceInitialized += (s, e) => LoadWindowPosition();

            // Character change updates
            App.CharacterMgr.CharactersChanged += OnCharactersChanged;
            App.Config.PropertyChanged += OnConfigChanged;
            OnCharactersChanged();
        }

        private void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlertConfig.AlwaysOnTop))
                Dispatcher.Invoke(() => Topmost = App.Config.AlwaysOnTop);
        }

        private void OnCharactersChanged()
        {
            // Unsubscribe from old character
            if (_trackedCharacter != null)
                _trackedCharacter.PropertyChanged -= OnActiveCharacterPropertyChanged;

            _trackedCharacter = App.ActiveCharacter;

            // Subscribe to new character's property changes
            if (_trackedCharacter != null)
                _trackedCharacter.PropertyChanged += OnActiveCharacterPropertyChanged;

            UpdateStatus();
        }

        private void OnActiveCharacterPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Refresh UI when alert settings change
            if (e.PropertyName == nameof(AlertCharacter.AlertRange) ||
                e.PropertyName == nameof(AlertCharacter.AlertEnabled) ||
                e.PropertyName == nameof(AlertCharacter.Location) ||
                e.PropertyName == nameof(AlertCharacter.IsOnline))
            {
                Dispatcher.Invoke(() => UpdateStatus());
            }
        }

        private void UpdateStatus()
        {
            var c = App.ActiveCharacter;
            if (c != null && !string.IsNullOrEmpty(c.Location))
            {
                bool zh = EveManager.CurrentLanguage == "zh-CN";
                StatusText.Text = $"{c.Name} @ {c.Location}\n" +
                    $"{(zh ? "联盟" : "Alliance")}: {c.AllianceTicker} | " +
                    $"{(zh ? "预警范围" : "Alert Range")}: {c.AlertRange}{(zh ? "跳" : "j")} | " +
                    $"{(c.AlertEnabled ? (zh ? "已启用" : "Enabled") : (zh ? "已禁用" : "Disabled"))}";
            }
            else
            {
                StatusText.Text = (string)TryFindResource("Main_NoChar");
            }
            UpdateTitle();
        }

        public void UpdateTitle()
        {
            var c = App.ActiveCharacter;
            Title = c != null && !string.IsNullOrEmpty(c.Name)
                ? $"SMT Alert - {c.Name}"
                : "SMT Alert";
        }

        // --- Button handlers ---
        private void BtnOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
                _overlayWindow = null;
                return;
            }
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Closed += (s, a) => _overlayWindow = null;
            _overlayWindow.Show();
        }

        private void BtnZKB_Click(object sender, RoutedEventArgs e)
        {
            if (_zkbWindow != null)
            {
                _zkbWindow.Close();
                _zkbWindow = null;
                return;
            }
            _zkbWindow = new ZKBMonitorWindow();
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

        private void BtnAddChar_Click(object sender, RoutedEventArgs e)
        {
            var logonWindow = new LogonWindow { Owner = this };
            logonWindow.ShowDialog();
            UpdateStatus();
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
        }

        // --- Window management ---
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _overlayWindow?.Close();
            _zkbWindow?.Close();
            _alertChannelWindow?.Close();
            EveManager.Instance?.ShutDown();
            App.CharacterMgr?.Shutdown();
            App.Config?.Save();
            StoreWindowPosition();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // Window state change handler
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
    }
}
