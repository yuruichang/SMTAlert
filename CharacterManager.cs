using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using System.Web;
using EVEDataUtils;
using EVEStandard;
using EVEStandard.Enumerations;
using EVEStandard.Models;
using EVEStandard.Models.API;
using EVEStandard.Models.SSO;
using SMT.EVEData;
using Timer = System.Timers.Timer;

namespace SMTAlert
{
    /// <summary>
    /// Independent character manager for SMTAlert.
    /// Handles ESI authorization, character CRUD, position tracking, and alert calculations.
    /// </summary>
    public class CharacterManager
    {
        private static readonly string StorageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTAlert");
        private static readonly string CharactersFile = Path.Combine(StorageRoot, "Characters.xml");

        private EVEStandardAPI _esiClient;
        private SSOv2 _sso;
        private string _pendingPkceCodeVerifier;

        private Timer _updateTimer;
        private Timer _warningTimer;
        private HashSet<string> _recentAlertedSystems = new();
        private Dictionary<string, DateTime> _systemFirstWarned = new();
        private int _updateTickCounter = 0;

        // Character-based clear tracking
        private Dictionary<string, string> _reporterLastSystem = new();   // reporterName → last system reported
        private Dictionary<string, HashSet<string>> _systemReporters = new(); // system → set of reporter names
        private Dictionary<string, DateTime> _systemClearedAt = new();    // system → time when marked clear

        // Last intel text per system (for tooltip) — written from Timer thread, read from UI thread
        public System.Collections.Concurrent.ConcurrentDictionary<string, string> SystemLastIntelText { get; private set; } = new();

        public ObservableCollection<AlertCharacter> Characters { get; } = new();

        public event Action CharactersChanged;
        public event Action<CharacterManager> CharacterPositionsUpdated;

        // ESI scopes needed for SMTAlert
        public static readonly List<string> RequiredScopes = new()
        {
            "publicData",
            "esi-location.read_location.v1",
            "esi-location.read_online.v1",
            "esi-characters.read_standings.v1",
        };

        public CharacterManager()
        {
            if (!Directory.Exists(StorageRoot))
                Directory.CreateDirectory(StorageRoot);
        }

        public void Initialize()
        {
            string userAgent = "SMTAlert/1.0 (+https://github.com/Slazanger/SMT)";
            _esiClient = new EVEStandardAPI(userAgent, DataSource.Tranquility,
                CompatibilityDate.v2025_12_16, TimeSpan.FromSeconds(30));
            _sso = new SSOv2(DataSource.Tranquility,
                EveAppConfig.CallbackURL, EveAppConfig.ClientID, null);

            LoadCharacters();

            // Position update every 5 seconds
            _updateTimer = new Timer(5000);
            _updateTimer.Elapsed += OnUpdateTick;
            _updateTimer.AutoReset = true;
            _updateTimer.Start();

            // Warning systems recalculation every 2 seconds
            _warningTimer = new Timer(2000);
            _warningTimer.Elapsed += OnWarningTick;
            _warningTimer.AutoReset = true;
            _warningTimer.Start();

            // Subscribe to intel updates for alert sounds
            EveManager.Instance.IntelUpdatedEvent += OnIntelForAlert;
        }

        public void Shutdown()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _warningTimer?.Stop();
            _warningTimer?.Dispose();
            if (EveManager.Instance != null)
                EveManager.Instance.IntelUpdatedEvent -= OnIntelForAlert;
            SaveCharacters();
        }

        // --- ESI Authorization ---

        public string GetESILogonURL(string challengeCode)
        {
            byte[] challengeBytes = Encoding.UTF8.GetBytes(challengeCode);
            string codeVerifier = Base64UrlEncode(challengeBytes);
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            string codeChallenge = Base64UrlEncode(hash);

            _pendingPkceCodeVerifier = codeVerifier;

            return _sso.AuthorizeToSSOPKCEUri("SMTAlert", codeChallenge, RequiredScopes);
        }

