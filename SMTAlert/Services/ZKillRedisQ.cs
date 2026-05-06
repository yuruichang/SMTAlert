using System.ComponentModel;
using System.Net;
using System.Net.Http;
using Timer = System.Timers.Timer;

namespace SMT.EVEData
{
    public class ZKillRedisQ
    {
        private BackgroundWorker backgroundWorker;
        private Timer dpTimer;
        private long currentSequence;
        private DateTime nextPollTime = DateTime.MinValue;

        public List<ZKBDataSimple> KillStream { get; set; }

        public delegate void KillsAddedHandler();
        public event KillsAddedHandler KillsAddedEvent;

        public int KillExpireTimeMinutes { get; set; }
        public bool PauseUpdate { get; set; }

        public void Initialise()
        {
            KillStream = new List<ZKBDataSimple>();

            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.WorkerReportsProgress = false;
            backgroundWorker.DoWork += zkb_DoWork;
            backgroundWorker.RunWorkerCompleted += zkb_DoWorkComplete;

            dpTimer = new Timer(150);
            dpTimer.Elapsed += Dp_Tick;
            dpTimer.AutoReset = true;
            dpTimer.Enabled = true;
        }

        public void ShutDown()
        {
            if (dpTimer != null)
            {
                dpTimer.Enabled = false;
                dpTimer.Elapsed -= Dp_Tick;
                dpTimer.Dispose();
                dpTimer = null;
            }
            if (backgroundWorker != null)
            {
                backgroundWorker.CancelAsync();
                backgroundWorker = null;
            }
        }

        private void Dp_Tick(object sender, EventArgs e)
        {
            if (backgroundWorker != null && !backgroundWorker.IsBusy && !PauseUpdate && DateTime.Now >= nextPollTime)
            {
                backgroundWorker.RunWorkerAsync();
            }
        }

        private void zkb_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                HttpClient hc = new HttpClient();
                string userAgent = "SMT/" + EveAppConfig.SMT_VERSION + EveAppConfig.SMT_USERAGENT_DETAILS;
                hc.DefaultRequestHeaders.Add("User-Agent", userAgent);

                if (currentSequence == 0)
                {
                    string seqUrl = "https://r2z2.zkillboard.com/ephemeral/sequence.json";
                    var seqResponse = hc.GetAsync(seqUrl).Result;
                    if (seqResponse.IsSuccessStatusCode)
                    {
                        string seqContent = seqResponse.Content.ReadAsStringAsync().Result;
                        ZKBData.SequenceData seqData = ZKBData.SequenceData.FromJson(seqContent);
                        if (seqData != null)
                        {
                            currentSequence = seqData.Sequence;
                        }
                    }
                    if (currentSequence == 0)
                    {
                        nextPollTime = DateTime.Now.AddSeconds(6);
                        e.Result = 0;
                        return;
                    }
                }

