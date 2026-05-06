using System.ComponentModel;
using SMT.EVEData;

namespace SMTAlert.Models
{
    /// <summary>
    /// UI-friendly wrapper around IntelData with display properties
    /// </summary>
    public class IntelAlertEntry : INotifyPropertyChanged
    {
        public IntelData Data { get; set; }

        private bool _inAlertRange;
        public bool InAlertRange
        {
            get => _inAlertRange;
            set
            {
                bool changed = _inAlertRange != value;
                _inAlertRange = value;
                if (changed)
                {
                    OnPropertyChanged(nameof(InAlertRange));
                    OnPropertyChanged(nameof(InAlertRangeVisibility));
                    OnPropertyChanged(nameof(RangeIndicator));
                }
                OnPropertyChanged(nameof(SystemItems));
            }
        }

        public System.Windows.Visibility InAlertRangeVisibility =>
            InAlertRange ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string RangeIndicator => InAlertRange ? "⚠" : "";

        public string SystemsDisplay => Data?.Systems != null
            ? string.Join(", ", Data.Systems)
            : "";

        public string IntelContent => Data?.IntelString ?? "";
        public string IntelContentFull => Data?.RawIntelString ?? "";

        public bool ClearNotification
        {
            get => Data?.ClearNotification ?? false;
            set { if (Data != null) Data.ClearNotification = value; }
        }

        // Pass-through to IntelData
        public string IntelString => Data?.IntelString ?? "";
        public string RawIntelString => Data?.RawIntelString ?? "";
        public System.DateTime IntelTime => Data?.IntelTime ?? System.DateTime.Now;
        public System.Collections.Generic.List<string> Systems => Data?.Systems ?? new();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public IEnumerable<SystemItemInfo> SystemItems
        {
            get
            {
                foreach (var sys in Systems)
                {
                    yield return new SystemItemInfo
                    {
                        Name = sys,
                        IsInRange = AlertManager.Instance?.IsSystemInRange(sys) ?? false,
                        IsClear = ClearNotification
                    };
                }
            }
        }
    }

    public class SystemItemInfo
    {
        public string Name { get; set; }
        public bool IsInRange { get; set; }
        public bool IsClear { get; set; }
    }
}
