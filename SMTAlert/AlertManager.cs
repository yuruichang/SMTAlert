using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using NAudio.Wave;
using SMT.EVEData;
using SMTAlert.Models;
using Application = System.Windows.Application;

namespace SMTAlert
{
    public class AlertManager : INotifyPropertyChanged
    {
        private static AlertManager _instance;
        public static AlertManager Instance => _instance;

        private EveManager _eveManager;
        public EveManager EveManager => _eveManager;
        private AlertConfig _config;
        private FileSystemWatcher _intelWatcher;

        public ObservableCollection<LocalCharacter> Characters { get; } = new();

        private LocalCharacter _activeCharacter;
        public LocalCharacter ActiveCharacter
        {
            get => _activeCharacter;
            set { _activeCharacter = value; OnPropertyChanged(nameof(ActiveCharacter)); }
        }

        public ObservableCollection<IntelAlertEntry> IntelEntries { get; } = new();
        public ObservableCollection<ZKillRedisQ.ZKBDataSimple> FilteredKillStream { get; } = new();
        public List<ZKillRedisQ.ZKBDataSimple> KillStream => _eveManager?.ZKillFeed?.KillStream;

        private WaveOutEvent _waveOut;
        private AudioFileReader _audioFile;
        private Dictionary<string, int> _intelFileReadPos = new();
        private Dictionary<string, DateTime> _recentIntelHashes = new(); // dedup: hash -> last seen time
        private readonly object _recentIntelLock = new();
        private HttpListener _esiListener;
        private string _esiLoginChallenge;
        private bool _zkbStarted;

        private List<string> _intelChannelFilters = new() { "Intel", "intel", "Int" };
        private List<string> _intelAlertKeywords = new();
        private List<string> _intelClearKeywords = new() { "clear", "Clear", "clr", "Clr" };

