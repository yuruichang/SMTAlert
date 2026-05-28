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
            AlertFreshMinTxt.Text = cfg.AlertFreshMinutes.ToString();
            AlertStaleMinTxt.Text = cfg.AlertExpireMinutes.ToString();

            // EVE Log folder
            // Alert sound volume

            AlertVolumeSlider.Value = cfg.AlertVolume * 100.0;
            AlertVolumeValue.Content = $"{(int)(cfg.AlertVolume * 100)}%";
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
            ZkbMonitoredCharIDsTxt.Text = cfg.ZkbMonitoredCharacterIDs;
            ZkbMonitoredCorpIDsTxt.Text = cfg.ZkbMonitoredCorpIDs;
            ZkbUseLocalTimeChk.IsChecked = cfg.ZkbUseLocalTime;
            ZkbFontSizeSlider.Value = cfg.ZkbFontSize;
            ZkbFontSizeLabel.Text = cfg.ZkbFontSize.ToString();

            // Ensure Corp column exists in config (migration guard for running sessions)
            if (!cfg.ZkbColumnOrder.Contains("Corp", StringComparison.OrdinalIgnoreCase))
            {
                cfg.ZkbColumnOrder = cfg.ZkbColumnOrder
                    .Replace("AttackerAlliance", "__PROTECTED_ATTACKER__", StringComparison.OrdinalIgnoreCase)
                    .Replace("Alliance,", "Alliance,Corp,", StringComparison.OrdinalIgnoreCase)
                    .Replace("__PROTECTED_ATTACKER__", "AttackerAlliance");
            }
            if (!cfg.ZkbVisibleColumns.Contains("Corp", StringComparison.OrdinalIgnoreCase))
                cfg.ZkbVisibleColumns += ",Corp";

            // Load column visibility (from ZkbVisibleColumns)
            var visible = cfg.ZkbVisibleColumns
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ZkbColTimeChk.IsChecked = visible.Contains("Time");
            ZkbColRegionChk.IsChecked = visible.Contains("Region");
            ZkbColSystemChk.IsChecked = visible.Contains("System");
            ZkbColAllianceChk.IsChecked = visible.Contains("Alliance");
            ZkbColCorpChk.IsChecked = visible.Contains("Corp");
            ZkbColCharacterIDChk.IsChecked = visible.Contains("CharacterID");
            ZkbColAttackerAllianceChk.IsChecked = visible.Contains("AttackerAlliance");
            ZkbColShipTypeChk.IsChecked = visible.Contains("ShipType");
            ZkbColValueChk.IsChecked = visible.Contains("Value");

            // Reorder UI rows to match ZkbColumnOrder
            ReorderColumnUI();

            _initializing = false;

            VersionText.Text = App.AppVersion;
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

        // --- Alert Volume ---
        private void AlertVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AlertVolumeSlider == null || AlertVolumeValue == null) return;
            int volume = (int)AlertVolumeSlider.Value;
            AlertVolumeValue.Content = $"{volume}%";
            if (_initializing) return;
            App.Config.AlertVolume = volume / 100.0f;
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

        private void AlertFreshMin_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            if (int.TryParse(AlertFreshMinTxt.Text, out int val))
            {
                App.Config.AlertFreshMinutes = val;
                App.Config.Save();
            }
        }

        private void AlertStaleMin_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            if (int.TryParse(AlertStaleMinTxt.Text, out int val))
            {
                App.Config.AlertExpireMinutes = val;
                App.Config.Save();
            }
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

        private void ZkbMonitoredCharIDs_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            App.Config.ZkbMonitoredCharacterIDs = ZkbMonitoredCharIDsTxt.Text;
            App.Config.Save();
        }

        private void ZkbMonitoredCorpIDs_Changed(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            App.Config.ZkbMonitoredCorpIDs = ZkbMonitoredCorpIDsTxt.Text;
            App.Config.Save();
        }

        private void ZkbColumn_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            SaveColumnConfig();
        }

        private void ZkbUseLocalTime_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            App.Config.ZkbUseLocalTime = ZkbUseLocalTimeChk.IsChecked == true;
            App.Config.Save();
        }

        private void ZkbFontSize_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            int size = (int)ZkbFontSizeSlider.Value;
            ZkbFontSizeLabel.Text = size.ToString();
            App.Config.ZkbFontSize = size;
            App.Config.Save();
        }

        private void ZkbColumn_MoveUp(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            string tag = (sender as Button)?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;
            MoveColumn(tag, -1);
        }

        private void ZkbColumn_MoveDown(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            string tag = (sender as Button)?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;
            MoveColumn(tag, 1);
        }

        private void MoveColumn(string columnName, int direction)
        {
            var order = App.Config.ZkbColumnOrder
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            int idx = order.FindIndex(c => string.Equals(c, columnName, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return;

            int swapIdx = idx + direction;
            if (swapIdx < 0 || swapIdx >= order.Count) return;

            (order[idx], order[swapIdx]) = (order[swapIdx], order[idx]);
            App.Config.ZkbColumnOrder = string.Join(",", order);

            // Re-sync ZkbVisibleColumns in the new order
            SaveColumnConfig();
            ReorderColumnUI();
        }

        private void ReorderColumnUI()
        {
            var order = App.Config.ZkbColumnOrder
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var rowMap = new Dictionary<string, System.Windows.FrameworkElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["Time"] = ZkbColTimeChk.Parent as System.Windows.FrameworkElement,
                ["Region"] = ZkbColRegionChk.Parent as System.Windows.FrameworkElement,
                ["System"] = ZkbColSystemChk.Parent as System.Windows.FrameworkElement,
                ["Alliance"] = ZkbColAllianceChk.Parent as System.Windows.FrameworkElement,
                ["Corp"] = ZkbColCorpChk.Parent as System.Windows.FrameworkElement,
                ["CharacterID"] = ZkbColCharacterIDChk.Parent as System.Windows.FrameworkElement,
                ["AttackerAlliance"] = ZkbColAttackerAllianceChk.Parent as System.Windows.FrameworkElement,
                ["ShipType"] = ZkbColShipTypeChk.Parent as System.Windows.FrameworkElement,
                ["Value"] = ZkbColValueChk.Parent as System.Windows.FrameworkElement,
            };

            var container = ZkbColTimeChk.Parent is System.Windows.FrameworkElement parent ? parent.Parent as System.Windows.Controls.Panel : null;
            if (container == null) return;

            int insertIdx = 0;
            foreach (var name in order)
            {
                if (rowMap.TryGetValue(name, out var row) && row != null)
                {
                    int curIdx = container.Children.IndexOf(row);
                    if (curIdx != insertIdx)
                    {
                        container.Children.RemoveAt(curIdx);
                        container.Children.Insert(insertIdx, row);
                    }
                    insertIdx++;
                }
            }
        }

        private void SaveColumnConfig()
        {
            var order = App.Config.ZkbColumnOrder
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var visMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["Time"] = ZkbColTimeChk.IsChecked == true,
                ["Region"] = ZkbColRegionChk.IsChecked == true,
                ["System"] = ZkbColSystemChk.IsChecked == true,
                ["Alliance"] = ZkbColAllianceChk.IsChecked == true,
                ["Corp"] = ZkbColCorpChk.IsChecked == true,
                ["CharacterID"] = ZkbColCharacterIDChk.IsChecked == true,
                ["AttackerAlliance"] = ZkbColAttackerAllianceChk.IsChecked == true,
                ["ShipType"] = ZkbColShipTypeChk.IsChecked == true,
                ["Value"] = ZkbColValueChk.IsChecked == true,
            };

            var visible = order.Where(c => visMap.ContainsKey(c) && visMap[c]).ToList();
            App.Config.ZkbVisibleColumns = string.Join(",", visible);
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
                IsMonitoredChk.IsChecked = c.IsMonitored;
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

        private void IsMonitored_Checked(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                c.IsMonitored = true;
                App.CharacterMgr.SaveCharacters();
                CharacterListBox.Items.Refresh();
                if (App.AppWindow != null)
                    App.AppWindow.UpdateTitle();
            }
        }

        private void IsMonitored_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            if (CharacterListBox.SelectedItem is AlertCharacter c)
            {
                c.IsMonitored = false;
                App.CharacterMgr.SaveCharacters();
                CharacterListBox.Items.Refresh();
                if (App.AppWindow != null)
                    App.AppWindow.UpdateTitle();
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

        private void RepoLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
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
