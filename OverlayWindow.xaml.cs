using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SMT.EVEData;

namespace SMTAlert
{
    /// <summary>
    /// Alert radar overlay window - shows systems within alert range and highlights warning systems.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        // --- Brushes ---
        private Brush _sysOutlineBrush;
        private Brush _sysLocationBrush;
        private Brush _sysFillBrush;
        private Brush _warningSysBrush;
        private Brush _clearSysBrush;
        private Brush _staleSysBrush;
        private Brush _jumpLineBrush;
        private Brush _outOfRegionOutlineBrush;
        private Brush _outOfRegionFillBrush;

        // --- State ---
        private Dictionary<string, OverlaySystemEntry> _systems = new();
        private List<List<string>> _hierarchyTiers = new();
        private List<Line> _jumpLines = new();
        private int _overlayDepth = 6; // 1 + alertRange
        private bool _gathererMode = false;
        private bool _hunterModeShowFullRegion = true;
        private bool _showSystemNames = false;
        private string _currentLocationCache = "";
        private string _currentRegionCache = "";

        // --- Sizes ---
        private const float SystemSizeGatherer = 20f;
        private const float SystemSizeHunter = 5f;
        private const float CurrentSystemSizeMod = 3f;
        private const float WarningOversize = 10f;

        // --- Timers ---
        private DispatcherTimer _locationTimer;
        private DispatcherTimer _redrawTimer;

        // --- Canvas data ---
        private float _canvasWidth = 100;
        private float _canvasHeight = 100;
        private float _scaleMin = 1f;
        private float _offsetX;
        private float _offsetY;
        private float _minX, _maxX, _minY, _maxY;
        private float _centerX, _centerY;

        // --- Zoom/Pan ---
        private float _userScale = 1f;
        private float _userPanX = 0f;
        private float _userPanY = 0f;
        private bool _isPanning;
        private Point _panStartPos;
        private float _panStartPanX;
        private float _panStartPanY;

        public OverlayWindow()
        {
            InitializeComponent();

            _sysOutlineBrush = new SolidColorBrush(Colors.DarkGray);
            _sysLocationBrush = new SolidColorBrush(Colors.Orange);
            _sysFillBrush = new SolidColorBrush(Colors.Gray);
            _warningSysBrush = new SolidColorBrush(Colors.Red) { Opacity = 0.8 };
            _clearSysBrush = new SolidColorBrush(Colors.LimeGreen) { Opacity = 0.7 };
            _staleSysBrush = new SolidColorBrush(Colors.White) { Opacity = 0.6 };
            _jumpLineBrush = new SolidColorBrush(Colors.White) { Opacity = 0.5f };

            var outColor = Colors.Red;
            outColor.R = (byte)(outColor.R * 0.4);
            outColor.G = (byte)(outColor.G * 0.4);
            outColor.B = (byte)(outColor.B * 0.4);
            _outOfRegionOutlineBrush = new SolidColorBrush(outColor);
            _outOfRegionFillBrush = new SolidColorBrush(Colors.Black);

            // Settings from config
            windowBackground.Opacity = App.Config.OverlayBackgroundOpacity;
            overlay_Canvas.Opacity = App.Config.OverlayContentOpacity;
            _gathererMode = App.Config.OverlayGathererMode;
            _hunterModeShowFullRegion = App.Config.OverlayHunterModeShowFullRegion;
            _showSystemNames = App.Config.OverlayShowSystemNames;

            RefreshButtonStates();

            overlay_Canvas.SizeChanged += (s, e) =>
            {
                _canvasWidth = (float)overlay_Canvas.ActualWidth;
                _canvasHeight = (float)overlay_Canvas.ActualHeight;
                RefreshView();
            };

            Closing += (s, e) => StoreWindowPosition();

            // Position timer: 250ms
            _locationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Normal, (s, e) => CheckPositionChange(), Dispatcher);
            // Redraw timer: 1s
            _redrawTimer = new DispatcherTimer(TimeSpan.FromSeconds(1),
                DispatcherPriority.Normal, (s, e) => RefreshView(), Dispatcher);

