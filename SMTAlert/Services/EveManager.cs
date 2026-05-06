using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EVEDataUtils;
using EVEStandard;
using EVEStandard.Enumerations;
using EVEStandard.Models.API;
using EVEStandard.Models.SSO;
using Newtonsoft.Json;

namespace SMT.EVEData
{
    public class EveManager
    {
        private static EveManager _instance;

        public static string CurrentLanguage { get; set; } = "en-US";
        public static Dictionary<string, string> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static EveManager Instance
        {
            get => _instance;
            set => _instance = value;
        }

        public string EVELogFolder { get; set; }
        public string DataRootFolder { get; set; }
        public string SaveDataRootFolder { get; set; }
        public ZKillRedisQ ZKillFeed { get; set; }

        public List<System> Systems { get; set; }
        public List<MapRegion> Regions { get; set; }
        public SerializableDictionary<string, string> ShipTypes { get; set; }
        public SerializableDictionary<string, string> ShipTypesCN { get; set; } = new();
        public SerializableDictionary<long, string> SystemIDToName { get; set; }

        public SerializableDictionary<int, string> CharacterIDToName { get; set; }
        public SerializableDictionary<int, string> AllianceIDToName { get; set; }
        public SerializableDictionary<int, string> AllianceIDToTicker { get; set; }

        public EVEStandardAPI EveApiClient { get; set; }
        public SSOv2 Sso { get; set; }
        public List<string> ESIScopes { get; set; }
        public bool UseESIForCharacterPositions { get; set; } = true;

        private Dictionary<string, System> NameToSystem = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<long, System> IDToSystem = new();

        private string _pendingPkceCodeVerifier;
        private string _versionStr;

        // Event for when a new character is added via ESI
        public event Action<LocalCharacter> CharacterAddedViaEsi;