        public string ChannelFiltersText
        {
            get => string.Join(",", _intelChannelFilters);
            set
            {
                _intelChannelFilters.Clear();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = part.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            _intelChannelFilters.Add(trimmed);
                    }
                }
                if (_intelChannelFilters.Count == 0)
                    _intelChannelFilters.Add("Intel");
                SaveIntelFilters();
            }
        }

        public string AlertKeywordsText
        {
            get => string.Join(",", _intelAlertKeywords);
            set
            {
                _intelAlertKeywords.Clear();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = part.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            _intelAlertKeywords.Add(trimmed);
                    }
                }
                SaveIntelFilters();
            }
        }

        public string ClearKeywordsText
        {
            get => string.Join(",", _intelClearKeywords);
            set
            {
                _intelClearKeywords.Clear();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = part.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            _intelClearKeywords.Add(trimmed);
                    }
                }
                if (_intelClearKeywords.Count == 0)
                {
                    _intelClearKeywords.Add("clear");
                    _intelClearKeywords.Add("clr");
                }
                SaveIntelFilters();
            }
        }

        private void SaveIntelFilters()
        {
            string root = EveAppConfig.StorageRoot;
            try
            {
                File.WriteAllLines(Path.Combine(root, "IntelChannels.txt"), _intelChannelFilters);
                File.WriteAllLines(Path.Combine(root, "IntelAlertFilters.txt"), _intelAlertKeywords);
                File.WriteAllLines(Path.Combine(root, "IntelClearFilters.txt"), _intelClearKeywords);
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<int> IntelAlertTriggered;

        public AlertConfig Config
        {
            get => _config;
            set { _config = value; OnPropertyChanged(nameof(Config)); }
        }

        public AlertManager() { _instance = this; }

        public void Initialize()
        {
            LoadConfig();

            _eveManager = new EveManager(EveAppConfig.SMT_VERSION);
            EveManager.Instance = _eveManager;
            _eveManager.LoadFromDisk();
            _eveManager.InitNavigation();

            // ZKB feed is NOT started here — it starts when a character has a known region.
            _eveManager.ZKillFeed = new ZKillRedisQ();
            _eveManager.ZKillFeed.KillExpireTimeMinutes = _config.ZKBExpireMinutes;
            _eveManager.ZKillFeed.KillsAddedEvent += OnKillsAdded;

            LoadCharacters();
            SetupAudio();
            LoadIntelFilters();
            SetupIntelWatcher();
            StartEsiListener();
            _eveManager.CharacterAddedViaEsi += OnCharacterAddedViaEsi;
        }

        private void SetupAudio()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "woop.mp3");
                if (!File.Exists(soundPath)) return;
                _waveOut = new WaveOutEvent { DeviceNumber = -1, Volume = _config.IntelSoundVolume };
                _audioFile = new AudioFileReader(soundPath);
                _waveOut.Init(_audioFile);
            }
            catch { }
        }

        public void PlayAlertSound()
        {
            if (!_config.PlayIntelSound) return;
            try
            {
                if (_waveOut == null) return;
                if (_waveOut.PlaybackState == PlaybackState.Playing) return;
                _audioFile.Position = 0;
                _waveOut.Play();
            }
            catch { }
        }

        public void UpdateVolume()
        {
            if (_waveOut != null)
                _waveOut.Volume = Config.IntelSoundVolume;
        }

        private string ConfigPath => Path.Combine(EveAppConfig.StorageRoot, "SMTAlert_Config.dat");

        public void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var xs = new XmlSerializer(typeof(AlertConfig));
                    using var fs = new FileStream(ConfigPath, FileMode.Open);
                    _config = (AlertConfig)xs.Deserialize(fs);
                }
                catch { _config = new AlertConfig(); }
            }
            else { _config = new AlertConfig(); }
        }

        public void SaveConfig()
        {
            try
            {
                var xs = new XmlSerializer(typeof(AlertConfig));
                using var tw = new StreamWriter(ConfigPath);
                xs.Serialize(tw, _config);
            }
            catch { }
        }

        public void LoadCharacters()
        {
            string charFolder = Path.Combine(EveAppConfig.StorageRoot, "Characters");
            if (!Directory.Exists(charFolder)) return;

            foreach (var file in Directory.GetFiles(charFolder, "*.dat"))
            {
                try
                {
                    var xs = new XmlSerializer(typeof(LocalCharacter));
                    using var fs = new FileStream(file, FileMode.Open);
                    var c = (LocalCharacter)xs.Deserialize(fs);
                    if (c != null)
                    {
                        c.Location = "";
                        c.IsOnline = false;
                        Application.Current.Dispatcher.Invoke(() => Characters.Add(c));
                    }
                }
                catch { }
            }
        }

        private void LoadIntelFilters()
        {
            string root = EveAppConfig.StorageRoot;
            string channelFile = Path.Combine(root, "IntelChannels.txt");
            string alertFile = Path.Combine(root, "IntelAlertFilters.txt");
            string clearFile = Path.Combine(root, "IntelClearFilters.txt");

            if (File.Exists(channelFile))
            {
                _intelChannelFilters.Clear();
                foreach (var line in File.ReadAllLines(channelFile))
                    if (!string.IsNullOrWhiteSpace(line))
                        _intelChannelFilters.Add(line.Trim());
            }

            if (File.Exists(alertFile))
            {
                _intelAlertKeywords.Clear();
                foreach (var line in File.ReadAllLines(alertFile))
                    if (!string.IsNullOrWhiteSpace(line))
                        _intelAlertKeywords.Add(line.Trim());
            }

            if (File.Exists(clearFile))
            {
                _intelClearKeywords.Clear();
                foreach (var line in File.ReadAllLines(clearFile))
                    if (!string.IsNullOrWhiteSpace(line))
                        _intelClearKeywords.Add(line.Trim());
            }
        }

        private void SetupIntelWatcher()
        {
            string logFolder = _eveManager.EVELogFolder;
            if (string.IsNullOrEmpty(logFolder))
            {
                string evePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "EVE", "logs", "Chatlogs");
                if (Directory.Exists(evePath))
                    logFolder = evePath;
            }

            if (string.IsNullOrEmpty(logFolder) || !Directory.Exists(logFolder))
            {
                Debug.WriteLine("Intel log folder not found");
                return;
            }

            _eveManager.EVELogFolder = logFolder;

            foreach (var file in Directory.GetFiles(logFolder, "*.txt"))
                ReadIntelFile(file);

            // Don't show historical intel from before app startup on the star map
            IntelEntries.Clear();
            lock (_recentIntelLock)
                _recentIntelHashes.Clear();

            _intelWatcher = new FileSystemWatcher(logFolder, "*.txt")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };
            _intelWatcher.Changed += OnIntelFileChanged;
            _intelWatcher.Created += OnIntelFileChanged;
            _intelWatcher.Error += OnIntelWatcherError;
        }

        private void OnIntelWatcherError(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine($"Intel FileSystemWatcher error: {e.GetException()?.Message}");
            // Re-initialize the watcher on error (common with buffer overflow)
            try
            {
                _intelWatcher?.Dispose();
                _intelWatcher = null;
                SetupIntelWatcher();
            }
            catch { }
        }

        private DateTime _lastIntelRescan = DateTime.MinValue;

        /// <summary>
        /// Periodic fallback rescan of intel log folder to catch files missed by FileSystemWatcher.
        /// Called from PeriodicCleanup (every 1s via UiTimer).
        /// </summary>
        private void RescanIntelFiles()
        {
            var now = DateTime.Now;
            if ((now - _lastIntelRescan).TotalSeconds < 1.5) return;
            _lastIntelRescan = now;

            string logFolder = _eveManager?.EVELogFolder;
            if (string.IsNullOrEmpty(logFolder) || !Directory.Exists(logFolder)) return;

            try
            {
                foreach (var file in Directory.GetFiles(logFolder, "*.txt"))
                    ReadIntelFile(file);
            }
            catch { }
        }

        private void OnIntelFileChanged(object sender, FileSystemEventArgs e)
        {
            ReadIntelFile(e.FullPath);
        }

        private void ReadIntelFile(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                bool isIntelChannel = false;
                foreach (var filter in _intelChannelFilters)
                {
                    if (!string.IsNullOrWhiteSpace(filter) && fileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    { isIntelChannel = true; break; }
                }
                if (!isIntelChannel) return;

                // Clean channel name: remove .txt extension and trailing _YYYYMMDD[_HHMMSS[_...]] timestamp segments
                string channelName = Path.GetFileNameWithoutExtension(fileName);
                // Repeatedly strip trailing _<all-digits> segments (handles multi-segment timestamps like _20260506_110517_422161660)
                while (true)
                {
                    int usIdx = channelName.LastIndexOf('_');
                    if (usIdx <= 0) break;
                    string suffix = channelName.Substring(usIdx + 1);
                    if (suffix.Length > 0 && suffix.All(char.IsDigit))
                        channelName = channelName.Substring(0, usIdx);
                    else
                        break;
                }

                _intelFileReadPos.TryGetValue(filePath, out int lastPosition);

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length <= lastPosition) return;

                fs.Seek(lastPosition, SeekOrigin.Begin);
                using var sr = new StreamReader(fs);
                string newContent = sr.ReadToEnd();
                _intelFileReadPos[filePath] = (int)fs.Length;

                string[] lines = newContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (string line in lines)
                    ProcessIntelLine(line, channelName);
            }
            catch { }
        }

        private void ProcessIntelLine(string line, string channelFile)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Parse game timestamp: [YYYY.MM.DD HH:MM:SS]
            // EVE game time is UTC — store as UTC for display, compare with UtcNow
            DateTime gameTime = DateTime.UtcNow;
            var timeMatch = _gameTimeRegex.Match(line);
            if (timeMatch.Success)
            {
                DateTime.TryParseExact(timeMatch.Groups[1].Value, "yyyy.MM.dd HH:mm:ss",
                    null, System.Globalization.DateTimeStyles.AssumeUniversal, out gameTime);
            }

            int gtIdx = line.IndexOf('>');
            if (gtIdx < 0) return;

            string intelText = line.Substring(gtIdx + 1).Trim();
            if (string.IsNullOrWhiteSpace(intelText)) return;

            // Dedup: skip identical intel from multiple game clients within 3 seconds
            string dedupKey = channelFile + "|" + intelText;
            var now = DateTime.Now;
            lock (_recentIntelLock)
            {
                if (_recentIntelHashes.TryGetValue(dedupKey, out var lastSeen))
                {
                    if ((now - lastSeen).TotalSeconds < 3) return;
                }
                _recentIntelHashes[dedupKey] = now;
            }

            bool isClear = false;
            foreach (var kw in _intelClearKeywords)
            {
                if (!string.IsNullOrWhiteSpace(kw) && intelText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                { isClear = true; break; }
            }

            var systems = ExtractSystemNames(intelText);
            if (systems.Count == 0) return;

            var id = new IntelData(line, channelFile, gameTime)
            {
                Systems = systems,
                ClearNotification = isClear
            };

            bool inRange = false;
            foreach (var sysName in systems)
            {
                if (IsSystemInRange(sysName))
                { inRange = true; break; }
            }

            bool alertKeyword = false;
            foreach (var kw in _intelAlertKeywords)
            {
                if (!string.IsNullOrWhiteSpace(kw) && intelText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                { alertKeyword = true; break; }
            }

            var entry = new IntelAlertEntry
            {
                Data = id,
                InAlertRange = inRange
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                while (IntelEntries.Count > 50)
                    IntelEntries.RemoveAt(IntelEntries.Count - 1);
                IntelEntries.Insert(0, entry);
            });

            if (inRange && !isClear)
            {
                IntelAlertTriggered?.Invoke(alertKeyword ? 2 : 1);
            }
        }

        // Range cache
        private HashSet<string> _systemsInRange = new();

        public void UpdateRangeCache()
        {
            _systemsInRange.Clear();
            if (ActiveCharacter == null || string.IsNullOrEmpty(ActiveCharacter.Location))
                return;

            try
            {
                var inRange = Navigation.GetSystemsXJumpsFrom(
                    new List<string>(), ActiveCharacter.Location, Config.AlertRange);
                if (inRange != null)
                {
                    foreach (var s in inRange)
                        _systemsInRange.Add(s);
                }
            }
            catch { }
        }

        public bool IsSystemInRange(string systemName)
        {
            return _systemsInRange.Contains(systemName);
        }

        public void CleanupOldEntries()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-Config.MaxIntelSeconds);
            for (int i = IntelEntries.Count - 1; i >= 0; i--)
            {
                if (IntelEntries[i].IntelTime < cutoff)
                    IntelEntries.RemoveAt(i);
            }
        }

        private int _cleanupCounter;
        public void PeriodicCleanup()
        {
            _cleanupCounter++;
            if (_cleanupCounter % 60 == 0)
            {
                var deadKeys = _intelFileReadPos.Keys.Where(k => !File.Exists(k)).ToList();
                foreach (var key in deadKeys)
                    _intelFileReadPos.Remove(key);
            }
            // Clean up stale dedup hashes (older than 10 seconds)
            var cutoff = DateTime.Now.AddSeconds(-10);
            List<string> staleHashes;
            lock (_recentIntelLock)
            {
                staleHashes = _recentIntelHashes.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
                foreach (var key in staleHashes)
                    _recentIntelHashes.Remove(key);
            }

            // Periodic fallback rescan for intel files (catches missed by FileSystemWatcher)
            RescanIntelFiles();
        }

        private static readonly Regex _gameTimeRegex = new(@"^\[\s*(\d{4}\.\d{2}\.\d{2}\s+\d{2}:\d{2}:\d{2})\s*\]", RegexOptions.Compiled);
        private static readonly char[] _systemNameTrimChars = { '*', '.', ',', '>', '<', '(', ')', '[', ']', ':', ';', '!', '?' };

        private List<string> ExtractSystemNames(string text)
        {
            var result = new List<string>();
            if (_eveManager?.Systems == null) return result;

            var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                var cleaned = word.Trim(_systemNameTrimChars);
                if (string.IsNullOrEmpty(cleaned)) continue;

                var sys = _eveManager.GetEveSystem(cleaned);
                if (sys != null && !result.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
                    result.Add(cleaned);
            }
            return result;
        }

        private void OnKillsAdded()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SyncFilteredKillStream();
                ResolveUnknownAlliances();
            });
        }

        private async void ResolveUnknownAlliances()
        {
            var source = KillStream;
            if (source == null || _eveManager?.EveApiClient == null) return;
            var unknownIds = new List<int>();
            foreach (var kill in source)
            {
                if (kill.VictimAllianceID != 0 && string.IsNullOrEmpty(_eveManager.GetAllianceName(kill.VictimAllianceID)))
                {
                    if (!unknownIds.Contains(kill.VictimAllianceID))
                        unknownIds.Add(kill.VictimAllianceID);
                }
            }
            if (unknownIds.Count > 0)
            {
                await _eveManager.ResolveAllianceIDs(unknownIds);
                // Refresh alliance names
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var kill in FilteredKillStream)
                    {
                        string name = _eveManager.GetAllianceName(kill.VictimAllianceID);
                        if (!string.IsNullOrEmpty(name))
                            kill.VictimAllianceName = name;
                    }
                });
            }
        }

        public void StartZKBFeed()
        {
            if (_zkbStarted) return;
            if (_eveManager?.ZKillFeed == null) return;
            _eveManager.ZKillFeed.Initialise();
            _zkbStarted = true;
            Debug.WriteLine("ZKB feed started");
        }

        public void StopZKBFeed()
        {
            if (!_zkbStarted) return;
            _eveManager?.ZKillFeed?.ShutDown();
            _zkbStarted = false;
            Application.Current.Dispatcher.Invoke(() => FilteredKillStream.Clear());
            Debug.WriteLine("ZKB feed stopped");
        }

        public void TryAutoStartZKB()
        {
            if (_zkbStarted) return;
            if (ActiveCharacter == null) return;
            if (string.IsNullOrEmpty(ActiveCharacter.Region)) return;
            StartZKBFeed();
        }

        private void SyncFilteredKillStream()
        {
            var source = KillStream;
            if (source == null) return;

            string currentRegion = ActiveCharacter?.Region;

            // Remove entries no longer in source
            for (int i = FilteredKillStream.Count - 1; i >= 0; i--)
            {
                if (!source.Contains(FilteredKillStream[i]))
                    FilteredKillStream.RemoveAt(i);
            }

            // Add new entries from source (source is newest-first)
            foreach (var kill in source)
            {
                if (FilteredKillStream.Contains(kill)) continue;
                if (Config.ZKBFilterByRegion && !string.IsNullOrEmpty(currentRegion))
                {
                    var sys = _eveManager?.GetEveSystem(kill.SystemName);
                    if (sys == null || sys.Region != currentRegion) continue;
                }
                // Insert maintaining newest-first order
                int insertIdx = 0;
                FilteredKillStream.Insert(insertIdx, kill);
            }

            // Enforce max count
            while (FilteredKillStream.Count > Config.ZKBMaxKills)
                FilteredKillStream.RemoveAt(FilteredKillStream.Count - 1);
        }

        // ESI Character management

        public void StartAddCharacter()
        {
            if (_eveManager?.Sso == null) return;
            _esiLoginChallenge = Guid.NewGuid().ToString("N");
            string url = _eveManager.GetESILogonURL(_esiLoginChallenge);
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private void OnCharacterAddedViaEsi(LocalCharacter esiChar)
        {
            var existing = FindCharacterByName(esiChar.Name);
            if (existing != null)
            {
                // Update existing character's ESI tokens
                existing.ESILinked = esiChar.ESILinked;
                existing.ESIAccessToken = esiChar.ESIAccessToken;
                existing.ESIAccessTokenExpiry = esiChar.ESIAccessTokenExpiry;
                existing.ESIRefreshToken = esiChar.ESIRefreshToken;
                existing.ID = esiChar.ID;
                existing.ESIScopesStored = esiChar.ESIScopesStored;
                SaveCharacter(existing);
                return;
            }

            Application.Current.Dispatcher.Invoke(() => Characters.Add(esiChar));
            SaveCharacter(esiChar);
        }

        public LocalCharacter FindCharacterByName(string name)
        {
            foreach (var c in Characters)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        public void SaveCharacter(LocalCharacter c)
        {
            try
            {
                string charFolder = Path.Combine(EveAppConfig.StorageRoot, "Characters");
                Directory.CreateDirectory(charFolder);
                string filePath = Path.Combine(charFolder, $"{c.Name}.dat");
                var xs = new XmlSerializer(typeof(LocalCharacter));
                using var tw = new StreamWriter(filePath);
                xs.Serialize(tw, c);
            }
            catch { }
        }

        public void RemoveCharacter(LocalCharacter c)
        {
            Application.Current.Dispatcher.Invoke(() => Characters.Remove(c));
            try
            {
                string filePath = Path.Combine(EveAppConfig.StorageRoot, "Characters", $"{c.Name}.dat");
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { }
        }

        private void StartEsiListener()
        {
            try
            {
                _esiListener = new HttpListener();
                _esiListener.Prefixes.Add("http://localhost:8762/callback/");
                _esiListener.Start();
                _esiListener.BeginGetContext(OnEsiCallback, null);
            }
            catch
            {
                Debug.WriteLine("Failed to start ESI listener on http://localhost:8762/callback/");
            }
        }

        private void StopEsiListener()
        {
            try { _esiListener?.Stop(); } catch { }
        }

        private async void OnEsiCallback(IAsyncResult ar)
        {
            try
            {
                var ctx = _esiListener.EndGetContext(ar);
                // Continue listening
                _esiListener.BeginGetContext(OnEsiCallback, null);

                string challenge = _esiLoginChallenge;
                _esiLoginChallenge = null;

                var esiChar = await _eveManager.HandleEsiAuthCallback(ctx.Request.Url, challenge ?? string.Empty);

                string responseHtml = esiChar != null
                    ? "<html><body><h2>ESI Login Successful</h2><p>You may close this window.</p></body></html>"
                    : "<html><body><h2>ESI Login Failed</h2><p>Please try again. You may close this window.</p></body></html>";

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
                ctx.Response.ContentType = "text/html";
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                ctx.Response.Close();
            }
            catch { }
        }

        public void Shutdown()
        {
            SaveConfig();
            _intelWatcher?.Dispose();
            _eveManager?.ZKillFeed?.ShutDown();
            _waveOut?.Dispose();
            _audioFile?.Dispose();
            StopEsiListener();
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