            _locationTimer.Start();
            _redrawTimer.Start();

            App.CharacterMgr.CharacterPositionsUpdated += OnCharacterPositionsUpdated;
            App.Config.PropertyChanged += OnConfigChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            LoadWindowPosition();
        }

        private void OnConfigChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(AlertConfig.OverlayBackgroundOpacity):
                        windowBackground.Opacity = App.Config.OverlayBackgroundOpacity; break;
                    case nameof(AlertConfig.OverlayContentOpacity):
                        overlay_Canvas.Opacity = App.Config.OverlayContentOpacity; break;
                    case nameof(AlertConfig.OverlayGathererMode):
                        _gathererMode = App.Config.OverlayGathererMode;
                        RefreshButtonStates(); RefreshView(); break;
                    case nameof(AlertConfig.OverlayHunterModeShowFullRegion):
                        _hunterModeShowFullRegion = App.Config.OverlayHunterModeShowFullRegion;
                        RefreshView(); break;
                    case nameof(AlertConfig.OverlayShowSystemNames):
                        _showSystemNames = App.Config.OverlayShowSystemNames;
                        RefreshView(); break;
                }
            });
        }

        private void OnCharacterPositionsUpdated(CharacterManager mgr)
        {
            // Handled by timer-based CheckPositionChange
        }

        private void CheckPositionChange()
        {
            var c = App.ActiveCharacter;
            var newLoc = c?.Location ?? "";
            var newReg = c?.Region ?? "";

            if (newLoc != _currentLocationCache || newReg != _currentRegionCache)
            {
                _currentLocationCache = newLoc;
                _currentRegionCache = newReg;
                UpdateCharDisplay();
                RefreshView();
            }
        }

        private void UpdateCharDisplay()
        {
            var c = App.ActiveCharacter;
            if (c != null && !string.IsNullOrEmpty(c.Location))
            {
                overlay_CharNameTextblock.Text = $"{c.Name} @ {c.Location}";
                if (c.AlertEnabled)
                {
                    var alertLabel = (string)TryFindResource("Overlay_AlertLabel");
                    var jumpsUnit = (string)TryFindResource("Char_Jumps");
                    overlay_CharNameTextblock.Text += $" [{alertLabel}: {c.AlertRange}{jumpsUnit}]";
                }
            }
            else
            {
                overlay_CharNameTextblock.Text = (string)TryFindResource("Overlay_NoChar");
            }
        }

        private void RefreshView()
        {
            UpdateCharDisplay();

            if (App.ActiveCharacter == null || string.IsNullOrEmpty(App.ActiveCharacter.Location))
            {
                ClearView();
                return;
            }

            _overlayDepth = App.ActiveCharacter.AlertRange + 1;
            CollectSystems();
            DrawAll();
        }

        private void ClearView()
        {
            foreach (var line in _jumpLines)
                overlay_Canvas.Children.Remove(line);
            _jumpLines.Clear();

            foreach (var kvp in _systems)
            {
                if (kvp.Value.Shape != null && overlay_Canvas.Children.Contains(kvp.Value.Shape))
                    overlay_Canvas.Children.Remove(kvp.Value.Shape);
                if (kvp.Value.NameLabel != null && overlay_Canvas.Children.Contains(kvp.Value.NameLabel))
                    overlay_Canvas.Children.Remove(kvp.Value.NameLabel);
                if (kvp.Value.WarningEllipse != null && overlay_Canvas.Children.Contains(kvp.Value.WarningEllipse))
                    overlay_Canvas.Children.Remove(kvp.Value.WarningEllipse);
            }
            _systems.Clear();
            _hierarchyTiers.Clear();
        }

        private void CollectSystems()
        {
            var c = App.ActiveCharacter;
            var currentSys = EveManager.Instance.GetEveSystem(c.Location);
            if (currentSys == null) { ClearView(); return; }

            var visited = new HashSet<string>();
            var warningSet = new HashSet<string>(c.WarningSystems ?? new List<string>());

            // Reset extends for coordinate calculation
            _minX = float.MaxValue; _maxX = float.MinValue;
            _minY = float.MaxValue; _maxY = float.MinValue;

            if (_gathererMode || !_hunterModeShowFullRegion)
            {
                // Pyramid hierarchy: current system at tier 0 (top), each subsequent tier = +1 jump
                _hierarchyTiers = new List<List<string>>();

                // Remove old canvas elements before rebuilding
                foreach (var key in _systems.Keys.ToList())
                {
                    var oldEntry = _systems[key];
                    if (oldEntry.Shape != null && overlay_Canvas.Children.Contains(oldEntry.Shape))
                        overlay_Canvas.Children.Remove(oldEntry.Shape);
                    if (oldEntry.NameLabel != null && overlay_Canvas.Children.Contains(oldEntry.NameLabel))
                        overlay_Canvas.Children.Remove(oldEntry.NameLabel);
                    if (oldEntry.WarningEllipse != null && overlay_Canvas.Children.Contains(oldEntry.WarningEllipse))
                        overlay_Canvas.Children.Remove(oldEntry.WarningEllipse);
                }
                _systems.Clear();

                // Tier 0: current system (top of pyramid)
                _hierarchyTiers.Add(new List<string> { c.Location });
                visited.Add(c.Location);
                _systems[c.Location] = new OverlaySystemEntry
                {
                    EveSystem = currentSys,
                    LayoutCoord = Vector2.Zero,
                    Tier = 0,
                    TierIndex = 0
                };

                // Build subsequent tiers by BFS — systems appear at their shallowest depth
                for (int depth = 1; depth < _overlayDepth; depth++)
                {
                    var currentTier = new List<string>();
                    foreach (var prevSysName in _hierarchyTiers[depth - 1])
                    {
                        var prevSys = EveManager.Instance.GetEveSystem(prevSysName);
                        if (prevSys == null) continue;

                        foreach (var jump in prevSys.Jumps)
                        {
                            if (!visited.Contains(jump))
                            {
                                visited.Add(jump);
                                currentTier.Add(jump);

                                var jumpSys = EveManager.Instance.GetEveSystem(jump);
                                if (jumpSys != null)
                                {
                                    _systems[jump] = new OverlaySystemEntry
                                    {
                                        EveSystem = jumpSys,
                                        LayoutCoord = Vector2.Zero,
                                        Tier = depth,
                                        TierIndex = currentTier.Count - 1
                                    };
                                }
                            }
                        }
                    }
                    _hierarchyTiers.Add(currentTier);
                }
            }
            else
            {
                // Hunter mode: show all systems in current region
                var mr = EveManager.Instance.GetRegion(currentSys.Region);
                if (mr != null)
                {
                    foreach (var ms in mr.MapSystems.Values)
                    {
                        visited.Add(ms.Name);
                        UpdateExtends(ms.Layout);
                        if (!_systems.ContainsKey(ms.Name))
                            _systems[ms.Name] = new OverlaySystemEntry
                            {
                                EveSystem = ms.ActualSystem ?? EveManager.Instance.GetEveSystem(ms.Name),
                                LayoutCoord = ms.Layout
                            };
                    }
                }
            }

            if (!_gathererMode && _hunterModeShowFullRegion)
            {
                // Remove systems no longer in view (hunter mode only)
                var toRemove = _systems.Keys.Where(k => !visited.Contains(k)).ToList();
                foreach (var key in toRemove)
                {
                    var entry = _systems[key];
                    if (entry.Shape != null && overlay_Canvas.Children.Contains(entry.Shape))
                        overlay_Canvas.Children.Remove(entry.Shape);
                    if (entry.NameLabel != null && overlay_Canvas.Children.Contains(entry.NameLabel))
                        overlay_Canvas.Children.Remove(entry.NameLabel);
                    if (entry.WarningEllipse != null && overlay_Canvas.Children.Contains(entry.WarningEllipse))
                        overlay_Canvas.Children.Remove(entry.WarningEllipse);
                    _systems.Remove(key);
                }

                // Compute geographic scaling (hunter mode only)
                ComputeScale();
            }
        }

        private void UpdateExtends(Vector2 coord)
        {
            _minX = Math.Min(_minX, coord.X);
            _maxX = Math.Max(_maxX, coord.X);
            _minY = Math.Min(_minY, coord.Y);
            _maxY = Math.Max(_maxY, coord.Y);
        }

        private void ComputeScale()
        {
            float margin = 30f;
            float rangeX = _maxX - _minX;
            float rangeY = _maxY - _minY;

            if (rangeX < margin * 3) rangeX = margin * 3;
            if (rangeY < margin * 3) rangeY = margin * 3;

            float scaleX = (_canvasWidth - margin * 2) / rangeX;
            float scaleY = (_canvasHeight - margin * 2) / rangeY;
            _scaleMin = Math.Min(scaleX, scaleY);
            _centerX = (_minX + _maxX) / 2f;
            _centerY = (_minY + _maxY) / 2f;
            _offsetX = _canvasWidth / 2f;
            _offsetY = _canvasHeight / 2f;
        }

        private Vector2 LayoutToCanvas(Vector2 coord)
        {
            float x = _offsetX + _userPanX + (coord.X - _centerX) * _scaleMin * _userScale;
            float y = _offsetY + _userPanY + (coord.Y - _centerY) * _scaleMin * _userScale;
            return new Vector2(x, y);
        }

        private void DrawAll()
        {
            var c = App.ActiveCharacter;
            var warningSet = new HashSet<string>();
            var clearSet = new HashSet<string>();
            var staleSet = new HashSet<string>();

            // Build intel-based warning/clear/stale sets directly from IntelDataList.
            // This mirrors the original SMT Overlay.xaml.cs UpdateIntelData approach.
            // All systems on screen with intel are marked; alert SOUND is handled
            // separately in CharacterManager.OnIntelForAlert (BFS-range check).
            var intelList = EveManager.Instance?.IntelDataList;
            if (intelList != null)
            {
                var now = DateTime.UtcNow;
                var freshCutoff = now.AddMinutes(-5);
                var oldCutoff = now.AddMinutes(-10);

                lock (intelList)
                {
                    // Iterate oldest-first so newer intel overrides older.
                    // FixedQueue inserts at index 0 (newest first).
                    for (int idx = intelList.Count - 1; idx >= 0; idx--)
                    {
                        var intel = intelList[idx];
                        if (intel.IntelTime < oldCutoff) continue;
                        bool isFresh = intel.IntelTime >= freshCutoff;

                        foreach (var sysName in intel.Systems)
                        {
                            if (!_systems.ContainsKey(sysName)) continue;

                            // Always update tooltip text for every system that has intel (with time prefix)
                            var mgr = App.CharacterMgr;
                            if (mgr != null)
                                mgr.SystemLastIntelText[sysName] = $"[{intel.IntelTime:HH:mm:ss}] {intel.IntelString.Trim()}";

                            if (intel.ClearNotification)
                            {
                                warningSet.Remove(sysName);
                                staleSet.Remove(sysName);
                                clearSet.Add(sysName);
                            }
                            else if (isFresh)
                            {
                                clearSet.Remove(sysName);
                                warningSet.Add(sysName);
                            }
                            else
                            {
                                if (!warningSet.Contains(sysName) && !clearSet.Contains(sysName))
                                    staleSet.Add(sysName);
                            }
                        }
                    }
                }
            }

            // Also merge character-level lists from OnWarningTick (character-based clear tracking etc.)
            if (c != null)
            {
                foreach (var s in c.WarningSystems ?? new List<string>())
                    warningSet.Add(s);
                foreach (var s in c.ClearSystems ?? new List<string>())
                    { if (!warningSet.Contains(s)) clearSet.Add(s); }
                foreach (var s in c.StaleSystems ?? new List<string>())
                    { if (!warningSet.Contains(s) && !clearSet.Contains(s)) staleSet.Add(s); }
            }

            // Resolve priority: warning > stale > clear
            var finalStaleSet = new HashSet<string>(staleSet);
            var finalClearSet = new HashSet<string>(clearSet);
            finalStaleSet.ExceptWith(warningSet);
            finalClearSet.ExceptWith(warningSet);
            finalClearSet.ExceptWith(finalStaleSet);

            // Clear old jump lines
            foreach (var line in _jumpLines)
                overlay_Canvas.Children.Remove(line);
            _jumpLines.Clear();

            // Pre-compute canvas positions based on mode
            if (_gathererMode || !_hunterModeShowFullRegion)
            {
                // Pyramid layout: tier 0 at top, expanding downward like a pyramid
                int totalTiers = _hierarchyTiers.Count;
                if (totalTiers > 0)
                {
                    float rowHeight = _canvasHeight / totalTiers;
                    for (int t = 0; t < _hierarchyTiers.Count; t++)
                    {
                        var tierSystems = _hierarchyTiers[t];
                        int sysCount = tierSystems.Count;
                        if (sysCount == 0) continue;
                        float colWidth = _canvasWidth / sysCount;
                        for (int si = 0; si < tierSystems.Count; si++)
                        {
                            string sysName = tierSystems[si];
                            if (!_systems.TryGetValue(sysName, out var sysEntry)) continue;

                            float centerX = colWidth / 2f + colWidth * si;
                            float centerY = rowHeight / 2f + rowHeight * t;

                            // Apply zoom/pan relative to canvas center
                            float x = _canvasWidth / 2f + _userPanX + (centerX - _canvasWidth / 2f) * _userScale;
                            float y = _canvasHeight / 2f + _userPanY + (centerY - _canvasHeight / 2f) * _userScale;

                            sysEntry.CanvasCoord = new Vector2(x, y);
                        }
                    }
                }
            }
            else
            {
                // Hunter mode: geographic layout
                foreach (var kvp in _systems)
                {
                    kvp.Value.CanvasCoord = LayoutToCanvas(kvp.Value.LayoutCoord);
                }
            }

            // Track drawn connections
            var drawnConnections = new HashSet<string>();

            foreach (var kvp in _systems)
            {
                var entry = kvp.Value;
                var sys = entry.EveSystem ?? EveManager.Instance.GetEveSystem(kvp.Key);
                if (sys == null) continue;

                var canvasPos = entry.CanvasCoord;
                float sysSize = GetSystemSize(kvp.Key);
                bool isCurrent = kvp.Key == c?.Location;
                bool isWarning = warningSet.Contains(kvp.Key);
                bool isStale = !isWarning && finalStaleSet.Contains(kvp.Key);
                bool isClear = !isWarning && !isStale && finalClearSet.Contains(kvp.Key);

                // Draw system shape
                if (entry.Shape == null)
                {
                    entry.Shape = new Ellipse();
                    overlay_Canvas.Children.Add(entry.Shape);
                }
                entry.Shape.Width = sysSize;
                entry.Shape.Height = sysSize;
                entry.Shape.Fill = isCurrent ? new SolidColorBrush(Colors.Orange) : _sysFillBrush;
                entry.Shape.Stroke = isCurrent ? _sysLocationBrush :
                    isWarning ? _warningSysBrush :
                    isStale ? _staleSysBrush :
                    isClear ? _clearSysBrush : _sysOutlineBrush;
                entry.Shape.StrokeThickness = isWarning || isStale || isClear ? 2.5f : (_gathererMode ? 2f : 1f);
                Canvas.SetLeft(entry.Shape, canvasPos.X - sysSize / 2);
                Canvas.SetTop(entry.Shape, canvasPos.Y - sysSize / 2);
                Canvas.SetZIndex(entry.Shape, 100);

                // Tooltip: create proper ToolTip object with immediate show delay
                if (entry.Shape.ToolTip == null)
                {
                    entry.Shape.ToolTip = new ToolTip
                    {
                        Background = Brushes.Black,
                        Foreground = Brushes.DarkGray,
                        Opacity = 0.85
                    };
                    ToolTipService.SetInitialShowDelay(entry.Shape, 0);
                }
                string tipText = null;
                App.CharacterMgr?.SystemLastIntelText?.TryGetValue(kvp.Key, out tipText);
                ((ToolTip)entry.Shape.ToolTip).Content = string.IsNullOrEmpty(tipText)
                    ? $"{kvp.Key} ({sys.TrueSec:n2})"
                    : tipText;

                // Draw highlight for warning/stale/clear
                if (isWarning || isStale || isClear)
                {
                    if (entry.WarningEllipse == null)
                    {
                        entry.WarningEllipse = new Ellipse
                        {
                            IsHitTestVisible = false  // Don't block mouse events on the system ellipse
                        };
                        overlay_Canvas.Children.Add(entry.WarningEllipse);
                    }
                    entry.WarningEllipse.Width = sysSize + WarningOversize;
                    entry.WarningEllipse.Height = sysSize + WarningOversize;
                    entry.WarningEllipse.Stroke = isWarning ? _warningSysBrush :
                        isStale ? _staleSysBrush : _clearSysBrush;
                    entry.WarningEllipse.StrokeThickness = 2f;
                    entry.WarningEllipse.Fill = isWarning
                        ? new SolidColorBrush(Colors.Red) { Opacity = 0.15f }
                        : isStale
                            ? new SolidColorBrush(Colors.White) { Opacity = 0.10f }
                            : new SolidColorBrush(Colors.LimeGreen) { Opacity = 0.12f };
                    Canvas.SetLeft(entry.WarningEllipse, canvasPos.X - (sysSize + WarningOversize) / 2);
                    Canvas.SetTop(entry.WarningEllipse, canvasPos.Y - (sysSize + WarningOversize) / 2);
                    Canvas.SetZIndex(entry.WarningEllipse, 90);
                }
                else if (entry.WarningEllipse != null)
                {
                    overlay_Canvas.Children.Remove(entry.WarningEllipse);
                    entry.WarningEllipse = null;
                }

                // Draw system name
                if (_showSystemNames)
                {
                    if (entry.NameLabel == null)
                    {
                        entry.NameLabel = new TextBlock
                        {
                            Foreground = Brushes.White,
                            FontSize = 9,
                            TextAlignment = TextAlignment.Center,
                            IsHitTestVisible = false,
                            Width = 80
                        };
                        overlay_Canvas.Children.Add(entry.NameLabel);
                    }
                    entry.NameLabel.Text = kvp.Key;
                    Canvas.SetLeft(entry.NameLabel, canvasPos.X - 40);
                    Canvas.SetTop(entry.NameLabel, canvasPos.Y + sysSize / 2 + 2);
                    Canvas.SetZIndex(entry.NameLabel, 125);
                }
                else if (entry.NameLabel != null)
                {
                    overlay_Canvas.Children.Remove(entry.NameLabel);
                    entry.NameLabel = null;
                }

                // Draw jump connections
                foreach (var jumpName in sys.Jumps)
                {
                    if (!_systems.ContainsKey(jumpName)) continue;
                    var connKey = string.Compare(kvp.Key, jumpName) < 0
                        ? $"{kvp.Key}|{jumpName}" : $"{jumpName}|{kvp.Key}";
                    if (drawnConnections.Contains(connKey)) continue;
                    drawnConnections.Add(connKey);

                    var targetEntry = _systems[jumpName];
                    var targetPos = targetEntry.CanvasCoord;

                    var line = new Line
                    {
                        X1 = canvasPos.X, Y1 = canvasPos.Y,
                        X2 = targetPos.X, Y2 = targetPos.Y,
                        Stroke = _jumpLineBrush,
                        StrokeThickness = 0.5f,
                        IsHitTestVisible = false
                    };
                    Canvas.SetZIndex(line, 50);
                    overlay_Canvas.Children.Add(line);
                    _jumpLines.Add(line);
                }

                entry.CanvasCoord = canvasPos;
            }
        }

        private float GetSystemSize(string sysName)
        {
            if (_gathererMode) return SystemSizeGatherer;
            if (sysName == App.ActiveCharacter?.Location) return SystemSizeHunter * CurrentSystemSizeMod;
            return SystemSizeHunter;
        }

        private void RefreshButtonStates()
        {
            overlay_HunterButton.Visibility = _gathererMode ? Visibility.Collapsed : Visibility.Visible;
            overlay_GathererButton.Visibility = _gathererMode ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Canvas mouse events (zoom/pan) ---

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(overlay_Canvas);
            float oldScale = _userScale;
            float zoomStep = 0.1f;

            if (e.Delta > 0)
                _userScale = Math.Min(_userScale + zoomStep, 5f);
            else
                _userScale = Math.Max(_userScale - zoomStep, 0.2f);

            // Zoom centered on cursor position
            _userPanX -= (float)((pos.X - _offsetX - _userPanX) * (_userScale - oldScale) / oldScale);
            _userPanY -= (float)((pos.Y - _offsetY - _userPanY) * (_userScale - oldScale) / oldScale);

            DrawAll();
            e.Handled = true;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                _userScale = 1f;
                _userPanX = 0f;
                _userPanY = 0f;
                DrawAll();
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                _isPanning = true;
                _panStartPos = e.GetPosition(overlay_Canvas);
                _panStartPanX = _userPanX;
                _panStartPanY = _userPanY;
                overlay_Canvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var pos = e.GetPosition(overlay_Canvas);
                _userPanX = _panStartPanX + (float)(pos.X - _panStartPos.X);
                _userPanY = _panStartPanY + (float)(pos.Y - _panStartPos.Y);
                DrawAll();
                e.Handled = true;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isPanning)
            {
                _isPanning = false;
                overlay_Canvas.ReleaseMouseCapture();
            }
            e.Handled = true;
        }

        // --- Window events ---
        private void Overlay_Window_Move(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ResizeMode = ResizeMode.NoResize;
                DragMove();
                ResizeMode = ResizeMode.CanResizeWithGrip;
            }
            e.Handled = true;
        }

        private void Overlay_Window_Close(object sender, MouseButtonEventArgs e) => Close();

        private void Overlay_ToggleGathererMode(object sender, MouseButtonEventArgs e)
        {
            _gathererMode = true;
            App.Config.OverlayGathererMode = true;
            RefreshButtonStates();
            RefreshView();
        }

        private void Overlay_ToggleHunterMode(object sender, MouseButtonEventArgs e)
        {
            _gathererMode = false;
            App.Config.OverlayGathererMode = false;
            RefreshButtonStates();
            RefreshView();
        }

        // --- Window position persistence ---
        private void LoadWindowPosition()
        {
            string placement = Properties.Settings.Default.OverlayWindow_placement;
            if (!string.IsNullOrEmpty(placement))
            {
                WindowPlacement.SetPlacement(new WindowInteropHelper(this).Handle, placement);
            }
        }

        private void StoreWindowPosition()
        {
            Properties.Settings.Default.OverlayWindow_placement =
                WindowPlacement.GetPlacement(new WindowInteropHelper(this).Handle);
            Properties.Settings.Default.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            _locationTimer?.Stop();
            _redrawTimer?.Stop();
            App.CharacterMgr.CharacterPositionsUpdated -= OnCharacterPositionsUpdated;
            App.Config.PropertyChanged -= OnConfigChanged;
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Entry for a system drawn on the overlay canvas.
    /// </summary>
    public class OverlaySystemEntry
    {
        public SMT.EVEData.System EveSystem;
        public Vector2 LayoutCoord;
        public Vector2 CanvasCoord;
        public int Tier;
        public int TierIndex;
        public Ellipse Shape;
        public Ellipse WarningEllipse;
        public TextBlock NameLabel;
    }
}