        public EveManager(string version)
        {
            Instance = this;
            _versionStr = version;

            string appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMT");
            DataRootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            SaveDataRootFolder = appDataRoot;

            if (!Directory.Exists(DataRootFolder))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "SMT", "EVEData", "data")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "SMT", "EVEData", "data")),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMT", "data"),
                };
                foreach (var p in candidates)
                {
                    if (Directory.Exists(p)) { DataRootFolder = p; break; }
                }
            }
        }

        public void LoadFromDisk()
        {
            SystemIDToName = new SerializableDictionary<long, string>();

            string systemsFile = Path.Combine(DataRootFolder, "Systems.dat");
            string shipTypesFile = Path.Combine(DataRootFolder, "ShipTypes.dat");

            if (!File.Exists(systemsFile))
                throw new FileNotFoundException($"Systems data file not found: {systemsFile}. Please ensure the SMT data files are available.");

            Systems = Serialization.DeserializeFromDisk<List<System>>(systemsFile);

            if (File.Exists(shipTypesFile))
                ShipTypes = Serialization.DeserializeFromDisk<SerializableDictionary<string, string>>(shipTypesFile);
            else
                ShipTypes = new SerializableDictionary<string, string>();

            CharacterIDToName = new SerializableDictionary<int, string>();
            AllianceIDToName = new SerializableDictionary<int, string>();
            AllianceIDToTicker = new SerializableDictionary<int, string>();

            NameToSystem = new Dictionary<string, System>(StringComparer.OrdinalIgnoreCase);
            IDToSystem = new Dictionary<long, System>();

            foreach (System s in Systems)
            {
                SystemIDToName[s.ID] = s.Name;
                NameToSystem[s.Name] = s;
                IDToSystem[s.ID] = s;
            }

            LoadShipTypesCN();
            Init();
        }

        private void Init()
        {
            if (string.IsNullOrEmpty(EveAppConfig.ClientID))
            {
                Debug.WriteLine("EveManager: ESI ClientID not configured — ESI features disabled.");
                return;
            }

            string userAgent = "SMT/" + EveAppConfig.SMT_VERSION + EveAppConfig.SMT_USERAGENT_DETAILS;
            EveApiClient = new EVEStandardAPI(userAgent, DataSource.Tranquility, CompatibilityDate.v2025_12_16, TimeSpan.FromSeconds(30));
            Sso = new SSOv2(DataSource.Tranquility, EveAppConfig.CallbackURL, EveAppConfig.ClientID, null);

            ESIScopes = new List<string>
            {
                "publicData",
                "esi-location.read_location.v1",
                "esi-search.search_structures.v1",
                "esi-clones.read_clones.v1",
                "esi-ui.write_waypoint.v1",
                "esi-characters.read_standings.v1",
                "esi-location.read_online.v1",
                "esi-characters.read_fatigue.v1",
                "esi-corporations.read_contacts.v1",
                "esi-alliances.read_contacts.v1",
                "esi-universe.read_structures.v1",
                "esi-fleets.read_fleet.v1"
            };
        }

        private void LoadShipTypesCN()
        {
            ShipTypesCN = new SerializableDictionary<string, string>();

            // Load built-in Chinese ship names shipped with the application
            string builtinFile = Path.Combine(DataRootFolder, "ShipTypesCN.dat");
            if (File.Exists(builtinFile))
            {
                try
                {
                    var builtin = Serialization.DeserializeFromDisk<SerializableDictionary<string, string>>(builtinFile);
                    if (builtin != null)
                    {
                        foreach (var kvp in builtin)
                            ShipTypesCN[kvp.Key] = kvp.Value;
                    }
                }
                catch { }
            }

            // Merge user cache (may contain updates or overrides)
            string userCache = Path.Combine(SaveDataRootFolder, "ShipTypesCN.dat");
            if (File.Exists(userCache))
            {
                try
                {
                    var userCn = Serialization.DeserializeFromDisk<SerializableDictionary<string, string>>(userCache);
                    if (userCn != null)
                    {
                        foreach (var kvp in userCn)
                            ShipTypesCN[kvp.Key] = kvp.Value;
                    }
                }
                catch { }
            }
        }

        public System GetEveSystem(string name)
        {
            if (NameToSystem.TryGetValue(name, out var sys))
                return sys;
            return null;
        }

        public System GetEveSystemFromID(long id)
        {
            if (IDToSystem.TryGetValue(id, out var sys))
                return sys;
            return null;
        }

        public string GetEveSystemNameFromID(long id)
        {
            var sys = GetEveSystemFromID(id);
            return sys?.Name ?? string.Empty;
        }

        public string GetSystemNameFromSystemID(long id)
        {
            return SystemIDToName.TryGetValue(id, out var name) ? name : string.Empty;
        }

        public bool DoesSystemExist(string name) => GetEveSystem(name) != null;

        public string GetAllianceName(int id)
        {
            if (AllianceIDToName.TryGetValue(id, out var name))
                return name;
            return string.Empty;
        }

        public string GetAllianceTicker(int id)
        {
            if (AllianceIDToTicker.TryGetValue(id, out var ticker))
                return ticker;
            return string.Empty;
        }

        public string GetCharacterName(int id)
        {
            if (CharacterIDToName.TryGetValue(id, out var name))
                return name;
            return string.Empty;
        }

        public LocalCharacter FindCharacterByName(string characterName)
        {
            // Characters are managed by AlertManager; we return null here.
            // AlertManager will handle the lookup via its own collection.
            return null;
        }

        public async Task ResolveAllianceIDs(List<int> IDs)
        {
            if (IDs.Count == 0 || EveApiClient == null) return;

            var unknownIDs = new List<int>();
            foreach (int id in IDs)
            {
                if ((!AllianceIDToName.ContainsKey(id) || !AllianceIDToTicker.ContainsKey(id)) && !unknownIDs.Contains(id))
                    unknownIDs.Add(id);
            }

            if (unknownIDs.Count == 0) return;

            try
            {
                var idsLong = unknownIDs.ConvertAll(i => (long)i);
                var esra = await EveApiClient.Universe.GetNamesAndCategoriesFromIdsAsync(idsLong);
                if (ESIHelpers.ValidateESICall(esra))
                {
                    foreach (var ri in esra.Model)
                    {
                        if (ri.Category == "alliance")
                        {
                            try
                            {
                                var esraA = await EveApiClient.Alliance.GetAllianceInfoAsync(ri.Id);
                                if (ESIHelpers.ValidateESICall(esraA))
                                {
                                    AllianceIDToTicker[(int)ri.Id] = esraA.Model.Ticker;
                                    AllianceIDToName[(int)ri.Id] = esraA.Model.Name;
                                }
                            }
                            catch
                            {
                                AllianceIDToTicker[(int)ri.Id] = "???";
                                AllianceIDToName[(int)ri.Id] = "???";
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public async Task ResolveCharacterIDs(List<int> IDs)
        {
            if (IDs.Count == 0 || EveApiClient == null) return;

            var unknownIDs = new List<int>();
            foreach (int id in IDs)
            {
                if (!CharacterIDToName.ContainsKey(id))
                    unknownIDs.Add(id);
            }

            if (unknownIDs.Count == 0) return;

            try
            {
                var idsLong = unknownIDs.ConvertAll(i => (long)i);
                var esra = await EveApiClient.Universe.GetNamesAndCategoriesFromIdsAsync(idsLong);
                if (ESIHelpers.ValidateESICall(esra))
                {
                    foreach (var ri in esra.Model)
                    {
                        if (ri.Category == "character")
                            CharacterIDToName[(int)ri.Id] = ri.Name;
                    }
                }
            }
            catch { }
        }

        public void InitNavigation()
        {
            if (Systems != null)
                Navigation.InitNavigation(Systems);
        }

        // ESI SSO Login

        public string GetESILogonURL(string challengeCode)
        {
            if (Sso == null) return null;
            string codeVerifier = ToBase64UrlString(Encoding.UTF8.GetBytes(challengeCode));
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            }
            string codeChallenge = ToBase64UrlString(hash);
            _pendingPkceCodeVerifier = codeVerifier;
            return Sso.AuthorizeToSSOPKCEUri(_versionStr, codeChallenge, ESIScopes);
        }

        private static string ToBase64UrlString(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Handle the ESI callback URI. Returns the created/updated LocalCharacter, or null on failure.
        /// </summary>
        public async Task<LocalCharacter> HandleEsiAuthCallback(Uri uri, string challengeCode)
        {
            if (Sso == null) return null;
            string code = ParseQueryParam(uri.Query, "code");
            if (string.IsNullOrEmpty(code))
                return null;

            string codeVerifier = _pendingPkceCodeVerifier ?? ToBase64UrlString(Encoding.UTF8.GetBytes(challengeCode));
            _pendingPkceCodeVerifier = null;

            AccessTokenDetails tokenDetails;
            try
            {
                tokenDetails = await Sso.VerifyAuthorizationForPKCEAuthAsync(code, codeVerifier);
                if (tokenDetails == null || tokenDetails.ExpiresIn <= 0)
                    return null;
            }
            catch
            {
                return null;
            }

            CharacterDetails characterDetails;
            try
            {
                characterDetails = await Sso.GetCharacterDetailsAsync(tokenDetails.AccessToken);
                if (characterDetails == null)
                    return null;
            }
            catch
            {
                return null;
            }

            var esiChar = new LocalCharacter(characterDetails.CharacterName, string.Empty, string.Empty)
            {
                ESIRefreshToken = tokenDetails.RefreshToken,
                ESILinked = true,
                ESIAccessToken = tokenDetails.AccessToken,
                ESIAccessTokenExpiry = tokenDetails.ExpiresUtc.ToLocalTime(),
                ID = characterDetails.CharacterId,
                ESIScopesStored = characterDetails.Scopes != null ? string.Join(" ", characterDetails.Scopes) : string.Empty
            };

            CharacterAddedViaEsi?.Invoke(esiChar);
            return esiChar;
        }

        private static string ParseQueryParam(string query, string paramName)
        {
            if (string.IsNullOrEmpty(query)) return null;
            query = query.TrimStart('?');
            foreach (string part in query.Split('&'))
            {
                int eqIdx = part.IndexOf('=');
                if (eqIdx < 0) continue;
                string key = Uri.UnescapeDataString(part.Substring(0, eqIdx));
                string value = Uri.UnescapeDataString(part.Substring(eqIdx + 1));
                if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
            return null;
        }
    }
}