        public async Task<AlertCharacter> HandleEveAuthCallback(Uri uri, string challengeCode)
        {
            try
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                string code = query["code"];
                if (string.IsNullOrEmpty(code))
                    return null;

                var tokenDetails = await _sso.VerifyAuthorizationForPKCEAuthAsync(
                    code, _pendingPkceCodeVerifier);
                if (tokenDetails == null || string.IsNullOrEmpty(tokenDetails.AccessToken))
                    return null;

                var charDetails = await _sso.GetCharacterDetailsAsync(tokenDetails.AccessToken);
                if (charDetails == null)
                    return null;

                var character = new AlertCharacter
                {
                    ID = charDetails.CharacterId,
                    Name = charDetails.CharacterName,
                    ESIAccessToken = tokenDetails.AccessToken,
                    ESIAccessTokenExpiry = tokenDetails.ExpiresUtc.ToLocalTime(),
                    ESIRefreshToken = tokenDetails.RefreshToken ?? string.Empty,
                    ESIScopesStored = charDetails.Scopes != null
                        ? string.Join(" ", charDetails.Scopes) : string.Empty,
                    ESILinked = true,
                    AlertRange = 5,
                    AlertEnabled = true
                };

                // Fetch initial info
                await UpdateCharacterInfo(character);
                await UpdateCharacterPosition(character);
                await UpdateCharacterStandings(character);

                AddCharacter(character);
                return character;
            }
            catch
            {
                return null;
            }
        }

        // --- Character CRUD ---

        public void AddCharacter(AlertCharacter c)
        {
            if (Characters.Any(x => x.ID == c.ID))
            {
                var existing = Characters.First(x => x.ID == c.ID);
                var idx = Characters.IndexOf(existing);
                Characters[idx] = c;
            }
            else
            {
                Characters.Add(c);
            }
            SaveCharacters();
            CharactersChanged?.Invoke();
        }

        public void RemoveCharacter(AlertCharacter c)
        {
            Characters.Remove(c);
            SaveCharacters();
            CharactersChanged?.Invoke();
        }

        // --- ESI Updates ---

        private async void OnUpdateTick(object sender, ElapsedEventArgs e)
        {
            _updateTickCounter++;

            foreach (var c in Characters.ToList())
            {
                if (!c.ESILinked)
                    continue;

                if ((c.ESIAccessTokenExpiry - DateTime.Now).TotalMinutes < 1)
                {
                    await RefreshAccessToken(c);
                    await UpdateCharacterInfo(c);
                }

                await UpdateCharacterPosition(c);
                await UpdateCharacterOnlineStatus(c);

                // Refresh standings every 60 seconds (every 12th tick)
                if (_updateTickCounter % 12 == 0)
                    await UpdateCharacterStandings(c);
            }

            CharacterPositionsUpdated?.Invoke(this);
        }

        private void OnIntelForAlert(List<IntelData> items)
        {
            var newest = items.FirstOrDefault();
            if (newest == null) return;

            var c = App.ActiveCharacter;
            if (c == null || !c.AlertEnabled || !c.IsOnline || string.IsNullOrEmpty(c.Location))
                return;

            // Get all systems within alert range
            var systemsInRange = Navigation.GetSystemsXJumpsFrom(
                new List<string>(), c.Location, c.AlertRange);

            // Check if any intel system is within range, new to us, and NOT a clear notification
            bool shouldAlert = false;
            if (!newest.ClearNotification)
            {
                foreach (var sysName in newest.Systems)
                {
                    if (systemsInRange.Contains(sysName) && !_recentAlertedSystems.Contains(sysName))
                    {
                        _recentAlertedSystems.Add(sysName);
                        shouldAlert = true;
                    }
                }
            }

            if (shouldAlert)
            {
                // Play alert sound on a background thread (SoundPlayer.Play blocks briefly)
                Task.Run(() => AlertSound.Play());
            }

            // Clean up old entries periodically
            if (_recentAlertedSystems.Count > 100)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-2);
                // Just clear entirely if too large — simple approach
                _recentAlertedSystems.Clear();
            }
        }

        private void OnWarningTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                var em = EveManager.Instance;
                var intelList = em?.IntelDataList;
                if (intelList == null) return;

                // Snapshot to avoid threading issues with concurrent writes
                List<IntelData> intelSnapshot;
                lock (intelList)
                {
                    intelSnapshot = intelList.ToList();
                }

                foreach (var c in Characters.ToList())
                {
                    if (c.AlertEnabled && c.IsOnline && !string.IsNullOrEmpty(c.Location))
                    {
                        var systemsInRange = Navigation.GetSystemsXJumpsFrom(
                            new List<string>(), c.Location, c.AlertRange);

                        var warned = new List<string>();
                        var cleared = new List<string>();
                        var stale = new List<string>();
                        var now = DateTime.UtcNow;
                        var freshCutoff = now.AddMinutes(-5);
                        var oldCutoff = now.AddMinutes(-10);

                        // Iterate oldest-first so newer intel overrides older.
                        // FixedQueue inserts at index 0 (newest first), so reverse gives oldest first.
                        for (int idx = intelSnapshot.Count - 1; idx >= 0; idx--)
                        {
                            var intel = intelSnapshot[idx];
                            if (intel.IntelTime < oldCutoff) continue;
                            bool isFresh = intel.IntelTime >= freshCutoff;

                            foreach (var sysName in intel.Systems)
                            {
                                if (!systemsInRange.Contains(sysName))
                                    continue;

                                // Track last intel text for tooltip (with time prefix)
                                SystemLastIntelText[sysName] = $"[{intel.IntelTime:HH:mm:ss}] {intel.IntelString.Trim()}";

                                if (intel.ClearNotification)
                                {
                                    warned.Remove(sysName);
                                    if (!cleared.Contains(sysName))
                                        cleared.Add(sysName);
                                    _systemFirstWarned.Remove(sysName);
                                }
                                else
                                {
                                    if (isFresh)
                                    {
                                        // Fresh hostile: active warning
                                        if (!warned.Contains(sysName))
                                            warned.Add(sysName);
                                        if (!_systemFirstWarned.ContainsKey(sysName))
                                            _systemFirstWarned[sysName] = now;
                                    }
                                    else
                                    {
                                        // Older hostile (5-10 min): candidate for stale
                                        if (!warned.Contains(sysName) && !cleared.Contains(sysName))
                                        {
                                            if (!_systemFirstWarned.ContainsKey(sysName))
                                                _systemFirstWarned[sysName] = intel.IntelTime;
                                        }
                                    }
                                }

                                // Character-based clear tracking
                                TrackReporterMovement(intel, sysName, cleared);
                            }
                        }

                        // Stale: systems first warned > 5 min ago, not actively warned, not cleared
                        foreach (var kvp in _systemFirstWarned.ToList())
                        {
                            if (kvp.Value < freshCutoff && !cleared.Contains(kvp.Key) && !warned.Contains(kvp.Key))
                            {
                                stale.Add(kvp.Key);
                            }
                        }

                        // Clean up old entries from _systemFirstWarned
                        foreach (var key in _systemFirstWarned.Keys.ToList())
                        {
                            if (!warned.Contains(key) && !stale.Contains(key) && !cleared.Contains(key))
                                _systemFirstWarned.Remove(key);
                        }

                        // Clear revert: systems marked clear for > 1 minute revert to default
                        var clearRevertCutoff = now.AddMinutes(-1);
                        foreach (var sysName in cleared.ToList())
                        {
                            if (!_systemClearedAt.ContainsKey(sysName))
                                _systemClearedAt[sysName] = now;
                            else if (_systemClearedAt[sysName] < clearRevertCutoff)
                            {
                                cleared.Remove(sysName);
                                _systemClearedAt.Remove(sysName);
                            }
                        }
                        // Clean up _systemClearedAt for systems no longer in cleared list
                        foreach (var key in _systemClearedAt.Keys.ToList())
                        {
                            if (!cleared.Contains(key))
                                _systemClearedAt.Remove(key);
                        }

                        c.WarningSystems = warned;
                        c.ClearSystems = cleared;
                        c.StaleSystems = stale;
                    }
                    else
                    {
                        c.WarningSystems?.Clear();
                        c.ClearSystems?.Clear();
                        c.StaleSystems?.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnWarningTick error: {ex.Message}");
            }
        }

        private async Task RefreshAccessToken(AlertCharacter c)
        {
            if (string.IsNullOrEmpty(c.ESIRefreshToken) || !c.ESILinked)
                return;

            try
            {
                var tokenDetails = await _sso.GetNewPKCEAccessAndRefreshTokenAsync(c.ESIRefreshToken);
                if (tokenDetails == null || string.IsNullOrEmpty(tokenDetails.AccessToken))
                    return;

                c.ESIAccessToken = tokenDetails.AccessToken;
                c.ESIAccessTokenExpiry = tokenDetails.ExpiresUtc.ToLocalTime();
                c.ESIRefreshToken = tokenDetails.RefreshToken ?? string.Empty;
            }
            catch { }
        }

        public async Task UpdateCharacterInfo(AlertCharacter c)
        {
            var auth = c.GetAuthDTO();
            if (auth == null) return;

            try
            {
                var esr = await _esiClient.Character.GetCharacterPublicInfoAsync(c.ID);
                if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                {
                    c.CorporationID = esr.Model.CorporationId;
                }

                if (c.CorporationID > 0)
                {
                    var corpEsr = await _esiClient.Corporation.GetCorporationInfoAsync(c.CorporationID);
                    if (ESIHelpers.ValidateESICall(corpEsr) && corpEsr.Model != null)
                    {
                        c.CorporationName = corpEsr.Model.Name;
                        c.CorporationTicker = corpEsr.Model.Ticker;
                        c.AllianceID = corpEsr.Model.AllianceId ?? 0;
                    }
                }

                if (c.AllianceID > 0)
                {
                    var allyEsr = await _esiClient.Alliance.GetAllianceInfoAsync((int)c.AllianceID);
                    if (ESIHelpers.ValidateESICall(allyEsr) && allyEsr.Model != null)
                    {
                        c.AllianceName = allyEsr.Model.Name;
                        c.AllianceTicker = allyEsr.Model.Ticker;
                    }
                }
            }
            catch { }
        }

        public async Task UpdateCharacterPosition(AlertCharacter c)
        {
            var auth = c.GetAuthDTO();
            if (auth == null) return;

            try
            {
                var esr = await _esiClient.Location.GetCharacterLocationAsync(auth);
                if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                {
                    var sysId = esr.Model.SolarSystemId;
                    if (EveManager.Instance.SystemIDToName.ContainsKey(sysId))
                    {
                        c.Location = EveManager.Instance.SystemIDToName[sysId];
                        var sys = EveManager.Instance.GetEveSystem(c.Location);
                        if (sys != null)
                            c.Region = sys.Region;
                    }
                }
            }
            catch { }
        }

        public async Task UpdateCharacterOnlineStatus(AlertCharacter c)
        {
            var auth = c.GetAuthDTO();
            if (auth == null) return;

            try
            {
                var esr = await _esiClient.Location.GetCharacterOnlineAsync(auth);
                if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                {
                    c.IsOnline = esr.Model.Online;
                }
            }
            catch { }
        }

        public async Task UpdateCharacterStandings(AlertCharacter c)
        {
            var auth = c.GetAuthDTO();
            if (auth == null) return;

            try
            {
                // Use alliance and corporation contacts (same approach as main SMT)
                // Character personal standings are typically empty for most players.
                // Corp/Alliance contacts are what matter for ZKB kill coloring.

                if (c.AllianceID != 0)
                {
                    int page = 1;
                    int maxPageCount = 1;
                    do
                    {
                        var esr = await _esiClient.Contacts.GetAllianceContactsAsync(auth, (int)c.AllianceID, page);
                        if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                        {
                            maxPageCount = esr.MaxPages > 0 ? esr.MaxPages : 1;
                            foreach (var con in esr.Model)
                            {
                                c.Standings[con.ContactId] = (float)con.Standing;
                            }
                        }
                        page++;
                    }
                    while (page <= maxPageCount);
                }

                if (c.CorporationID != 0)
                {
                    int page = 1;
                    int maxPageCount = 1;
                    do
                    {
                        var esr = await _esiClient.Contacts.GetCorporationContactsAsync(auth, (int)c.CorporationID, page);
                        if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                        {
                            maxPageCount = esr.MaxPages > 0 ? esr.MaxPages : 1;
                            foreach (var con in esr.Model)
                            {
                                // Only add if not already set by alliance contacts
                                if (!c.Standings.ContainsKey(con.ContactId))
                                    c.Standings[con.ContactId] = (float)con.Standing;
                            }
                        }
                        page++;
                    }
                    while (page <= maxPageCount);
                }
            }
            catch { }
        }

        /// <summary>
        /// Tracks reporter movement between systems. When all reporters of a system
        /// have been seen in other systems, the original system is implicitly cleared.
        /// </summary>
        private void TrackReporterMovement(IntelData intel, string sysName, List<string> cleared)
        {
            string reporter = ParseReporterName(intel.RawIntelString);
            if (string.IsNullOrEmpty(reporter))
                return;

            // Update reporter's last known system
            if (_reporterLastSystem.TryGetValue(reporter, out string prevSystem) && prevSystem != sysName)
            {
                // Reporter moved to a new system — remove them from the previous system's reporter set
                if (_systemReporters.TryGetValue(prevSystem, out var prevReporters))
                {
                    prevReporters.Remove(reporter);
                    // If no reporters remain for the previous system, it's implicitly cleared
                    if (prevReporters.Count == 0)
                    {
                        _systemReporters.Remove(prevSystem);
                        _systemFirstWarned.Remove(prevSystem);
                        if (!cleared.Contains(prevSystem))
                            cleared.Add(prevSystem);
                    }
                }
            }

            _reporterLastSystem[reporter] = sysName;

            // Add reporter to current system's reporter set
            if (!_systemReporters.ContainsKey(sysName))
                _systemReporters[sysName] = new HashSet<string>();
            _systemReporters[sysName].Add(reporter);
        }

        /// <summary>
        /// Extracts reporter character name from raw intel line.
        /// Format: "[ timestamp ] CharName > message"
        /// </summary>
        private static string ParseReporterName(string rawIntelString)
        {
            if (string.IsNullOrEmpty(rawIntelString))
                return null;

            int closeBracket = rawIntelString.IndexOf(']');
            if (closeBracket < 0)
                return null;

            int gtPos = rawIntelString.IndexOf('>', closeBracket);
            if (gtPos <= closeBracket + 1)
                return null;

            return rawIntelString.Substring(closeBracket + 1, gtPos - closeBracket - 1).Trim();
        }

        // --- Persistence ---

        public void LoadCharacters()
        {
            var chars = Serialization.DeserializeFromDisk<List<AlertCharacter>>(CharactersFile);
            if (chars != null)
            {
                foreach (var c in chars)
                {
                    // Clear stale tokens on load
                    c.ESIAccessToken = string.Empty;
                    c.ESILinked = false;
                    Characters.Add(c);
                }
            }
        }

        public void SaveCharacters()
        {
            Serialization.SerializeToDisk(Characters.ToList(), CharactersFile);
        }

        // --- Helpers ---

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
