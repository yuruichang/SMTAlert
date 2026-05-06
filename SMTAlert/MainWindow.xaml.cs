using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SMT.EVEData;
using SMTAlert.Models;
using Application = System.Windows.Application;

namespace SMTAlert
{
    public partial class MainWindow : Window
    {
        private AlertManager _manager;
        private DispatcherTimer _uiTimer;
        private DispatcherTimer _charUpdateTimer;
        private SettingsWindow _settingsWindow;
        private IntelAlertWindow _intelWindow;
        private ZKBWindow _zkbWindow;

        public MainWindow()
        {
            InitializeComponent();

            _manager = new AlertManager();
            _manager.Initialize();

            ApplyLanguage(_manager.Config.Language);

            RangeSlider.Value = _manager.Config.AlertRange;
            RangeValueLabel.Text = _manager.Config.AlertRange.ToString();
            Topmost = _manager.Config.AlwaysOnTop;

            CharCombo.ItemsSource = _manager.Characters;
            CharCombo.DisplayMemberPath = "Name";
            IntelList.ItemsSource = _manager.IntelEntries;

            // Restore last selected character
            string lastName = Properties.Settings.Default.LastCharacterName;
            if (!string.IsNullOrEmpty(lastName))
            {
                foreach (var c in _manager.Characters)
                {
                    if (string.Equals(c.Name, lastName, StringComparison.OrdinalIgnoreCase))
                    {
                        CharCombo.SelectedItem = c;
                        break;
                    }
                }
            }

            _manager.IntelAlertTriggered += OnIntelAlert;

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            _charUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _charUpdateTimer.Tick += CharUpdateTimer_Tick;
            _charUpdateTimer.Start();

            // Refresh all ESI-linked characters' tokens on startup
            Loaded += async (s, e) =>
            {
                foreach (var c in _manager.Characters)
                {
                    if (c.ESILinked)
                    {
                        try { await c.Update(); } catch { }
                    }
                }
            };

            // Create child floating windows (Owner set after SourceInitialized)
            _intelWindow = new IntelAlertWindow(_manager);
            _zkbWindow = new ZKBWindow(_manager);

            SourceInitialized += (s, e) =>
            {
                _intelWindow.Owner = this;
                _zkbWindow.Owner = this;

                var placement = Properties.Settings.Default.MainWindow_placement;
                if (!string.IsNullOrEmpty(placement))
                    WindowPlacement.SetPlacement(new WindowInteropHelper(this).Handle, placement);

                // Show child windows after main window is positioned
                ShowIntelWindow();
                ShowZKBWindow();
            };
            Closing += MainWindow_Closing;
        }

        public void ApplyLanguage(string langCode)
        {
            ResourceDictionary oldDict = null;
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                if (dict.Source != null && dict.Source.OriginalString.StartsWith("Languages/"))
                { oldDict = dict; break; }
            }

            var newDict = new ResourceDictionary
            {
                Source = new Uri($"Languages/{langCode}.xaml", UriKind.Relative)
            };
            Application.Current.Resources.MergedDictionaries.Add(newDict);
            if (oldDict != null)
                Application.Current.Resources.MergedDictionaries.Remove(oldDict);

            // Update ship type display language
            EveManager.CurrentLanguage = langCode;
            if (_manager?.EveManager?.ZKillFeed?.KillStream != null)
            {
                foreach (var kill in _manager.EveManager.ZKillFeed.KillStream)
                    kill.RefreshShipTypeDisplay();
            }
        }

        private void ShowIntelWindow(bool activate = true)
        {
            if (_intelWindow != null && !_intelWindow.IsVisible)
            {
                _intelWindow.ShowActivated = activate;
                _intelWindow.Show();
            }
        }

        private void HideIntelWindow()
        {
            _intelWindow?.Hide();
        }

        private void ShowZKBWindow()
        {
            if (_zkbWindow != null && !_zkbWindow.IsVisible)
                _zkbWindow.Show();
        }

        private void HideZKBWindow()
        {
            _zkbWindow?.Hide();
        }

        private void ToggleIntel_Click(object sender, RoutedEventArgs e)
        {
            if (_intelWindow == null) return;
            if (_intelWindow.IsVisible)
                HideIntelWindow();
            else
                ShowIntelWindow();
        }

