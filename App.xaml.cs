using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using SMT.EVEData;

namespace SMTAlert
{
    /// <summary>
    /// SMTAlert - Standalone ZKB kill feed and alert radar application.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Current version of the application.</summary>
        public const string AppVersion = "2.0";

        /// <summary>GitHub repository path for update checks.</summary>
        public const string GitHubRepo = "yuruichang/SMTAlert";

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

            // Set active character (first one if available) and auto-monitor
            if (CharacterMgr.Characters.Count > 0)
            {
                CharacterMgr.Characters[0].IsMonitored = true;
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

            // Check for updates on GitHub
            _ = CheckForUpdateAsync();
        }

        /// <summary>
        /// Checks the latest release on GitHub and prompts the user if a newer version is available.
        /// </summary>
        private static async Task CheckForUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SMTAlert");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

                var response = await client.GetStringAsync($"https://api.github.com/repos/{GitHubRepo}/releases/latest");
                var json = System.Text.Json.JsonDocument.Parse(response);
                var latestTag = json.RootElement.GetProperty("tag_name").GetString();
                var releaseUrl = json.RootElement.GetProperty("html_url").GetString();

                if (string.IsNullOrEmpty(latestTag)) return;

                // Strip leading "v" if present and compare versions
                string latestVersionStr = latestTag.TrimStart('v');
                string currentVersionStr = AppVersion;

                if (Version.TryParse(latestVersionStr, out var latestVersion) &&
                    Version.TryParse(currentVersionStr, out var currentVersion) &&
                    latestVersion > currentVersion)
                {
                    // New version available — prompt on UI thread
                    var appWindow = Current?.Dispatcher;
                    if (appWindow == null) return;

                    await appWindow.InvokeAsync(() =>
                    {
                        string msg = string.Format(
                            (string)Current.TryFindResource("Update_Available") ??
                            "A new version is available!\n\nCurrent: {0}\nLatest: {1}\n\nOpen the release page to download?",
                            currentVersionStr, latestVersionStr);

                        string title = (string)Current.TryFindResource("Update_Title") ?? "Update Available";

                        if (MessageBox.Show(AppWindow, msg, title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            if (!string.IsNullOrEmpty(releaseUrl))
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = releaseUrl,
                                    UseShellExecute = true
                                });
                        }
                    });
                }
            }
            catch
            {
                // Silently ignore network/parse errors — update check is non-critical
            }
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
                CharacterMgr.Characters[0].IsMonitored = true;
                ActiveCharacter = CharacterMgr.Characters[0];
                ActiveCharacter.IsActiveMonitor = true;
            }

            // Ensure at least one character is monitored
            if (CharacterMgr.Characters.Count > 0 && !CharacterMgr.Characters.Any(c => c.IsMonitored))
            {
                CharacterMgr.Characters[0].IsMonitored = true;
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

            // Load synchronously via XamlReader to ensure resources are available immediately
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(baseDir, "Languages", $"{langCode}.xaml");
            ResourceDictionary newDict;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                newDict = (ResourceDictionary)XamlReader.Load(fs);
            }
            newDict.Source = new Uri($"Languages/{langCode}.xaml", UriKind.Relative);

            Current.Resources.MergedDictionaries.Add(newDict);
            if (oldDict != null)
                Current.Resources.MergedDictionaries.Remove(oldDict);
        }
    }
}