                string r2z2Url = $"https://r2z2.zkillboard.com/ephemeral/{currentSequence}.json";
                var response = hc.GetAsync(r2z2Url).Result;

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    nextPollTime = DateTime.Now.AddSeconds(6);
                    e.Result = 0;
                    return;
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    nextPollTime = DateTime.Now.AddSeconds(60);
                    e.Result = 0;
                    return;
                }
                else if (response.IsSuccessStatusCode)
                {
                    string strContent = response.Content.ReadAsStringAsync().Result;
                    ZKBData.R2Z2Data r2z2Data = ZKBData.R2Z2Data.FromJson(strContent);

                    if (r2z2Data != null && r2z2Data.Esi != null && r2z2Data.Esi.Victim != null)
                    {
                        ZKBDataSimple zs = new ZKBDataSimple();
                        zs.KillID = r2z2Data.KillmailId;
                        zs.VictimAllianceID = r2z2Data.Esi.Victim.AllianceId;
                        zs.VictimCharacterID = r2z2Data.Esi.Victim.CharacterId;
                        zs.VictimCorpID = r2z2Data.Esi.Victim.CorporationId;
                        zs.SystemName = EveManager.Instance.GetEveSystemNameFromID((int)r2z2Data.Esi.SolarSystemId);
                        zs.KillTime = r2z2Data.Esi.KillmailTime.ToLocalTime();

                        zs.ShipTypeID = r2z2Data.Esi.Victim.ShipTypeId;
                        string shipID = zs.ShipTypeID.ToString();
                        if (EveManager.Instance.ShipTypes.ContainsKey(shipID))
                        {
                            zs.ShipType = EveManager.Instance.ShipTypes[shipID];
                        }
                        else
                        {
                            zs.ShipType = "Unknown (" + shipID + ")";
                        }

                        zs.VictimAllianceName = EveManager.Instance.GetAllianceName(zs.VictimAllianceID);

                        KillStream.Insert(0, zs);
                        KillsAddedEvent?.Invoke();
                    }

                    currentSequence++;
                    e.Result = 0;
                }
                else
                {
                    nextPollTime = DateTime.Now.AddSeconds(10);
                    e.Result = -1;
                }
            }
            catch
            {
                nextPollTime = DateTime.Now.AddSeconds(10);
                e.Result = -1;
            }
        }

        private void zkb_DoWorkComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            bool updatedKillList = false;

            for (int i = KillStream.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(KillStream[i].VictimAllianceName))
                {
                    KillStream[i].VictimAllianceName = EveManager.Instance.GetAllianceName(KillStream[i].VictimAllianceID);
                }

                if (KillStream[i].KillTime + TimeSpan.FromMinutes(KillExpireTimeMinutes) < DateTimeOffset.Now)
                {
                    KillStream.RemoveAt(i);
                    updatedKillList = true;
                }
            }

            if (updatedKillList)
            {
                KillsAddedEvent?.Invoke();
            }
        }

        public class ZKBDataSimple : INotifyPropertyChanged
        {
            private string m_victimAllianceName;
            private string m_shipType;

            public event PropertyChangedEventHandler PropertyChanged;

            public long KillID { get; set; }
            public DateTimeOffset KillTime { get; set; }
            public int ShipTypeID { get; set; }

            public string ShipType
            {
                get => m_shipType;
                set
                {
                    m_shipType = value;
                    OnPropertyChanged("ShipType");
                    OnPropertyChanged("ShipTypeDisplay");
                }
            }

            public string ShipTypeDisplay
            {
                get
                {
                    if (EveManager.CurrentLanguage == "zh-CN" &&
                        EveManager.Instance != null &&
                        EveManager.Instance.ShipTypesCN != null &&
                        EveManager.Instance.ShipTypesCN.ContainsKey(ShipTypeID.ToString()))
                    {
                        return EveManager.Instance.ShipTypesCN[ShipTypeID.ToString()];
                    }
                    return ShipType;
                }
            }

            public void RefreshShipTypeDisplay()
            {
                OnPropertyChanged("ShipTypeDisplay");
            }

            public string SystemName { get; set; }
            public int VictimAllianceID { get; set; }

            public string VictimAllianceName
            {
                get
                {
                    if (!string.IsNullOrEmpty(m_victimAllianceName))
                        return m_victimAllianceName;
                    if (VictimAllianceID == 0) return "无";
                    string ticker = EveManager.Instance?.GetAllianceTicker(VictimAllianceID);
                    if (!string.IsNullOrEmpty(ticker)) return $"[{ticker}]";
                    return $"[ID:{VictimAllianceID}]";
                }
                set
                {
                    m_victimAllianceName = value;
                    OnPropertyChanged("VictimAllianceName");
                }
            }

            public int VictimCharacterID { get; set; }
            public int VictimCorpID { get; set; }

            public override string ToString()
            {
                string allianceTicker = EveManager.Instance.GetAllianceTicker(VictimAllianceID);
                if (string.IsNullOrEmpty(allianceTicker))
                    allianceTicker = VictimAllianceID.ToString();
                return $"System: {SystemName}, Alliance: {allianceTicker}, Ship {ShipType}";
            }

            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
