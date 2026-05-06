using System.Windows;
using SMTAlert.Models;
using Application = System.Windows.Application;

namespace SMTAlert
{
    public partial class SettingsWindow : Window
    {
        private AlertConfig _config;

        public event Action<string?> LanguageChanged;
        public event Action SettingsSaved;

        public SettingsWindow(AlertConfig config)
        {
            InitializeComponent();
            _config = config;
            LoadValues();
        }

        private void LoadValues()
        {
            // Language
            if (_config.Language == "zh-CN")
                LangCN.IsChecked = true;
            else
                LangEN.IsChecked = true;

            // Alert
            AlertRangeSlider.Value = _config.AlertRange;
            AlertRangeLabel.Text = _config.AlertRange.ToString();
            MaxIntelBox.Text = _config.MaxIntelSeconds.ToString();
            PlaySoundChk.IsChecked = _config.PlayIntelSound;
            VolumeSlider.Value = _config.IntelSoundVolume;
            VolumeLabel.Text = _config.IntelSoundVolume.ToString("F1");

            // Intel Window
            IntelBgOpacitySlider.Value = _config.IntelBackgroundOpacity;
            IntelBgOpacityLabel.Text = _config.IntelBackgroundOpacity.ToString("F2");
            IntelContentOpacitySlider.Value = _config.IntelContentOpacity;
            IntelContentOpacityLabel.Text = _config.IntelContentOpacity.ToString("F2");

            // ZKB
            MaxKillsBox.Text = _config.ZKBMaxKills.ToString();
            ExpireBox.Text = _config.ZKBExpireMinutes.ToString();
            FilterByRegionChk.IsChecked = _config.ZKBFilterByRegion;
            BgOpacitySlider.Value = _config.ZKBBackgroundOpacity;
            BgOpacityLabel.Text = _config.ZKBBackgroundOpacity.ToString("F2");
            ContentOpacitySlider.Value = _config.ZKBContentOpacity;
            ContentOpacityLabel.Text = _config.ZKBContentOpacity.ToString("F2");

            // Intel Channel Filters
            var mgr = AlertManager.Instance;
            if (mgr != null)
            {
                ChannelFilterBox.Text = mgr.ChannelFiltersText;
                AlertKeywordsBox.Text = mgr.AlertKeywordsText;
                ClearKeywordsBox.Text = mgr.ClearKeywordsText;
            }

            // General
            AlwaysOnTopChk.IsChecked = _config.AlwaysOnTop;
        }

        private void AlertRangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AlertRangeLabel != null)
                AlertRangeLabel.Text = ((int)e.NewValue).ToString();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeLabel != null)
                VolumeLabel.Text = e.NewValue.ToString("F1");
        }

        private void IntelBgOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IntelBgOpacityLabel != null)
                IntelBgOpacityLabel.Text = e.NewValue.ToString("F2");
        }

        private void IntelContentOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IntelContentOpacityLabel != null)
                IntelContentOpacityLabel.Text = e.NewValue.ToString("F2");
        }

        private void BgOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BgOpacityLabel != null)
                BgOpacityLabel.Text = e.NewValue.ToString("F2");
        }

        private void ContentOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ContentOpacityLabel != null)
                ContentOpacityLabel.Text = e.NewValue.ToString("F2");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string oldLang = _config.Language;
            string newLang = LangCN.IsChecked == true ? "zh-CN" : "en-US";

            _config.Language = newLang;
            _config.AlertRange = (int)AlertRangeSlider.Value;
            if (int.TryParse(MaxIntelBox.Text, out int mi)) _config.MaxIntelSeconds = Math.Max(30, mi);
            _config.PlayIntelSound = PlaySoundChk.IsChecked == true;
            _config.IntelSoundVolume = (float)VolumeSlider.Value;
            if (int.TryParse(MaxKillsBox.Text, out int mk)) _config.ZKBMaxKills = Math.Clamp(mk, 10, 200);
            if (int.TryParse(ExpireBox.Text, out int ex)) _config.ZKBExpireMinutes = Math.Max(5, ex);
            _config.ZKBFilterByRegion = FilterByRegionChk.IsChecked == true;
            _config.IntelBackgroundOpacity = (float)IntelBgOpacitySlider.Value;
            _config.IntelContentOpacity = (float)IntelContentOpacitySlider.Value;
            _config.ZKBBackgroundOpacity = (float)BgOpacitySlider.Value;
            _config.ZKBContentOpacity = (float)ContentOpacitySlider.Value;
            _config.AlwaysOnTop = AlwaysOnTopChk.IsChecked == true;

            // Save intel channel filters
            var mgr = AlertManager.Instance;
            if (mgr != null)
            {
                mgr.ChannelFiltersText = ChannelFilterBox.Text;
                mgr.AlertKeywordsText = AlertKeywordsBox.Text;
                mgr.ClearKeywordsText = ClearKeywordsBox.Text;
            }

            AlertManager.Instance.SaveConfig();
            AlertManager.Instance.UpdateVolume();

            if (oldLang != newLang)
                LanguageChanged?.Invoke(newLang);

            SettingsSaved?.Invoke();

            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.Topmost = _config.AlwaysOnTop;

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
