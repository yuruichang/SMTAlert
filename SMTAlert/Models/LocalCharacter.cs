using System.ComponentModel;
using System.Xml.Serialization;
using EVEStandard.Models.API;
using EVEStandard.Models.SSO;

namespace SMT.EVEData
{
    public class LocalCharacter : Character, INotifyPropertyChanged
    {
        public static readonly string SaveVersion = "03";

        [XmlIgnore]
        public SemaphoreSlim UpdateLock = new(1);

        [XmlIgnore]
        public bool warningSystemsNeedsUpdate;

        private string _location;
        private bool _isOnline;

        public LocalCharacter()
        {
            ESILinked = false;
            ESIAuthCode = string.Empty;
            ESIAccessToken = string.Empty;
            ESIRefreshToken = string.Empty;
            Standings = new Dictionary<long, float>();
            Location = string.Empty;
            Region = string.Empty;
            CorporationID = -1;
            AllianceID = -1;
            IsOnline = true;
        }

        public LocalCharacter(string name, string lcf, string location)
            : this()
        {
            Name = name;
            Location = location;
            IsOnline = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [XmlIgnore]
        public string Location
        {
            get => _location;
            set
            {
                if (_location == value) return;
                _location = value;
                warningSystemsNeedsUpdate = true;
                OnPropertyChanged(nameof(Location));
            }
        }

        public string Region { get; set; }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(nameof(IsOnline)); }
        }

        public bool ESILinked { get; set; }

        public string ESIAccessToken { get; set; }

        public DateTime ESIAccessTokenExpiry { get; set; }
        public string ESIRefreshToken { get; set; }
        public string ESIAuthCode { get; set; }
        public string ESIScopesStored { get; set; }

        [XmlIgnore]
        public Dictionary<long, float> Standings { get; set; }

        public AuthDTO GetAuthDTO()
        {
            if (!ESILinked || ID == 0 || string.IsNullOrEmpty(ESIAccessToken))
                return null;
            var expiry = ESIAccessTokenExpiry.Kind == DateTimeKind.Utc ? ESIAccessTokenExpiry : ESIAccessTokenExpiry.ToUniversalTime();
            return new AuthDTO
            {
                CharacterId = ID,
                AccessToken = new AccessTokenDetails
                {
                    AccessToken = ESIAccessToken,
                    RefreshToken = ESIRefreshToken ?? string.Empty,
                    ExpiresUtc = expiry
                },
                Scopes = ESIScopesStored ?? string.Empty
            };
        }

        /// <summary>
        /// Update the Character info from ESI
        /// </summary>
        public async Task Update()
        {
            await UpdateLock.WaitAsync();
            try
            {
                TimeSpan ts = ESIAccessTokenExpiry - DateTime.Now;
                if (ts.TotalMinutes < 1)
                {
                    await RefreshAccessToken().ConfigureAwait(false);
                    await UpdateInfoFromESI().ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(Location))
                {
                    await UpdatePositionFromESI().ConfigureAwait(false);
                }

                await UpdateOnlineStatus().ConfigureAwait(false);
            }
            catch { }
            finally
            {
                UpdateLock.Release();
            }
        }

        /// <summary>
        /// Refresh the ESI access token
        /// </summary>
        public async Task RefreshAccessToken()
        {
            if (string.IsNullOrEmpty(ESIRefreshToken) || !ESILinked)
                return;

            try
            {
                var tokenDetails = await EveManager.Instance.Sso.GetNewPKCEAccessAndRefreshTokenAsync(ESIRefreshToken);
                if (tokenDetails == null || string.IsNullOrEmpty(tokenDetails.AccessToken))
                {
                    return;
                }

                var characterDetails = await EveManager.Instance.Sso.GetCharacterDetailsAsync(tokenDetails.AccessToken);
                if (characterDetails == null)
                    return;

                ESIAccessToken = tokenDetails.AccessToken;
                ESIAccessTokenExpiry = tokenDetails.ExpiresUtc.ToLocalTime();
                ESIRefreshToken = tokenDetails.RefreshToken ?? string.Empty;
                ESILinked = true;
                ESIScopesStored = characterDetails.Scopes != null ? string.Join(" ", characterDetails.Scopes) : string.Empty;

                // Persist updated tokens so the refresh token stays valid across restarts
                SMTAlert.AlertManager.Instance?.SaveCharacter(this);
            }
            catch
            {
                // Token refresh failed, will retry next tick
            }
        }

