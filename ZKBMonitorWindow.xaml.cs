using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SMT.EVEData;

namespace SMTAlert
{
    /// <summary>
    /// ZKB kill feed float window - displays real-time kill data with standing-based color coding.
    /// </summary>
    public partial class ZKBMonitorWindow : Window
    {
        private int _maxKills = 50;

        public ZKBMonitorWindow()
        {
            InitializeComponent();

            ZKBKillList.ItemsSource = App.ZKillFeed.KillStream;

            var view = (CollectionView)CollectionViewSource.GetDefaultView(ZKBKillList.ItemsSource);
            view.Filter = item => ZKillFilter(item);

            _maxKills = App.Config.ZkbMaxKills;
            windowBackground.Opacity = App.Config.ZkbBackgroundOpacity;
            ZKBKillList.Opacity = App.Config.ZkbContentOpacity;

            // Apply header opacity
            ApplyHeaderOpacity(App.Config.ZkbContentOpacity);

            // Apply column layout (order + visibility)
            ApplyColumnLayout();

            // Apply font size
            ApplyFontSize();

            // Set time display mode
            SMT.EVEData.ZKillRedisQ.ZKBDataSimple.DisplayLocalTime = App.Config.ZkbUseLocalTime;

            // Initial column width adjustment (deferred for proper layout)
            Dispatcher.BeginInvoke(new Action(() => AdjustColumnWidths()));

            App.ZKillFeed.KillsAddedEvent += OnKillsAdded;
            App.Config.PropertyChanged += OnConfigChanged;
            Closing += ZKBMonitor_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            LoadWindowPosition();
        }

        private void LoadWindowPosition()
        {
            string placement = Properties.Settings.Default.ZKBMonitorWindow_placement;
            if (!string.IsNullOrEmpty(placement))
                WindowPlacement.SetPlacement(new WindowInteropHelper(this).Handle, placement);
        }

        private void StoreWindowPosition()
        {
            Properties.Settings.Default.ZKBMonitorWindow_placement =
                WindowPlacement.GetPlacement(new WindowInteropHelper(this).Handle);
            Properties.Settings.Default.Save();
        }

        private void ZKBMonitor_Closing(object sender, CancelEventArgs e)
        {
            StoreWindowPosition();
            App.ZKillFeed.KillsAddedEvent -= OnKillsAdded;
            App.Config.PropertyChanged -= OnConfigChanged;
        }

        private void ZKBMonitor_Window_Move(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ResizeMode = ResizeMode.NoResize;
                DragMove();
                ResizeMode = ResizeMode.CanResizeWithGrip;
            }
            e.Handled = true;
        }

        private void ZKBMonitor_Window_Close(object sender, MouseButtonEventArgs e) => Close();

