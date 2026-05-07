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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static AlertConfig Load()
        {
            if (!Directory.Exists(StorageRoot))
                Directory.CreateDirectory(StorageRoot);

            var config = Serialization.DeserializeFromDisk<AlertConfig>(ConfigFile);
            return config ?? new AlertConfig();
        }

        public void Save()
        {
            if (!Directory.Exists(StorageRoot))
                Directory.CreateDirectory(StorageRoot);
            Serialization.SerializeToDisk(this, ConfigFile);
        }
    }
}
