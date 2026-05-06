using System.ComponentModel;
using System.Xml.Serialization;

namespace SMTAlert.Models
{
    public class AlertConfig : INotifyPropertyChanged
    {
        public static readonly string SaveVersion = "01";

        private string _language = "zh-CN";
        private int _alertRange = 5;
        private int _zkbMaxKills = 50;
        private int _zkbExpireMinutes = 30;
        private int _maxIntelSeconds = 120;
        private float _intelSoundVolume = 0.5f;
        private bool _playIntelSound = true;
        private bool _alwaysOnTop = true;
        private bool _zkbFilterByRegion = true;
        private float _zkbBackgroundOpacity = 0.2f;
        private float _zkbContentOpacity = 0.85f;
        private float _intelBackgroundOpacity = 0.15f;
        private float _intelContentOpacity = 0.85f;

        [Browsable(false)]
        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(nameof(Language)); }
        }

        [Category("Alert")]
        [DisplayName("预警范围(跳)")]
        [Description("角色周围多少跳范围内的预警触发警报")]
        public int AlertRange
        {
            get => _alertRange;
            set { _alertRange = Math.Max(1, value); OnPropertyChanged(nameof(AlertRange)); }
        }

        [Category("Alert")]
        [DisplayName("最大预警时间(秒)")]
        [Description("超过此时间的预警不再显示")]
        public int MaxIntelSeconds
        {
            get => _maxIntelSeconds;
            set { _maxIntelSeconds = Math.Max(30, value); OnPropertyChanged(nameof(MaxIntelSeconds)); }
        }

        [Category("Alert")]
        [DisplayName("播放预警声音")]
        public bool PlayIntelSound
        {
            get => _playIntelSound;
            set { _playIntelSound = value; OnPropertyChanged(nameof(PlayIntelSound)); }
        }

        [Category("Alert")]
        [DisplayName("预警音量")]
        public float IntelSoundVolume
        {
            get => _intelSoundVolume;
            set { _intelSoundVolume = Math.Clamp(value, 0f, 1f); OnPropertyChanged(nameof(IntelSoundVolume)); }
        }

        [Category("ZKB")]
        [DisplayName("最大击杀数")]
        public int ZKBMaxKills
        {
            get => _zkbMaxKills;
            set { _zkbMaxKills = Math.Clamp(value, 10, 200); OnPropertyChanged(nameof(ZKBMaxKills)); }
        }

        [Category("ZKB")]
        [DisplayName("击杀过期时间(分钟)")]
        public int ZKBExpireMinutes
        {
            get => _zkbExpireMinutes;
            set { _zkbExpireMinutes = Math.Max(5, value); OnPropertyChanged(nameof(ZKBExpireMinutes)); }
        }

        [Category("ZKB")]
        [DisplayName("仅显示当前星域")]
        public bool ZKBFilterByRegion
        {
            get => _zkbFilterByRegion;
            set { _zkbFilterByRegion = value; OnPropertyChanged(nameof(ZKBFilterByRegion)); }
        }

        [Category("ZKB")]
        [DisplayName("背景透明度")]
        public float ZKBBackgroundOpacity
        {
            get => _zkbBackgroundOpacity;
            set { _zkbBackgroundOpacity = Math.Clamp(value, 0.05f, 1f); OnPropertyChanged(nameof(ZKBBackgroundOpacity)); }
        }

        [Category("ZKB")]
        [DisplayName("内容透明度")]
        public float ZKBContentOpacity
        {
            get => _zkbContentOpacity;
            set { _zkbContentOpacity = Math.Clamp(value, 0.1f, 1f); OnPropertyChanged(nameof(ZKBContentOpacity)); }
        }

        [Category("Intel")]
        [DisplayName("背景透明度")]
        public float IntelBackgroundOpacity
        {
            get => _intelBackgroundOpacity;
            set { _intelBackgroundOpacity = Math.Clamp(value, 0.05f, 1f); OnPropertyChanged(nameof(IntelBackgroundOpacity)); }
        }

        [Category("Intel")]
        [DisplayName("内容透明度")]
        public float IntelContentOpacity
        {
            get => _intelContentOpacity;
            set { _intelContentOpacity = Math.Clamp(value, 0.1f, 1f); OnPropertyChanged(nameof(IntelContentOpacity)); }
        }

        [Category("General")]
        [DisplayName("窗口置顶")]
        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set { _alwaysOnTop = value; OnPropertyChanged(nameof(AlwaysOnTop)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
