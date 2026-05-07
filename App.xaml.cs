using System.Windows;
using SMT.EVEData;

namespace SMTAlert
{
    /// <summary>
    /// SMTAlert - Standalone ZKB kill feed and alert radar application.
    /// </summary>
    public partial class App : Application
    {
        public static AlertConfig Config { get; private set; }
        public static CharacterManager CharacterMgr { get; private set; }
        public static ZKillRedisQ ZKillFeed { get; private set; }
        public static AlertCharacter ActiveCharacter { get; set; }

        public static MainWindow AppWindow { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Load config
            Config = AlertConfig.Load();

            // Apply language before any UI is created
            ApplyLanguage(Config.Language);

            // Initialize EVE data manager (systems, regions, ship types, etc.)
            var eveManager = new EveManager("SMTAlert_100");
            EveManager.Instance = eveManager;
            EveManager.UIThreadInvoker = (action) =>
            {
                if (Dispatcher.CheckAccess())
                    action();
                else
                    Dispatcher.Invoke(action);
            };
            eveManager.LoadFromDisk();
            eveManager.InitNavigation();
            eveManager.LoadShipTypesCNFromCache();

            // Apply custom log folder path before setting up watchers
            if (!string.IsNullOrEmpty(Config.EveLogFolder))
                eveManager.EVELogFolder = Config.EveLogFolder;

            // Enable chat log + intel monitoring for alert channel
            eveManager.SetupIntelWatcher();
            eveManager.SetupGameLogWatcher();
            eveManager.SetupLogFileTriggers();

            // Register alert channel filter so Chatlogs watcher processes the right files
            UpdateIntelChannelFilter();
            UpdateIntelClearFilters();

            // React to channel config changes
            Config.PropertyChanged += (s, pe) =>
            {
                if (pe.PropertyName == nameof(AlertConfig.AlertChannelName))
                    UpdateIntelChannelFilter();
                if (pe.PropertyName == nameof(AlertConfig.AlertClearKeywords))
                    UpdateIntelClearFilters();
            };

            // Use EveManager's ZKB feed
            ZKillFeed = eveManager.ZKillFeed;
            ZKillFeed.KillExpireTimeMinutes = Config.ZkbExpireMinutes;

            // Initialize character manager
            CharacterMgr = new CharacterManager();
            CharacterMgr.Initialize();
            CharacterMgr.CharactersChanged += OnCharactersChanged;

            // Set active character (first one if available)
            if (CharacterMgr.Characters.Count > 0)
            {
                ActiveCharacter = CharacterMgr.Characters[0];
                ActiveCharacter.IsActiveMonitor = true;
            }

            // Show main window
            AppWindow = new MainWindow();
            AppWindow.Show();

            // Fetch Chinese ship names async
            _ = eveManager.FetchShipTypeChineseNamesAsync().ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (var item in ZKillFeed.KillStream)
                        item.RefreshShipTypeDisplay();
                });
            });
        }

        private static void UpdateIntelChannelFilter()
        {
            var filters = EveManager.Instance?.IntelFilters;
            if (filters == null) return;

            string channel = Config.AlertChannelName;

            // Remove any previously added SMTAlert channel filters
            filters.RemoveAll(f => f.StartsWith("__smta_"));

            if (!string.IsNullOrEmpty(channel))
            {
                // Add as a prefixed filter so we can identify and replace it later
                filters.Add("__smta_" + channel);

                // Also add the raw channel name for matching
                if (!filters.Contains(channel))
                    filters.Add(channel);
            }
        }

        private static void UpdateIntelClearFilters()
        {
            var clearFilters = EveManager.Instance?.IntelClearFilters;
            if (clearFilters == null) return;

            // Remove previously added SMTAlert clear keywords
            clearFilters.RemoveAll(f => f.StartsWith("__smta_"));

            string keywords = Config.AlertClearKeywords;
            if (!string.IsNullOrEmpty(keywords))
            {
                var parts = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var kw in parts)
                {
                    if (!string.IsNullOrEmpty(kw) && !clearFilters.Contains(kw, StringComparer.OrdinalIgnoreCase))
                        clearFilters.Add(kw);
                }
            }
        }

        private void OnCharactersChanged()
        {
            if (ActiveCharacter == null && CharacterMgr.Characters.Count > 0)
            {
                ActiveCharacter = CharacterMgr.Characters[0];
                ActiveCharacter.IsActiveMonitor = true;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            EveManager.Instance?.ShutDown();
            CharacterMgr?.Shutdown();
            Config?.Save();
            base.OnExit(e);
        }

        /// <summary>
        /// Apply language ResourceDictionary before UI loads.
        /// </summary>
        public static void ApplyLanguage(string langCode)
        {
            EveManager.CurrentLanguage = langCode;

            ResourceDictionary oldDict = null;
            foreach (var dict in Current.Resources.MergedDictionaries)
            {
                if (dict.Source != null && dict.Source.OriginalString.StartsWith("Languages/"))
                {
                    oldDict = dict;
                    break;
                }
            }

            var newDict = new ResourceDictionary
            {
                Source = new Uri($"Languages/{langCode}.xaml", UriKind.Relative)
            };
            Current.Resources.MergedDictionaries.Add(newDict);
            if (oldDict != null)
                Current.Resources.MergedDictionaries.Remove(oldDict);
        }
    }
}
