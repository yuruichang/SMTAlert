using System.ComponentModel;
using System.Xml.Serialization;
using EVEDataUtils;

namespace SMTAlert
{
    /// <summary>
    /// Independent configuration for SMTAlert application.
    /// Stored as XML in %AppData%/SMTAlert/AlertConfig.xml
    /// </summary>
    public class AlertConfig : INotifyPropertyChanged
    {
        private static readonly string StorageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTAlert");
        private static readonly string ConfigFile = Path.Combine(StorageRoot, "AlertConfig.xml");

        // --- Language ---
        private string _language = "en-US";
        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(nameof(Language)); }
        }

        // --- Overlay ---
        private float _overlayBackgroundOpacity = 0.2f;
        public float OverlayBackgroundOpacity
        {
            get => _overlayBackgroundOpacity;
            set { _overlayBackgroundOpacity = Math.Clamp(value, 0.05f, 1.0f); OnPropertyChanged(nameof(OverlayBackgroundOpacity)); }
        }

        private float _overlayContentOpacity = 0.5f;
        public float OverlayContentOpacity
        {
            get => _overlayContentOpacity;
            set { _overlayContentOpacity = Math.Clamp(value, 0.05f, 1.0f); OnPropertyChanged(nameof(OverlayContentOpacity)); }
        }

        private bool _overlayGathererMode = false;
        public bool OverlayGathererMode
        {
            get => _overlayGathererMode;
            set { _overlayGathererMode = value; OnPropertyChanged(nameof(OverlayGathererMode)); }
        }

        private bool _overlayHunterModeShowFullRegion = true;
        public bool OverlayHunterModeShowFullRegion
        {
            get => _overlayHunterModeShowFullRegion;
            set { _overlayHunterModeShowFullRegion = value; OnPropertyChanged(nameof(OverlayHunterModeShowFullRegion)); }
        }

        private bool _overlayShowSystemNames = false;
        public bool OverlayShowSystemNames
        {
            get => _overlayShowSystemNames;
            set { _overlayShowSystemNames = value; OnPropertyChanged(nameof(OverlayShowSystemNames)); }
        }

        // --- ZKB Monitor ---
        private float _zkbBackgroundOpacity = 0.2f;
        public float ZkbBackgroundOpacity
        {
            get => _zkbBackgroundOpacity;
            set { _zkbBackgroundOpacity = Math.Clamp(value, 0.05f, 1.0f); OnPropertyChanged(nameof(ZkbBackgroundOpacity)); }
        }

        private float _zkbContentOpacity = 0.85f;
        public float ZkbContentOpacity
        {
            get => _zkbContentOpacity;
            set { _zkbContentOpacity = Math.Clamp(value, 0.1f, 1.0f); OnPropertyChanged(nameof(ZkbContentOpacity)); }
        }

        private int _zkbMaxKills = 50;
        public int ZkbMaxKills
        {
            get => _zkbMaxKills;
            set { _zkbMaxKills = Math.Clamp(value, 10, 200); OnPropertyChanged(nameof(ZkbMaxKills)); }
        }

        private int _zkbExpireMinutes = 30;
        public int ZkbExpireMinutes
        {
            get => _zkbExpireMinutes;
            set { _zkbExpireMinutes = Math.Max(value, 5); OnPropertyChanged(nameof(ZkbExpireMinutes)); }
        }

        private bool _zkbFilterByWarningRegion = true;
        public bool ZkbFilterByWarningRegion
        {
            get => _zkbFilterByWarningRegion;
            set { _zkbFilterByWarningRegion = value; OnPropertyChanged(nameof(ZkbFilterByWarningRegion)); }
        }

        private string _zkbCustomSystems = "";
        public string ZkbCustomSystems
        {
            get => _zkbCustomSystems;
            set { _zkbCustomSystems = value ?? ""; OnPropertyChanged(nameof(ZkbCustomSystems)); }
        }

        private string _zkbMonitoredCharacterIDs = "";
        public string ZkbMonitoredCharacterIDs
        {
            get => _zkbMonitoredCharacterIDs;
            set { _zkbMonitoredCharacterIDs = value ?? ""; OnPropertyChanged(nameof(ZkbMonitoredCharacterIDs)); }
        }

        private string _zkbMonitoredCorpIDs = "";
        public string ZkbMonitoredCorpIDs
        {
            get => _zkbMonitoredCorpIDs;
            set { _zkbMonitoredCorpIDs = value ?? ""; OnPropertyChanged(nameof(ZkbMonitoredCorpIDs)); }
        }

        private string _zkbVisibleColumns = "Time,System,Corp,Alliance,ShipType,Value,CharacterID,Region";
        public string ZkbVisibleColumns
        {
            get => _zkbVisibleColumns;
            set { _zkbVisibleColumns = value ?? "Time,System,Corp,Alliance,ShipType,Value,CharacterID,Region"; OnPropertyChanged(nameof(ZkbVisibleColumns)); }
        }

        private string _zkbColumnOrder = "Time,Region,System,Corp,Alliance,CharacterID,AttackerAlliance,ShipType,Value";
        public string ZkbColumnOrder
        {
            get => _zkbColumnOrder;
            set { _zkbColumnOrder = value ?? "Time,Region,System,Corp,Alliance,CharacterID,AttackerAlliance,ShipType,Value"; OnPropertyChanged(nameof(ZkbColumnOrder)); }
        }

        private bool _zkbUseLocalTime = false;
        public bool ZkbUseLocalTime
        {
            get => _zkbUseLocalTime;
            set { _zkbUseLocalTime = value; OnPropertyChanged(nameof(ZkbUseLocalTime)); }
        }

        private int _zkbFontSize = 10;
        public int ZkbFontSize
        {
            get => _zkbFontSize;
            set { _zkbFontSize = Math.Clamp(value, 8, 24); OnPropertyChanged(nameof(ZkbFontSize)); }
        }

        // --- Always on top ---
        private bool _alwaysOnTop = true;
        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set { _alwaysOnTop = value; OnPropertyChanged(nameof(AlwaysOnTop)); }
        }

        // --- Minimize to tray ---
        private bool _minimizeToTray = true;
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set { _minimizeToTray = value; OnPropertyChanged(nameof(MinimizeToTray)); }
        }

        // --- EVE log folder ---
        private string _eveLogFolder = "";
        public string EveLogFolder
        {
            get => _eveLogFolder;
            set { _eveLogFolder = value ?? ""; OnPropertyChanged(nameof(EveLogFolder)); }
        }

        // --- Alert channel ---
        private string _alertChannelName = "";
        public string AlertChannelName
        {
            get => _alertChannelName;
            set { _alertChannelName = value ?? ""; OnPropertyChanged(nameof(AlertChannelName)); }
        }

        private string _alertClearKeywords = "";
        public string AlertClearKeywords
        {
            get => _alertClearKeywords;
            set { _alertClearKeywords = value ?? ""; OnPropertyChanged(nameof(AlertClearKeywords)); }
        }
        // --- Alert sound volume ---
        private float _alertVolume = 1.0f;
        public float AlertVolume
        {
            get => _alertVolume;
            set { _alertVolume = Math.Clamp(value, 0.0f, 1.0f); OnPropertyChanged(nameof(AlertVolume)); }
        }

        // --- Alert timing ---
        private int _alertFreshMinutes = 5;
        public int AlertFreshMinutes
        {
            get => _alertFreshMinutes;
            set { _alertFreshMinutes = Math.Clamp(value, 1, 30); OnPropertyChanged(nameof(AlertFreshMinutes)); }
        }

        private int _alertExpireMinutes = 10;
        public int AlertExpireMinutes
        {
            get => _alertExpireMinutes;
            set { _alertExpireMinutes = Math.Clamp(value, 1, 60); OnPropertyChanged(nameof(AlertExpireMinutes)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static AlertConfig Load()
        {
            if (!Directory.Exists(StorageRoot))
                Directory.CreateDirectory(StorageRoot);

            var config = Serialization.DeserializeFromDisk<AlertConfig>(ConfigFile);
            if (config == null)
                return new AlertConfig();

            // Migrate old configs: ensure Corp column is in order and visible lists
            bool migrated = false;
            if (!string.IsNullOrEmpty(config.ZkbColumnOrder) &&
                !config.ZkbColumnOrder.Contains("Corp", StringComparison.OrdinalIgnoreCase))
            {
                // Protect AttackerAlliance before replacing Alliance
                config.ZkbColumnOrder = config.ZkbColumnOrder
                    .Replace("AttackerAlliance", "__PROTECTED_ATTACKER__", StringComparison.OrdinalIgnoreCase)
                    .Replace("Alliance,", "Alliance,Corp,", StringComparison.OrdinalIgnoreCase)
                    .Replace("__PROTECTED_ATTACKER__", "AttackerAlliance");
                migrated = true;
            }
            if (!string.IsNullOrEmpty(config.ZkbVisibleColumns) &&
                !config.ZkbVisibleColumns.Contains("Corp", StringComparison.OrdinalIgnoreCase))
            {
                config.ZkbVisibleColumns += ",Corp";
                migrated = true;
            }

            if (migrated)
            {
                try { config.Save(); } catch { /* Migration save is best-effort; will retry on next startup */ }
            }

            return config;
        }

        public void Save()
        {
            if (!Directory.Exists(StorageRoot))
                Directory.CreateDirectory(StorageRoot);
            Serialization.SerializeToDisk(this, ConfigFile);
        }
    }
}
