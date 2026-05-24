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

            // Load persisted column widths
            Dispatcher.BeginInvoke(new Action(() => LoadColumnWidths()));

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
            StoreColumnWidths();
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

        private void LoadColumnWidths()
        {
            try
            {
                string saved = Properties.Settings.Default.ZKB_ColumnWidths;
                if (string.IsNullOrEmpty(saved)) return;
                var parts = saved.Split('|');
                var columns = ZKBKillList.Columns;
                for (int i = 0; i < parts.Length && i < columns.Count; i++)
                {
                    if (double.TryParse(parts[i], out double w) && w > 0)
                    {
                        columns[i].Width = new DataGridLength(w, DataGridLengthUnitType.Pixel);
                        if (i == 1 || i == 2 || i == 3) // System, Alliance, ShipType are star-sized
                            columns[i].Width = new DataGridLength(w, DataGridLengthUnitType.Star);
                    }
                }
            }
            catch { }
        }

        private void StoreColumnWidths()
        {
            try
            {
                var widths = new List<string>();
                foreach (var col in ZKBKillList.Columns)
                {
                    widths.Add(col.ActualWidth.ToString("F0"));
                }
                Properties.Settings.Default.ZKB_ColumnWidths = string.Join("|", widths);
                Properties.Settings.Default.Save();
            }
            catch { }
        }

        private void ApplyHeaderOpacity(double opacity)
        {
            foreach (var header in FindVisualChildren<DataGridColumnHeader>(ZKBKillList))
            {
                header.Opacity = opacity;
            }
        }

        private void OnKillsAdded()
        {
            Dispatcher.Invoke(() =>
            {
                CollectionViewSource.GetDefaultView(ZKBKillList.ItemsSource)?.Refresh();
            });
        }

        private bool ZKillFilter(object item)
        {
            var zs = item as SMT.EVEData.ZKillRedisQ.ZKBDataSimple;
            if (zs == null) return false;

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
                        CollectionViewSource.GetDefaultView(ZKBKillList.ItemsSource)?.Refresh(); break;
                }
            });
        }
    }
}