        private void ToggleZKB_Click(object sender, RoutedEventArgs e)
        {
            if (_zkbWindow == null) return;
            if (_zkbWindow.IsVisible)
                HideZKBWindow();
            else
                ShowZKBWindow();
        }

        private void CharCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var prev = _manager.ActiveCharacter;
            _manager.ActiveCharacter = CharCombo.SelectedItem as LocalCharacter;
            if (_manager.ActiveCharacter != null)
            {
                Properties.Settings.Default.LastCharacterName = _manager.ActiveCharacter.Name;
                Properties.Settings.Default.Save();
            }
            if (_manager.ActiveCharacter == null && prev != null)
                _manager.StopZKBFeed();
            _lastLocation = "";
            _lastRange = -1;
            _manager.UpdateRangeCache();
            _manager.TryAutoStartZKB();
        }

        private void RangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int val = (int)e.NewValue;
            if (RangeValueLabel != null)
                RangeValueLabel.Text = val.ToString();
            if (_manager?.Config != null)
                _manager.Config.AlertRange = val;
        }

        private string _lastLocation = "";
        private int _lastRange = -1;

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_manager.ActiveCharacter != null)
            {
                CharLocationLabel.Text = _manager.ActiveCharacter.Location ?? "---";
                CharRegionLabel.Text = _manager.ActiveCharacter.Region ?? "---";

                string curLoc = _manager.ActiveCharacter.Location ?? "";
                int curRange = _manager.Config?.AlertRange ?? 5;
                if (curLoc != _lastLocation || curRange != _lastRange)
                {
                    _lastLocation = curLoc;
                    _lastRange = curRange;
                    _manager.UpdateRangeCache();
                }
            }
            else
            {
                CharLocationLabel.Text = "---";
                CharRegionLabel.Text = "---";
            }

            _manager.CleanupOldEntries();
            _manager.PeriodicCleanup();

            foreach (var item in _manager.IntelEntries)
            {
                item.InAlertRange = CheckInRange(item);
            }
        }

        private bool CheckInRange(IntelAlertEntry entry)
        {
            foreach (var sys in entry.Systems)
            {
                if (_manager.IsSystemInRange(sys))
                    return true;
            }
            return false;
        }

        private async void CharUpdateTimer_Tick(object sender, EventArgs e)
        {
            var c = _manager.ActiveCharacter;
            if (c == null || !c.ESILinked) return;

            try { await c.Update(); } catch { }
            _manager.TryAutoStartZKB();
        }

        private void OnIntelAlert(int alertLevel)
        {
            Dispatcher.Invoke(() =>
            {
                _manager.PlayAlertSound();
                if (_intelWindow != null && !_intelWindow.IsVisible)
                    ShowIntelWindow(activate: false);
                if (_intelWindow != null && _intelWindow.WindowState == WindowState.Minimized)
                    _intelWindow.WindowState = WindowState.Normal;
                // Do NOT call Activate() — alert should not steal focus from foreground app
            });
        }

        private void ClearIntel_Click(object sender, RoutedEventArgs e)
        {
            _manager.IntelEntries.Clear();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(_manager.Config);
                _settingsWindow.Owner = this;
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
                _settingsWindow.LanguageChanged += lang =>
                {
                    ApplyLanguage(lang);
                };
                _settingsWindow.SettingsSaved += () =>
                {
                    _intelWindow?.ApplyOpacity();
                    _zkbWindow?.ApplyOpacity();
                    if (_manager?.Config != null)
                        Topmost = _manager.Config.AlwaysOnTop;
                };
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void AddChar_Click(object sender, RoutedEventArgs e)
        {
            _manager.StartAddCharacter();
        }

        private void RemoveChar_Click(object sender, RoutedEventArgs e)
        {
            var c = CharCombo.SelectedItem as LocalCharacter;
            if (c != null)
                _manager.RemoveCharacter(c);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.MainWindow_placement =
                WindowPlacement.GetPlacement(new WindowInteropHelper(this).Handle);
            Properties.Settings.Default.Save();

            if (_intelWindow != null)
            {
                _intelWindow.SavePlacement();
                _intelWindow.ForceClose = true;
                _intelWindow.Close();
            }
            if (_zkbWindow != null)
            {
                _zkbWindow.SavePlacement();
                _zkbWindow.ForceClose = true;
                _zkbWindow.Close();
            }

            _manager.Shutdown();
            _uiTimer?.Stop();
            _charUpdateTimer?.Stop();
        }
    }
}
