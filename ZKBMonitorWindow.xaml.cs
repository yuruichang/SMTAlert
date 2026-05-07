using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
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

            App.ZKillFeed.KillsAddedEvent += OnKillsAdded;
            App.Config.PropertyChanged += OnConfigChanged;
            Closing += ZKBMonitor_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnInitialized(e);
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

            // Region filter only when user enables it (matching original SMT behavior)
            if (App.Config.ZkbFilterByWarningRegion)
            {
                if (c == null || string.IsNullOrEmpty(c.Region))
                    return true; // No active char = show all

                var sys = EveManager.Instance?.GetEveSystem(zs.SystemName);
                if (sys == null || sys.Region != c.Region)
                    return false;
            }

            return true;
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
                        ZKBKillList.Opacity = App.Config.ZkbContentOpacity; break;
                    case nameof(AlertConfig.ZkbMaxKills):
                        _maxKills = App.Config.ZkbMaxKills; break;
                    case nameof(AlertConfig.ZkbFilterByWarningRegion):
                        CollectionViewSource.GetDefaultView(ZKBKillList.ItemsSource)?.Refresh(); break;
                }
            });
        }
    }
}
