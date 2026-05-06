using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Windows.Threading;
using SMT.EVEData;
using SMTAlert.Models;
using Media = System.Windows.Media;
using WinCtl = System.Windows.Controls;

namespace SMTAlert
{
    public partial class IntelAlertWindow : Window
    {
        private AlertManager _manager;
        private DispatcherTimer _mapPollTimer;
        public bool ForceClose { get; set; }

        // Map state
        private bool _isRegionView;
        private string _lastMapLocation;
        private int _lastMapRange;
        private List<MapSystemInfo> _mapSystems = new();
        private Dictionary<string, Ellipse> _systemDots = new();
        private List<IntelOverlayEntry> _activeIntelOverlays = new();

        // Zoom state
        private double _zoomLevel = 1.0;
        private const double MinZoom = 0.3;
        private const double MaxZoom = 3.0;
        private const double ZoomStep = 0.15;

        // Pan-drag state
        private bool _isPanning;
        private System.Windows.Point _panStart;
        private double _panOffsetX;
        private double _panOffsetY;

        public IntelAlertWindow(AlertManager manager)
        {
            InitializeComponent();
            _manager = manager;

            _manager.IntelEntries.CollectionChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                RefreshIntelOverlays();
                DrawStarMap();
            });
            _manager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AlertManager.ActiveCharacter))
                    Dispatcher.Invoke(() => DrawStarMap());
            };

            // Poll for location/range changes
            _mapPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _mapPollTimer.Tick += (s, e) =>
            {
                var ch = _manager?.ActiveCharacter;
                string curLoc = ch?.Location;
                int curRange = _manager?.Config?.AlertRange ?? 5;
                if (curLoc != _lastMapLocation || curRange != _lastMapRange)
                {
                    if (MapCanvas != null && MapCanvas.ActualWidth > 10 && MapCanvas.ActualHeight > 10)
                        DrawStarMap();
                }
                else if (_mapSystems.Count > 0)
                {
                    RefreshIntelOverlays();
                    UpdateIntelMarkers();
                }
            };
            _mapPollTimer.Start();

            Loaded += (s, e) =>
            {
                RegionViewBtn.IsChecked = false;
                RangeViewBtn.IsChecked = true;
                _isRegionView = false;
                ApplyOpacity();
                DrawStarMap();
            };

            SourceInitialized += (s, e) =>
            {
                var placement = Properties.Settings.Default.IntelWindow_placement;
                if (!string.IsNullOrEmpty(placement))
                    WindowPlacement.SetPlacement(new WindowInteropHelper(this).Handle, placement);
                ApplyOpacity();
                DrawStarMap();
            };
            Closing += IntelAlertWindow_Closing;
        }

        public void ApplyOpacity()
        {
            var cfg = _manager?.Config;
            if (cfg == null) return;
            float bg = Math.Clamp(cfg.IntelBackgroundOpacity, 0.05f, 1.0f);
            float content = Math.Clamp(cfg.IntelContentOpacity, 0.1f, 1.0f);
            byte bgAlpha = (byte)(bg * 255);

            // Background layer: window chrome, title bar, toolbar, map background
            Background = new Media.SolidColorBrush(Media.Color.FromArgb(bgAlpha, 26, 26, 26));
            TitleBar.Background = new Media.SolidColorBrush(Media.Color.FromArgb(bgAlpha, 26, 26, 26));
            ToolbarBorder.Background = new Media.SolidColorBrush(Media.Color.FromArgb(bgAlpha, 30, 30, 30));
            MapBackground.Background = new Media.SolidColorBrush(Media.Color.FromArgb(bgAlpha, 21, 21, 21));

            // Content layer: star map canvas (circles, lines, labels), legend, info
            MapCanvas.Opacity = content;
            MapLegend.Opacity = content;
            MapInfoLabel.Opacity = content;
        }

        private void ViewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (sender == RangeViewBtn && RangeViewBtn.IsChecked == true)
            {
                RegionViewBtn.IsChecked = false;
                _isRegionView = false;
                _panOffsetX = 0;
                _panOffsetY = 0;
                DrawStarMap();
            }
            else if (sender == RegionViewBtn && RegionViewBtn.IsChecked == true)
            {
                RangeViewBtn.IsChecked = false;
                _isRegionView = true;
                _panOffsetX = 0;
                _panOffsetY = 0;
                DrawStarMap();
            }
            else
            {
                // Reject uncheck of the only selected button
                if (sender == RangeViewBtn && RegionViewBtn.IsChecked == false)
                    RangeViewBtn.IsChecked = true;
                if (sender == RegionViewBtn && RangeViewBtn.IsChecked == false)
                    RegionViewBtn.IsChecked = true;
            }
        }


        private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawStarMap();
        }

        // Pan-drag handling
        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPanning = true;
            _panStart = e.GetPosition(MapCanvas);
            MapCanvas.CaptureMouse();
        }

        private void MapCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanning) return;
            var pos = e.GetPosition(MapCanvas);
            _panOffsetX += pos.X - _panStart.X;
            _panOffsetY += pos.Y - _panStart.Y;
            _panStart = pos;
            DrawStarMap();
        }

        private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            MapCanvas.ReleaseMouseCapture();
        }

        public void DrawStarMap()
        {
            var canvas = MapCanvas;
            if (canvas == null || canvas.ActualWidth < 10 || canvas.ActualHeight < 10) return;

            var mgr = _manager;
            if (mgr == null) return;

            canvas.Children.Clear();
            _systemDots.Clear();
            _mapSystems.Clear();

            if (_isRegionView)
                DrawRegionMap(canvas, mgr);
            else
                DrawRangeMap(canvas, mgr);

            UpdateIntelMarkers();
        }

        private void DrawRegionMap(WinCtl.Canvas canvas, AlertManager mgr)
        {
            string regionName = mgr.ActiveCharacter?.Region;
            if (string.IsNullOrEmpty(regionName))
            {
                MapInfoLabel.Text = "Waiting for character region...";
                return;
            }

            RegionNameLabel.Text = regionName;
            var eveManager = mgr.EveManager;
            if (eveManager?.Systems == null) return;

            // Collect all systems in the current region
            var regionSystems = eveManager.Systems
                .Where(s => string.Equals(s.Region, regionName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (regionSystems.Count == 0)
            {
                MapInfoLabel.Text = $"No systems found in {regionName}";
                return;
            }

            MapInfoLabel.Text = $"{regionName} — {regionSystems.Count} systems";

            // Build name set for jump filtering
            var regionNames = new HashSet<string>(regionSystems.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

            // Collect map data
            foreach (var sys in regionSystems)
            {
                _mapSystems.Add(new MapSystemInfo
                {
                    Name = sys.Name,
                    X = (double)sys.ActualX,
                    Y = (double)sys.ActualY,
                    Region = sys.Region,
                    Jumps = sys.Jumps,
                    Security = sys.TrueSec
                });
            }

            if (_mapSystems.Count == 0) return;

            // Compute bounds
            ComputeBounds(out double minX, out double minY, out double maxX, out double maxY);
            double scale = ComputeScale(canvas, minX, minY, maxX, maxY, out double offsetX, out double offsetY);
            scale *= _zoomLevel;

            var nameToPoint = BuildPointMap(minX, minY, scale, offsetX, offsetY);

            // Draw gates (only to systems also on map)
            foreach (var ms in _mapSystems)
            {
                if (!nameToPoint.TryGetValue(ms.Name, out var p1)) continue;
                foreach (var connName in ms.Jumps)
                {
                    if (!nameToPoint.TryGetValue(connName, out var p2)) continue;
                    if (string.Compare(ms.Name, connName, StringComparison.OrdinalIgnoreCase) > 0) continue;

                    bool isRegionGate = !string.Equals(ms.Region,
                        _mapSystems.FirstOrDefault(x => string.Equals(x.Name, connName, StringComparison.OrdinalIgnoreCase))?.Region,
                        StringComparison.OrdinalIgnoreCase);

                    var line = new Line
                    {
                        X1 = p1.X, Y1 = p1.Y,
                        X2 = p2.X, Y2 = p2.Y,
                        Stroke = new Media.SolidColorBrush(isRegionGate
                            ? Media.Color.FromRgb(160, 110, 110)
                            : Media.Color.FromRgb(180, 180, 180)),
                        StrokeThickness = isRegionGate ? 0.8 : 0.6,
                        Opacity = isRegionGate ? 0.6 : 0.65
                    };
                    WinCtl.Panel.SetZIndex(line, 0);
                    canvas.Children.Add(line);
                }
            }

            // Draw systems
            string homeSystem = mgr.ActiveCharacter?.Location;
            foreach (var ms in _mapSystems)
            {
                if (!nameToPoint.TryGetValue(ms.Name, out var pt)) continue;

                bool isHome = string.Equals(ms.Name, homeSystem, StringComparison.OrdinalIgnoreCase);
                double radius = isHome ? 24 : 18;
                int zBase = isHome ? 10 : 1;

                var dot = CreateSystemDot(ms.Name, pt, radius, zBase, isHome, eveManager);
                canvas.Children.Add(dot);
                _systemDots[ms.Name] = dot;

                // Label
                var label = new WinCtl.TextBlock
                {
                    Text = ms.Name,
                    FontSize = isHome ? 9 : 8,
                    Foreground = isHome
                        ? new Media.SolidColorBrush(Media.Color.FromRgb(240, 190, 10))
                        : new Media.SolidColorBrush(Media.Color.FromArgb(180, 180, 180, 180)),
                    FontWeight = isHome ? FontWeights.Bold : FontWeights.Normal,
                    IsHitTestVisible = false
                };
                label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double labelW = label.DesiredSize.Width;
                WinCtl.Canvas.SetLeft(label, pt.X - labelW / 2);
                WinCtl.Canvas.SetTop(label, pt.Y + radius + 1);
                WinCtl.Panel.SetZIndex(label, zBase);
                canvas.Children.Add(label);
            }
        }

        private void DrawRangeMap(WinCtl.Canvas canvas, AlertManager mgr)
        {
            var eveManager = mgr.EveManager;
            if (eveManager?.Systems == null) return;

            var character = mgr.ActiveCharacter;
            string location = character?.Location;
            int range = mgr.Config?.AlertRange ?? 5;

            _lastMapLocation = location;
            _lastMapRange = range;

            MapInfoLabel.Text = "";
            if (string.IsNullOrEmpty(location))
            {
                MapInfoLabel.Text = "Waiting for character location...";
                return;
            }

            // BFS to build tree: depth + parent for each system
            var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var children = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            int maxDepth = 0;

            var queue = new Queue<string>();
            depth[location] = 0;
            parent[location] = null;
            children[location] = new List<string>();
            queue.Enqueue(location);

            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                int d = depth[cur];
                if (d > maxDepth) maxDepth = d;
                if (d >= range) continue;

                var sys = eveManager.GetEveSystem(cur);
                if (sys == null) continue;

                foreach (string neighbor in sys.Jumps)
                {
                    if (depth.ContainsKey(neighbor)) continue;

                    depth[neighbor] = d + 1;
                    parent[neighbor] = cur;
                    children[neighbor] = new List<string>();
                    children[cur].Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            MapInfoLabel.Text = $"{depth.Count} systems within {range} jumps of {location}";

            // Layout parameters
            double padH = 24;
            double padTop = 24;
            double padBottom = 24;
            double canvasW = canvas.ActualWidth - padH * 2;
            double canvasH = canvas.ActualHeight - padTop - padBottom;
            double nodeRadius = 18 * _zoomLevel;
            // Level height always fills the canvas; zoom only affects circle/text size
            double levelHeight = maxDepth > 0 ? Math.Min(canvasH / (maxDepth + 1), 90) : canvasH;

            // Group nodes by depth
            var nodesByLevel = new Dictionary<int, List<string>>();
            foreach (var kv in depth)
            {
                if (!nodesByLevel.ContainsKey(kv.Value))
                    nodesByLevel[kv.Value] = new List<string>();
                nodesByLevel[kv.Value].Add(kv.Key);
            }

            // Sort: children near their parent's horizontal position
            var nodePos = new Dictionary<string, System.Windows.Point>(StringComparer.OrdinalIgnoreCase);
            var levelOrder = new Dictionary<int, List<string>>();

            // Level 0: just the root
            levelOrder[0] = new List<string> { location };

            // For each subsequent level, order children by their parent's order in the previous level
            for (int lvl = 1; lvl <= maxDepth; lvl++)
            {
                levelOrder[lvl] = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string parentNode in levelOrder[lvl - 1])
                {
                    if (children.TryGetValue(parentNode, out var kids))
                    {
                        foreach (string kid in kids)
                        {
                            if (seen.Add(kid))
                                levelOrder[lvl].Add(kid);
                        }
                    }
                }
            }

            // Assign positions: each level spans full canvas width, evenly spaced
            for (int lvl = 0; lvl <= maxDepth; lvl++)
            {
                var nodes = levelOrder.TryGetValue(lvl, out var list) ? list : new List<string>();
                int count = nodes.Count;
                double y = padTop + lvl * levelHeight;

                if (count == 1)
                {
                    nodePos[nodes[0]] = new System.Windows.Point(padH + canvasW / 2, y);
                }
                else
                {
                    double spacing = canvasW / (count - 1);
                    for (int i = 0; i < count; i++)
                    {
                        double x = padH + i * spacing;
                        nodePos[nodes[i]] = new System.Windows.Point(x, y);
                    }
                }
            }

            // Build mapSystem list for intel overlay
            foreach (var kv in nodePos)
            {
                var sys = eveManager.GetEveSystem(kv.Key);
                _mapSystems.Add(new MapSystemInfo
                {
                    Name = kv.Key,
                    X = kv.Value.X,
                    Y = kv.Value.Y,
                    Region = sys?.Region ?? "",
                    Jumps = sys?.Jumps ?? new List<string>(),
                    Security = sys?.TrueSec ?? 0
                });
            }

            // Draw edges (parent -> child)
            foreach (var kv in children)
            {
                if (!nodePos.TryGetValue(kv.Key, out var p1)) continue;
                foreach (string childName in kv.Value)
                {
                    if (!nodePos.TryGetValue(childName, out var p2)) continue;

                    var sysParent = eveManager.GetEveSystem(kv.Key);
                    var sysChild = eveManager.GetEveSystem(childName);
                    bool isRegionGate = sysParent != null && sysChild != null &&
                        !string.Equals(sysParent.Region, sysChild.Region, StringComparison.OrdinalIgnoreCase);

                    var line = new Line
                    {
                        X1 = p1.X, Y1 = p1.Y + nodeRadius,
                        X2 = p2.X, Y2 = p2.Y - nodeRadius,
                        Stroke = new Media.SolidColorBrush(isRegionGate
                            ? Media.Color.FromRgb(160, 110, 110)
                            : Media.Color.FromRgb(180, 180, 180)),
                        StrokeThickness = isRegionGate ? 0.8 : 0.6,
                        Opacity = isRegionGate ? 0.6 : 0.65
                    };
                    WinCtl.Panel.SetZIndex(line, 0);
                    canvas.Children.Add(line);
                }
            }

            // Draw system dots and labels
            foreach (var kv in nodePos)
            {
                string sysName = kv.Key;
                var pt = kv.Value;
                bool isHome = string.Equals(sysName, location, StringComparison.OrdinalIgnoreCase);
                double radius = isHome ? 24 * _zoomLevel : nodeRadius;
                int zBase = isHome ? 10 : 1;

                var dot = CreateSystemDot(sysName, pt, radius, zBase, isHome, eveManager);
                canvas.Children.Add(dot);
                _systemDots[sysName] = dot;

                int d = depth.TryGetValue(sysName, out int dd) ? dd : 0;

                var label = new WinCtl.TextBlock
                {
                    Text = sysName,
                    FontSize = Math.Max(7, (isHome ? 9 : 8) * _zoomLevel),
                    Foreground = isHome
                        ? new Media.SolidColorBrush(Media.Color.FromRgb(240, 190, 10))
                        : new Media.SolidColorBrush(Media.Color.FromArgb(180, 180, 180, 180)),
                    FontWeight = isHome ? FontWeights.Bold : FontWeights.Normal,
                    IsHitTestVisible = false
                };
                label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                // Center label below the circle
                WinCtl.Canvas.SetLeft(label, pt.X - label.DesiredSize.Width / 2);
                WinCtl.Canvas.SetTop(label, pt.Y + radius + 1);
                WinCtl.Panel.SetZIndex(label, zBase);
                canvas.Children.Add(label);
            }
        }

        private Ellipse CreateSystemDot(string systemName, System.Windows.Point pt, double radius, int zBase, bool isHome, EveManager eveManager)
        {
            var dot = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = isHome
                    ? new Media.SolidColorBrush(Media.Color.FromRgb(240, 190, 10))
                    : new Media.SolidColorBrush(Media.Color.FromRgb(80, 80, 80)),
                Stroke = isHome
                    ? new Media.SolidColorBrush(Media.Color.FromRgb(255, 255, 255))
                    : new Media.SolidColorBrush(Media.Color.FromRgb(60, 60, 60)),
                StrokeThickness = isHome ? 1.5 : 0.5,
                Opacity = 0.9,
                Tag = systemName
            };
            WinCtl.Canvas.SetLeft(dot, pt.X - radius);
            WinCtl.Canvas.SetTop(dot, pt.Y - radius);
            WinCtl.Panel.SetZIndex(dot, zBase);
            return dot;
        }

        private void ComputeBounds(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = double.MaxValue; minY = double.MaxValue;
            maxX = double.MinValue; maxY = double.MinValue;
            foreach (var ms in _mapSystems)
            {
                if (ms.X < minX) minX = ms.X;
                if (ms.Y < minY) minY = ms.Y;
                if (ms.X > maxX) maxX = ms.X;
                if (ms.Y > maxY) maxY = ms.Y;
            }
        }

        private double ComputeScale(WinCtl.Canvas canvas, double minX, double minY, double maxX, double maxY, out double offsetX, out double offsetY)
        {
            double dataW = maxX - minX;
            double dataH = maxY - minY;
            if (dataW < 1) dataW = 1;
            if (dataH < 1) dataH = 1;

            double marginLeft = 12;
            double marginRight = 12;
            double marginTop = 12;
            double marginBottom = 20;

            double canvasW = canvas.ActualWidth - marginLeft - marginRight;
            double canvasH = canvas.ActualHeight - marginTop - marginBottom;
            double scale = Math.Min(canvasW / dataW, canvasH / dataH);
            offsetX = marginLeft + _panOffsetX + (canvasW - dataW * scale) / 2;
            offsetY = marginTop + _panOffsetY + (canvasH - dataH * scale) / 2;
            return scale;
        }

        private Dictionary<string, System.Windows.Point> BuildPointMap(double minX, double minY, double scale, double offsetX, double offsetY)
        {
            var map = new Dictionary<string, System.Windows.Point>(StringComparer.OrdinalIgnoreCase);
            foreach (var ms in _mapSystems)
            {
                double px = offsetX + (ms.X - minX) * scale;
                double py = offsetY + (ms.Y - minY) * scale;
                map[ms.Name] = new System.Windows.Point(px, py);
            }
            return map;
        }

        private Media.Color GetSecColor(string systemName, EveManager eveManager)
        {
            var sys = eveManager?.GetEveSystem(systemName);
            if (sys == null) return Media.Color.FromRgb(100, 100, 100);

            double sec = sys.TrueSec;
            if (sec <= 0.0) return Media.Color.FromRgb(139, 35, 0);
            if (sec <= 0.2) return Media.Color.FromRgb(178, 34, 34);
            if (sec <= 0.4) return Media.Color.FromRgb(205, 133, 0);
            if (sec <= 0.6) return Media.Color.FromRgb(180, 180, 60);
            if (sec <= 0.8) return Media.Color.FromRgb(80, 160, 80);
            if (sec <= 0.95) return Media.Color.FromRgb(60, 140, 200);
            return Media.Color.FromRgb(80, 120, 220);
        }

        private void RefreshIntelOverlays()
        {
            _activeIntelOverlays.Clear();
            if (_manager == null) return;

            var now = DateTime.UtcNow;
            // First pass: collect per-system info, prioritizing clear status
            // Skip entries older than 10 minutes
            var systemStates = new Dictionary<string, IntelOverlayEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _manager.IntelEntries)
            {
                if (entry.Systems == null) continue;
                if ((now - entry.IntelTime).TotalMinutes > 10) continue;
                bool entryIsClear = entry.ClearNotification;
                foreach (var sysName in entry.Systems)
                {
                    if (!systemStates.TryGetValue(sysName, out var existing))
                    {
                        systemStates[sysName] = new IntelOverlayEntry
                        {
                            SystemName = sysName,
                            IsClear = entryIsClear,
                            Time = entry.IntelTime
                        };
                    }
                    else
                    {
                        // Clear flag always wins once set
                        if (entryIsClear)
                            existing.IsClear = true;
                        // Keep the most recent time
                        if (entry.IntelTime > existing.Time)
                            existing.Time = entry.IntelTime;
                    }
                }
            }
            foreach (var ov in systemStates.Values)
                _activeIntelOverlays.Add(ov);
        }

        private void UpdateIntelMarkers()
        {
            if (MapCanvas == null || _mapSystems.Count == 0) return;
            var now = DateTime.UtcNow;

            foreach (var dot in _systemDots.Values)
            {
                if (dot.Tag is not string sysName) continue;

                var overlay = _activeIntelOverlays.FirstOrDefault(o =>
                    string.Equals(o.SystemName, sysName, StringComparison.OrdinalIgnoreCase));

                if (overlay != null)
                {
                    double age = (now - overlay.Time).TotalSeconds;

                    if (overlay.IsClear)
                    {
                        // Cleared → orange thick border
                        dot.Stroke = new Media.SolidColorBrush(Media.Color.FromRgb(255, 140, 0));
                        dot.StrokeThickness = 2.5;
                    }
                    else if (age < 60)
                    {
                        // Active alert < 1 min → red thick border
                        dot.Stroke = new Media.SolidColorBrush(Media.Color.FromRgb(191, 54, 12));
                        dot.StrokeThickness = 2.5;
                    }
                    else
                    {
                        // Active alert >= 1 min, no clr → white thick border
                        dot.Stroke = new Media.SolidColorBrush(Media.Color.FromRgb(220, 220, 220));
                        dot.StrokeThickness = 2.5;
                    }
                    dot.Opacity = 1.0;
                }
                else
                {
                    // Default → gray thin border
                    dot.Stroke = new Media.SolidColorBrush(Media.Color.FromRgb(60, 60, 60));
                    dot.StrokeThickness = 0.5;
                    dot.Opacity = 0.9;
                }
            }
        }

        // Zoom handlers

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = Math.Min(_zoomLevel + ZoomStep, MaxZoom);
            UpdateZoomLabel();
            DrawStarMap();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = Math.Max(_zoomLevel - ZoomStep, MinZoom);
            UpdateZoomLabel();
            DrawStarMap();
        }

        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            _zoomLevel = Math.Clamp(_zoomLevel + delta, MinZoom, MaxZoom);
            UpdateZoomLabel();
            DrawStarMap();
            e.Handled = true;
        }

        private void UpdateZoomLabel()
        {
            if (ZoomLabel != null)
                ZoomLabel.Text = $"{(int)(_zoomLevel * 100)}%";
        }

        // Event handlers

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void ClearIntel_Click(object sender, RoutedEventArgs e)
        {
            _manager.IntelEntries.Clear();
            _activeIntelOverlays.Clear();
            DrawStarMap();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void IntelAlertWindow_Closing(object sender, CancelEventArgs e)
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
            Properties.Settings.Default.IntelWindow_placement =
                WindowPlacement.GetPlacement(new WindowInteropHelper(this).Handle);
            Properties.Settings.Default.Save();
        }
    }

    internal class MapSystemInfo
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Region { get; set; }
        public double Security { get; set; }
        public List<string> Jumps { get; set; } = new();
    }

    internal class IntelOverlayEntry
    {
        public string SystemName { get; set; }
        public bool IsClear { get; set; }
        public DateTime Time { get; set; }
    }
}
