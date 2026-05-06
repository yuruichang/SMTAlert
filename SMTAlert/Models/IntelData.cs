using System.ComponentModel;

namespace SMT.EVEData
{
    public class IntelData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public IntelData(string intelText, string intelChannel, DateTime gameTime)
        {
            RawIntelString = intelText;
            int start = intelText.IndexOf('>') + 1;
            IntelString = intelText.Substring(start);
            IntelTime = gameTime;
            Systems = new List<string>();
            ClearNotification = false;
            IntelChannel = intelChannel;
        }

        public bool ClearNotification { get; set; }
        public string IntelChannel { get; set; }
        public string IntelString { get; set; }
        public DateTime IntelTime { get; set; }
        public string RawIntelString { get; set; }
        public List<string> Systems { get; set; }
    }
}
