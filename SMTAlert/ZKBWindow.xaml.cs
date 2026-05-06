using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SMT.EVEData;

namespace SMTAlert
{
    public partial class ZKBWindow : Window
    {
        private AlertManager _manager;
        public bool ForceClose { get; set; }

        public ZKBWindow(AlertManager manager)
        {
            InitializeComponent();
            _manager = manager;
            ZKBList.ItemsSource = _manager.FilteredKillStream;

            SourceInitialized += (s, e) =>
            {
                var placement = Properties.Settings.Default.ZKBWindow_placement;
                if (!string.IsNullOrEmpty(placement))
                    WindowPlacement.SetPlacement(new WindowInteropHelper(this).Handle, placement);
                ApplyOpacity();
            };
            Closing += ZKBWindow_Closing;
        }

        public void ApplyOpacity()
        {
            var cfg = _manager?.Config;
            if (cfg == null) return;
            float bg = Math.Clamp(cfg.ZKBBackgroundOpacity, 0.05f, 1.0f);
            float content = Math.Clamp(cfg.ZKBContentOpacity, 0.1f, 1.0f);
            byte bgAlpha = (byte)(bg * 255);

            // Background layer: window chrome, title bar
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(bgAlpha, 26, 26, 26));
            TitleBar.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(bgAlpha, 26, 26, 26));

            // Content layer: kill list
            ZKBList.Opacity = content;
        }

        private void ZKBList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Select the row under the cursor before context menu opens
            var element = e.OriginalSource as System.Windows.DependencyObject;
            while (element != null)
            {
                if (element is System.Windows.Controls.ListViewItem item)
                {
                    item.IsSelected = true;
                    break;
                }
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }
        }

        private void OpenZKBLink_Click(object sender, RoutedEventArgs e)
        {
            var kill = ZKBList.SelectedItem as ZKillRedisQ.ZKBDataSimple;
            if (kill?.KillID == 0) return;
            string url = $"https://zkillboard.com/kill/{kill.KillID}/";
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ZKBWindow_Closing(object sender, CancelEventArgs e)
        {
            SavePlacement();
            if (!ForceClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void SavePlacement()
        {
            Properties.Settings.Default.ZKBWindow_placement =
                WindowPlacement.GetPlacement(new WindowInteropHelper(this).Handle);
            Properties.Settings.Default.Save();
        }
    }
}