        /// <summary>
        /// Update the characters position from ESI
        /// </summary>
        public async Task UpdatePositionFromESI()
        {
            var auth = GetAuthDTO();
            if (auth == null || ID == 0 || !ESILinked)
                return;

            try
            {
                var esr = await EveManager.Instance.EveApiClient.Location.GetCharacterLocationAsync(auth);
                if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                {
                    if (!EveManager.Instance.SystemIDToName.ContainsKey(esr.Model.SolarSystemId))
                    {
                        Location = "";
                        Region = "";
                        return;
                    }
                    Location = EveManager.Instance.SystemIDToName[esr.Model.SolarSystemId];
                    var s = EveManager.Instance.GetEveSystem(Location);
                    Region = s?.Region ?? "";
                }
            }
            catch { }
        }

        /// <summary>
        /// Update the character info from the ESI data if linked
        /// </summary>
        public async Task UpdateInfoFromESI()
        {
            var auth = GetAuthDTO();
            if (auth == null || ID == 0 || !ESILinked)
            {
                if (ESILinked)
                    ESIAccessTokenExpiry = DateTime.Now;
                return;
            }

            var allianceToResolve = new List<int>();

            try
            {
                // Public info
                var esr = await EveManager.Instance.EveApiClient.Character.GetCharacterPublicInfoAsync(ID);
                if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                {
                    CorporationID = (int)esr.Model.CorporationId;
                    AllianceID = esr.Model.AllianceId ?? 0;
                }

                // Standings from alliance and corp contacts
                if (Standings.Count == 0)
                {
                    if (AllianceID != 0)
                    {
                        int page = 1;
                        int maxPageCount = 1;
                        do
                        {
                            var esrAlliance = await EveManager.Instance.EveApiClient.Contacts.GetAllianceContactsAsync(auth, AllianceID, page);
                            if (ESIHelpers.ValidateESICall(esrAlliance) && esrAlliance.Model != null)
                            {
                                maxPageCount = esrAlliance.MaxPages > 0 ? esrAlliance.MaxPages : 1;
                                foreach (var con in esrAlliance.Model)
                                {
                                    Standings[con.ContactId] = (float)con.Standing;
                                    if (con.ContactType == "alliance")
                                        allianceToResolve.Add((int)con.ContactId);
                                }
                            }
                            page++;
                        }
                        while (page <= maxPageCount);
                    }

                    if (CorporationID != 0)
                    {
                        int page = 1;
                        int maxPageCount = 1;
                        do
                        {
                            var esrCorp = await EveManager.Instance.EveApiClient.Contacts.GetCorporationContactsAsync(auth, CorporationID, page);
                            if (ESIHelpers.ValidateESICall(esrCorp) && esrCorp.Model != null)
                            {
                                maxPageCount = esrCorp.MaxPages > 0 ? esrCorp.MaxPages : 1;
                                foreach (var con in esrCorp.Model)
                                {
                                    Standings[con.ContactId] = (float)con.Standing;
                                    if (con.ContactType == "alliance")
                                        allianceToResolve.Add((int)con.ContactId);
                                }
                            }
                            page++;
                        }
                        while (page <= maxPageCount);
                    }
                }

                // Corporation info
                if (CorporationID != -1)
                {
                    var esrc = await EveManager.Instance.EveApiClient.Corporation.GetCorporationInfoAsync(CorporationID);
                    if (ESIHelpers.ValidateESICall(esrc) && esrc.Model != null)
                    {
                        CorporationName = esrc.Model.Name;
                        CorporationTicker = esrc.Model.Ticker;
                    }
                }

                // Alliance info
                if (AllianceID > 0)
                {
                    var esra = await EveManager.Instance.EveApiClient.Alliance.GetAllianceInfoAsync(AllianceID);
                    if (ESIHelpers.ValidateESICall(esra) && esra.Model != null)
                    {
                        AllianceName = esra.Model.Name;
                        AllianceTicker = esra.Model.Ticker;
                    }
                }
                else
                {
                    AllianceName = null;
                    AllianceTicker = null;
                }
            }
            catch { }

            await EveManager.Instance.ResolveAllianceIDs(allianceToResolve);
        }

        /// <summary>
        /// Update the characters logged on status from ESI
        /// </summary>
        private async Task UpdateOnlineStatus()
        {
            var auth = GetAuthDTO();
            if (auth == null || ID == 0 || !ESILinked)
                return;

            try
            {
                var esr = await EveManager.Instance.EveApiClient.Location.GetCharacterOnlineAsync(auth);
                if (ESIHelpers.ValidateESICall(esr) && esr.Model != null)
                {
                    IsOnline = esr.Model.Online;
                }
            }
            catch { }
        }

        public override string ToString()
        {
            string toStr = Name;
            if (ESILinked)
                toStr += " (ESI)";
            return toStr;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
