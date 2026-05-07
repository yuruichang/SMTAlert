using System.Windows;
using System.Windows.Controls;

namespace SMTAlert
{
    /// <summary>
    /// Settings window for SMTAlert - manages config, character alert settings, and language.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private bool _initializing = true;

        public SettingsWindow()
        {
            InitializeComponent();
            Topmost = App.Config.AlwaysOnTop;
            App.Config.PropertyChanged += OnAlwaysOnTopChanged;

            // Bind character list
            CharacterListBox.ItemsSource = App.CharacterMgr.Characters;
            App.CharacterMgr.CharactersChanged += () =>
            {
                Dispatcher.Invoke(() => CharacterListBox.Items.Refresh());
            };

            // Load config values
            var cfg = App.Config;

            // Language
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag.ToString() == cfg.Language)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            // General
            AlwaysOnTopChk.IsChecked = cfg.AlwaysOnTop;
            MinimizeToTrayChk.IsChecked = cfg.MinimizeToTray;
            AlertChannelTxt.Text = cfg.AlertChannelName;
            AlertClearKeywordsTxt.Text = cfg.AlertClearKeywords;

            // EVE Log folder
            LogFolderTxt.Text = string.IsNullOrEmpty(cfg.EveLogFolder)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "Logs")
                : cfg.EveLogFolder;

            // Overlay
            OverlayBgSlider.Value = cfg.OverlayBackgroundOpacity;
            OverlayBgValue.Content = $"{cfg.OverlayBackgroundOpacity:F2}";
            OverlayContentSlider.Value = cfg.OverlayContentOpacity;
            OverlayContentValue.Content = $"{cfg.OverlayContentOpacity:F2}";
            GathererModeChk.IsChecked = cfg.OverlayGathererMode;
            ShowFullRegionChk.IsChecked = cfg.OverlayHunterModeShowFullRegion;
            ShowSysNamesChk.IsChecked = cfg.OverlayShowSystemNames;

            // ZKB
            ZkbBgSlider.Value = cfg.ZkbBackgroundOpacity;
            ZkbBgValue.Content = $"{cfg.ZkbBackgroundOpacity:F2}";
            ZkbContentSlider.Value = cfg.ZkbContentOpacity;
            ZkbContentValue.Content = $"{cfg.ZkbContentOpacity:F2}";
            ZkbMaxKillsTxt.Text = cfg.ZkbMaxKills.ToString();
            ZkbExpireTxt.Text = cfg.ZkbExpireMinutes.ToString();
            ZkbFilterRegionChk.IsChecked = cfg.ZkbFilterByWarningRegion;
            ZkbCustomSystemsTxt.Text = cfg.ZkbCustomSystems;

            _initializing = false;
        }

        private void OnAlwaysOnTopChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlertConfig.AlwaysOnTop))
                Dispatcher.Invoke(() => Topmost = App.Config.AlwaysOnTop);
        }

        // --- General ---
        private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                string lang = item.Tag.ToString();
                App.Config.Language = lang;
                App.ApplyLanguage(lang);
                App.Config.Save();
                MessageBox.Show("Language changed. Please restart SMTAlert for full effect.",
                    "Language", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AlwaysOnTop_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.AlwaysOnTop = AlwaysOnTopChk.IsChecked == true;
            App.Config.Save();
        }

        private void MinimizeToTray_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.MinimizeToTray = MinimizeToTrayChk.IsChecked == true;
            App.Config.Save();
        }

        private void AlertChannel_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            App.Config.AlertChannelName = AlertChannelTxt.Text;
            App.Config.Save();
        }

        private void AlertClearKeywords_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            App.Config.AlertClearKeywords = AlertClearKeywordsTxt.Text;
            App.Config.Save();
        }

        // --- Overlay ---
        private void OverlayBg_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OverlayBgValue == null) return;
            OverlayBgValue.Content = $"{OverlayBgSlider.Value:F2}";
            if (_initializing) return;
            App.Config.OverlayBackgroundOpacity = (float)OverlayBgSlider.Value;
            App.Config.Save();
        }

        private void OverlayContent_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OverlayContentValue == null) return;
            OverlayContentValue.Content = $"{OverlayContentSlider.Value:F2}";
            if (_initializing) return;
            App.Config.OverlayContentOpacity = (float)OverlayContentSlider.Value;
            App.Config.Save();
        }

        private void GathererMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.OverlayGathererMode = GathererModeChk.IsChecked == true;
            App.Config.Save();
        }

        private void ShowFullRegion_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.OverlayHunterModeShowFullRegion = ShowFullRegionChk.IsChecked == true;
            App.Config.Save();
        }

        private void ShowSysNames_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.OverlayShowSystemNames = ShowSysNamesChk.IsChecked == true;
            App.Config.Save();
        }

        // --- ZKB ---
        private void ZkbBg_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZkbBgValue == null) return;
            ZkbBgValue.Content = $"{ZkbBgSlider.Value:F2}";
            if (_initializing) return;
            App.Config.ZkbBackgroundOpacity = (float)ZkbBgSlider.Value;
            App.Config.Save();
        }

        private void ZkbContent_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZkbContentValue == null) return;
            ZkbContentValue.Content = $"{ZkbContentSlider.Value:F2}";
            if (_initializing) return;
            App.Config.ZkbContentOpacity = (float)ZkbContentSlider.Value;
            App.Config.Save();
        }

        private void ZkbMaxKills_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            if (int.TryParse(ZkbMaxKillsTxt.Text, out int val))
            {
                App.Config.ZkbMaxKills = val;
                App.Config.Save();
            }
        }

        private void ZkbExpire_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            if (int.TryParse(ZkbExpireTxt.Text, out int val))
            {
                App.Config.ZkbExpireMinutes = val;
                App.ZKillFeed.KillExpireTimeMinutes = val;
                App.Config.Save();
            }
        }

        private void LogFolder_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            App.Config.EveLogFolder = LogFolderTxt.Text;
            App.Config.Save();
        }

        private void LogFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = (string)TryFindResource("Settings_General_LogFolder"),
                InitialDirectory = LogFolderTxt.Text
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LogFolderTxt.Text = dialog.SelectedPath;
                App.Config.EveLogFolder = dialog.SelectedPath;
                App.Config.Save();
            }
        }

        private void ZkbFilterRegion_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.ZkbFilterByWarningRegion = ZkbFilterRegionChk.IsChecked == true;
            App.Config.Save();
        }

        private void ZkbCustomSystems_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            App.Config.ZkbCustomSystems = ZkbCustomSystemsTxt.Text;
            App.Config.Save();
        }

        // --- Characters ---
        private void AddChar_Click(object sender, RoutedEventArgs e)
        {
            var logonWindow = new LogonWindow { Owner = this };
            logonWindow.ShowDialog();
            CharacterListBox.Items.Refresh();
        }

        private void DeleteChar_Click(object sender, RoutedEventArgs e)
        {
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                var result = MessageBox.Show(
                    (string)TryFindResource("Main_CharDeleteConfirm"),
                    (string)TryFindResource("Main_CharDelete"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (App.ActiveCharacter == c)
                        App.ActiveCharacter = App.CharacterMgr.Characters.FirstOrDefault(x => x != c);
                    App.CharacterMgr.RemoveCharacter(c);
                    CharacterListBox.Items.Refresh();
                }
            }
        }

        private void CharacterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                CharListGrid.Visibility = Visibility.Collapsed;
                CharSettingsPanel.Visibility = Visibility.Visible;
                AlertRangeSlider.Value = c.AlertRange;
                AlertRangeValue.Content = $"{c.AlertRange} {(string)TryFindResource("Char_Jumps")}";
                AlertEnabledChk.IsChecked = c.AlertEnabled;
                IsActiveMonitorChk.IsChecked = c == App.ActiveCharacter;
            }
            else
            {
                CharSettingsPanel.Visibility = Visibility.Collapsed;
                CharListGrid.Visibility = Visibility.Visible;
            }
        }

        private void CharBack_Click(object sender, RoutedEventArgs e)
        {
            CharacterListBox.SelectedItem = null;
            CharSettingsPanel.Visibility = Visibility.Collapsed;
            CharListGrid.Visibility = Visibility.Visible;
        }

        private void IsActiveMonitor_Checked(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                if (App.ActiveCharacter != null)
                    App.ActiveCharacter.IsActiveMonitor = false;
                App.ActiveCharacter = c;
                c.IsActiveMonitor = true;
                App.CharacterMgr.SaveCharacters();
                CharacterListBox.Items.Refresh();
                if (App.AppWindow != null)
                    App.AppWindow.UpdateTitle();
            }
        }

        private void IsActiveMonitor_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            // Re-check — must always have one active character
            if (CharacterListBox.SelectedItem is AlertCharacter c && c == App.ActiveCharacter)
            {
                IsActiveMonitorChk.IsChecked = true;
            }
        }

        private void AlertRange_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AlertRangeSlider == null || AlertRangeValue == null) return;
            int range = (int)AlertRangeSlider.Value;
            AlertRangeValue.Content = $"{range} {(string)TryFindResource("Char_Jumps")}";
            if (_initializing) return;
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                c.AlertRange = range;
                App.CharacterMgr.SaveCharacters();
            }
        }

        private void AlertEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                c.AlertEnabled = AlertEnabledChk.IsChecked == true;
                App.CharacterMgr.SaveCharacters();
            }
        }
    }
}