        private void ContextMenu_OpenZKB_Click(object sender, RoutedEventArgs e)
        {
            var zs = ZKBKillList.SelectedItem as SMT.EVEData.ZKillRedisQ.ZKBDataSimple;
            if (zs != null)
            {
                string url = $"https://zkillboard.com/kill/{zs.KillID}/";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                { UseShellExecute = true });
            }
        }

        private void ContextMenu_CopyInGameLink_Click(object sender, RoutedEventArgs e)
        {
            var zs = ZKBKillList.SelectedItem as SMT.EVEData.ZKillRedisQ.ZKBDataSimple;
            if (zs == null || string.IsNullOrEmpty(zs.Hash)) return;

            // Re-check name from cache — may have been resolved since the kill arrived
            string victimName = zs.VictimName;
            if (string.IsNullOrEmpty(victimName) && zs.VictimCharacterID != 0)
                victimName = EveManager.Instance.GetCharacterName(zs.VictimCharacterID);

            if (string.IsNullOrEmpty(victimName))
                victimName = "Unknown";

            string link = $"<url=killReport:{zs.KillID}:{zs.Hash}>Kill: {victimName} ({zs.ShipType})</url>";

            try
            {
                Clipboard.SetText(link);
            }
            catch
            {
                // Clipboard may be temporarily locked by another process; silently ignore.
            }
        }

        private void ZKBKillList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var zs = ZKBKillList.SelectedItem as SMT.EVEData.ZKillRedisQ.ZKBDataSimple;
            if (zs != null)
            {
                string url = $"https://zkillboard.com/kill/{zs.KillID}/";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                { UseShellExecute = true });
            }
        }

        private void ZKBKillList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(ZKBKillList, e.GetPosition(ZKBKillList));
            if (hit?.VisualHit != null)
            {
                var row = FindVisualParent<DataGridRow>(hit.VisualHit);
                if (row == null)
                    ZKBKillList.SelectedItem = null;
            }
        }

        private void ZKBKillList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(ZKBKillList, e.GetPosition(ZKBKillList));
            if (hit?.VisualHit != null)
            {
                var row = FindVisualParent<DataGridRow>(hit.VisualHit);
                if (row != null)
                    row.IsSelected = true;
            }
        }

        private void ZKBKillList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(ZKBKillList, Mouse.GetPosition(ZKBKillList));
            if (hit?.VisualHit != null)
            {
                var row = FindVisualParent<DataGridRow>(hit.VisualHit);
                if (row == null)
                    e.Handled = true;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    yield return typed;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typed)
                    return typed;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void ApplyHeaderOpacity(double opacity)
        {
            foreach (var header in FindVisualChildren<DataGridColumnHeader>(ZKBKillList))
            {
                header.Opacity = opacity;
            }
        }

        private void ApplyFontSize()
        {
            double size = App.Config.ZkbFontSize;

            // Apply to rows via the row style
            ZKBKillList.Resources.Remove(typeof(DataGridRow));
            var rowStyle = new Style(typeof(DataGridRow), TryFindResource(typeof(DataGridRow)) as Style);
            rowStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, size));
            ZKBKillList.Resources.Add(typeof(DataGridRow), rowStyle);

            // Apply to column headers via the header style
            foreach (var header in FindVisualChildren<DataGridColumnHeader>(ZKBKillList))
            {
                header.FontSize = size;
            }
        }

        private void ApplyColumnLayout()
        {
            var colMap = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
            {
                ["Time"] = ColTime,
                ["Region"] = ColRegion,
                ["System"] = ColSystem,
                ["Alliance"] = ColAlliance,
                ["Corp"] = ColCorp,
                ["CharacterID"] = ColCharacterID,
                ["AttackerAlliance"] = ColAttackerAlliance,
                ["ShipType"] = ColShipType,
                ["Value"] = ColValue,
            };

            var visible = App.Config.ZkbVisibleColumns
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var order = App.Config.ZkbColumnOrder
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            ZKBKillList.Columns.Clear();
            foreach (var name in order)
            {
                if (colMap.TryGetValue(name, out var col))
                {
                    col.Visibility = visible.Contains(name) ? Visibility.Visible : Visibility.Collapsed;
                    ZKBKillList.Columns.Add(col);
                }
            }
        }

        private void OnKillsAdded()
        {
            Dispatcher.Invoke(() =>
            {
                CollectionViewSource.GetDefaultView(ZKBKillList.ItemsSource)?.Refresh();
                AdjustColumnWidths();
            });
        }

        private void AdjustColumnWidths()
        {
            ZKBKillList.UpdateLayout();

            // Measure with Auto to get content-based widths
            foreach (var col in ZKBKillList.Columns)
            {
                if (col.Visibility == Visibility.Visible)
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
            ZKBKillList.UpdateLayout();

            double totalContentWidth = 0;
            int visibleCount = 0;
            foreach (var col in ZKBKillList.Columns)
            {
                if (col.Visibility == Visibility.Visible)
                {
                    totalContentWidth += col.ActualWidth;
                    visibleCount++;
                }
            }

            double availableWidth = ZKBKillList.ActualWidth - 5;
            if (totalContentWidth < availableWidth && availableWidth > 0 && visibleCount > 0)
            {
                // Content fits in window — proportionally fill using Star coefficients
                foreach (var col in ZKBKillList.Columns)
                {
                    if (col.Visibility == Visibility.Visible)
                    {
                        double ratio = col.ActualWidth / totalContentWidth;
                        col.Width = new DataGridLength(ratio, DataGridLengthUnitType.Star);
                    }
                }
            }
            // else: content wider than window — keep Auto, scrollbar handles overflow
        }

        private bool ZKillFilter(object item)
        {
            var zs = item as SMT.EVEData.ZKillRedisQ.ZKBDataSimple;
            if (zs == null) return false;

            // Check monitored character IDs/names (bypasses system/region filter)
            if (!string.IsNullOrWhiteSpace(App.Config.ZkbMonitoredCharacterIDs))
            {
                var charEntries = App.Config.ZkbMonitoredCharacterIDs
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var entry in charEntries)
                {
                    if (int.TryParse(entry, out int cid))
                    {
                        if (cid == zs.VictimCharacterID)
                            return true;
                    }
                    else
                    {
                        // Name-based reverse lookup
                        foreach (var kvp in EveManager.Instance.CharacterIDToName)
                        {
                            if (string.Equals(kvp.Value, entry, StringComparison.OrdinalIgnoreCase) && kvp.Key == zs.VictimCharacterID)
                                return true;
                        }
                    }
                }
            }

            // Check monitored corp IDs/names (bypasses system/region filter)
            if (!string.IsNullOrWhiteSpace(App.Config.ZkbMonitoredCorpIDs))
            {
                var corpEntries = App.Config.ZkbMonitoredCorpIDs
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var entry in corpEntries)
                {
                    if (int.TryParse(entry, out int cid))
                    {
                        if (cid == zs.VictimCorpID)
                            return true;
                    }
                    else
                    {
                        // Name-based reverse lookup
                        foreach (var kvp in EveManager.Instance.CorporationIDToName)
                        {
                            if (string.Equals(kvp.Value, entry, StringComparison.OrdinalIgnoreCase) && kvp.Key == zs.VictimCorpID)
                                return true;
                        }
                    }
                }
            }

            var c = App.ActiveCharacter;
            bool filterByRegion = App.Config.ZkbFilterByWarningRegion;
            bool hasCustomSystems = !string.IsNullOrWhiteSpace(App.Config.ZkbCustomSystems);
            bool hasActiveChar = c != null && !string.IsNullOrEmpty(c.Region);

            // If no filters active, show all
            if (!filterByRegion && !hasCustomSystems)
                return true;

            // If no active character and region filter is sole filter, show all
            if (!hasActiveChar && filterByRegion && !hasCustomSystems)
                return true;

            var sys = EveManager.Instance?.GetEveSystem(zs.SystemName);
            if (sys == null)
                return !filterByRegion && !hasCustomSystems; // Unknown system: show only if no filters

            // Check character's region filter
            if (filterByRegion && hasActiveChar && sys.Region == c.Region)
                return true;

            // Check custom system/region names (supports both English and Chinese)
            if (hasCustomSystems)
            {
                var names = App.Config.ZkbCustomSystems
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name)) continue;

                    // Direct match against system name (English)
                    if (string.Equals(sys.Name, name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Direct match against region name (English)
                    if (string.Equals(sys.Region, name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Match against Chinese-translated system name
                    if (EveManager.Translations.TryGetValue(sys.Name, out var zhSys) &&
                        string.Equals(zhSys, name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Match against Chinese-translated region name
                    if (EveManager.Translations.TryGetValue(sys.Region, out var zhRegion) &&
                        string.Equals(zhRegion, name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Input is Chinese, resolve to English and match
                    if (EveManager.ChineseToEnglish.TryGetValue(name, out var enName))
                    {
                        if (string.Equals(sys.Name, enName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sys.Region, enName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                return false;
            }

            return false;
        }

        private void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(AlertConfig.ZkbBackgroundOpacity):
                        windowBackground.Opacity = App.Config.ZkbBackgroundOpacity; break;
                    case nameof(AlertConfig.ZkbContentOpacity):
                        ZKBKillList.Opacity = App.Config.ZkbContentOpacity;
                        ApplyHeaderOpacity(App.Config.ZkbContentOpacity); break;
                    case nameof(AlertConfig.ZkbMaxKills):
                        _maxKills = App.Config.ZkbMaxKills; break;
                    case nameof(AlertConfig.ZkbFilterByWarningRegion):
                    case nameof(AlertConfig.ZkbCustomSystems):
                    case nameof(AlertConfig.ZkbMonitoredCharacterIDs):
                    case nameof(AlertConfig.ZkbMonitoredCorpIDs):
                        CollectionViewSource.GetDefaultView(ZKBKillList.ItemsSource)?.Refresh(); break;
                    case nameof(AlertConfig.ZkbUseLocalTime):
                        SMT.EVEData.ZKillRedisQ.ZKBDataSimple.DisplayLocalTime = App.Config.ZkbUseLocalTime;
                        OnKillsAdded(); break;
                    case nameof(AlertConfig.ZkbVisibleColumns):
                    case nameof(AlertConfig.ZkbColumnOrder):
                        ApplyColumnLayout(); break;
                    case nameof(AlertConfig.ZkbFontSize):
                        ApplyFontSize();
                        AdjustColumnWidths(); break;
                }
            });
        }
    }
}
