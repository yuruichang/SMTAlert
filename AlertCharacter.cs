using System.ComponentModel;
using System.Xml.Serialization;
using EVEStandard.Models.API;

namespace SMTAlert
{
    /// <summary>
    /// Independent character model for SMTAlert.
    /// Handles ESI tokens, position tracking, standings, and alert settings.
    /// </summary>
    public class AlertCharacter : INotifyPropertyChanged
    {
        // --- Basic identity ---
        public long ID { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public long CorporationID { get; set; }

        private string _corporationName;
        public string CorporationName
        {
            get => _corporationName;
            set { _corporationName = value; OnPropertyChanged(nameof(CorporationName)); }
        }

        private string _corporationTicker;
        public string CorporationTicker
        {
            get => _corporationTicker;
            set { _corporationTicker = value; OnPropertyChanged(nameof(CorporationTicker)); }
        }

        public long AllianceID { get; set; }

        private string _allianceName;
        public string AllianceName
        {
            get => _allianceName;
            set { _allianceName = value; OnPropertyChanged(nameof(AllianceName)); }
        }

        private string _allianceTicker;
        public string AllianceTicker
        {
            get => _allianceTicker;
            set { _allianceTicker = value; OnPropertyChanged(nameof(AllianceTicker)); }
        }

        // --- ESI tokens ---
        [XmlIgnore]
        public string ESIAccessToken { get; set; }
        public DateTime ESIAccessTokenExpiry { get; set; }
        public string ESIRefreshToken { get; set; }
        public string ESIScopesStored { get; set; }

        private bool _esiLinked;
        public bool ESILinked
        {
            get => _esiLinked;
            set { _esiLinked = value; OnPropertyChanged(nameof(ESILinked)); }
        }

        // --- Position ---
        private string _location;
        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(nameof(Location)); OnPropertyChanged(nameof(StatusText)); }
        }

        private string _region;
        public string Region
        {
            get => _region;
            set { _region = value; OnPropertyChanged(nameof(Region)); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isOnline;
        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(nameof(IsOnline)); OnPropertyChanged(nameof(StatusText)); }
        }

        // --- Alert settings ---
        private bool _alertEnabled = true;
        public bool AlertEnabled
        {
            get => _alertEnabled;
            set
            {
                _alertEnabled = value;
                OnPropertyChanged(nameof(AlertEnabled));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private int _alertRange = 5;
        public int AlertRange
        {
            get => _alertRange;
            set
            {
                _alertRange = Math.Clamp(value, 1, 10);
                OnPropertyChanged(nameof(AlertRange));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        // --- Standings (alliance ID -> standing value) ---
        [XmlIgnore]
        public Dictionary<long, float> Standings { get; set; } = new();

        // --- Computed display ---
        [XmlIgnore]
        public string StatusText
        {
            get
            {
                bool zh = SMT.EVEData.EveManager.CurrentLanguage == "zh-CN";
                return $"{Location ?? "-"} | {(zh ? "在线:" : "Online:")} {(IsOnline ? (zh ? "是" : "Yes") : (zh ? "否" : "No"))} | {(zh ? "预警:" : "Alert:")} {AlertRange}{(zh ? "跳" : "j")}";
            }
        }

        // --- Active monitor flag ---
        [XmlIgnore]
        public bool IsActiveMonitor { get; set; }

        // --- Computed warning systems ---
        [XmlIgnore]
        public List<string> WarningSystems { get; set; } = new();

        [XmlIgnore]
        public List<string> ClearSystems { get; set; } = new();

        [XmlIgnore]
        public List<string> StaleSystems { get; set; } = new();

        /// <summary>
        /// Builds AuthDTO for EVEStandard API calls.
        /// </summary>
        public AuthDTO GetAuthDTO()
        {
            if (!ESILinked || ID == 0 || string.IsNullOrEmpty(ESIAccessToken))
                return null;

            var expiry = ESIAccessTokenExpiry.Kind == DateTimeKind.Utc
                ? ESIAccessTokenExpiry
                : ESIAccessTokenExpiry.ToUniversalTime();

            return new AuthDTO
            {
                CharacterId = ID,
                AccessToken = new EVEStandard.Models.SSO.AccessTokenDetails
                {
                    AccessToken = ESIAccessToken,
                    RefreshToken = ESIRefreshToken ?? string.Empty,
                    ExpiresUtc = expiry
                },
                Scopes = ESIScopesStored ?? string.Empty
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
