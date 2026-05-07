using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SMT.EVEData;

namespace SMTAlert
{
    public partial class AlertChannelWindow : Window
    {
        private string _channelFilter = "";

        public AlertChannelWindow()
        {
            InitializeComponent();
            Topmost = App.Config.AlwaysOnTop;
            UpdateChannelInfo();

            EveManager.Instance.IntelUpdatedEvent += OnIntelUpdated;
            App.Config.PropertyChanged += OnConfigChanged;
            Closed += (s, e) =>
            {
                EveManager.Instance.IntelUpdatedEvent -= OnIntelUpdated;
                App.Config.PropertyChanged -= OnConfigChanged;
            };

            // Load existing messages
            if (EveManager.Instance.IntelDataList != null)
            {
                foreach (var item in EveManager.Instance.IntelDataList.ToList())
                    AddMessage(item);
            }
        }

        private void OnConfigChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlertConfig.AlertChannelName) ||
                e.PropertyName == nameof(AlertConfig.AlertClearKeywords))
            {
                Dispatcher.Invoke(() => UpdateChannelInfo());
            }
            else if (e.PropertyName == nameof(AlertConfig.AlwaysOnTop))
            {
                Dispatcher.Invoke(() => Topmost = App.Config.AlwaysOnTop);
            }
        }

        private void UpdateChannelInfo()
        {
            bool zh = EveManager.CurrentLanguage == "zh-CN";
            _channelFilter = App.Config.AlertChannelName;
            ChannelNameLabel.Content = string.IsNullOrEmpty(_channelFilter)
                ? (zh ? "(未设置)" : "(not set)")
                : _channelFilter;
            ClearKeywordsLabel.Content = string.IsNullOrEmpty(App.Config.AlertClearKeywords)
                ? (zh ? "(未设置)" : "(not set)")
                : App.Config.AlertClearKeywords;
        }

        private void OnIntelUpdated(List<IntelData> items)
        {
            Dispatcher.Invoke(() =>
            {
                // Only add the newest item (FixedQueue inserts at index 0, so index 0 is newest)
                var newest = items.FirstOrDefault();
                if (newest != null && ChannelMatches(newest.IntelChannel))
                    AddMessage(newest);

                // Trim old messages
                while (MessageListBox.Items.Count > 200)
                    MessageListBox.Items.RemoveAt(0);

                // Auto-scroll to bottom
                if (MessageListBox.Items.Count > 0)
                    MessageListBox.ScrollIntoView(MessageListBox.Items[MessageListBox.Items.Count - 1]);
            });
        }

        private bool ChannelMatches(string intelChannel)
        {
            if (string.IsNullOrEmpty(_channelFilter))
                return true;
            if (string.IsNullOrEmpty(intelChannel))
                return false;
            return intelChannel.Contains(_channelFilter, StringComparison.OrdinalIgnoreCase);
        }

        private void AddMessage(IntelData id)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(2, 1, 2, 1) };

            // Parse the raw intel string: [ 2026.05.07 00:23:38 ] CharName > message
            string raw = id.RawIntelString ?? "";
            string timeStr = "";
            string charName = "";
            string msgText = raw;

            if (raw.StartsWith("["))
            {
                int closeBracket = raw.IndexOf(']');
                if (closeBracket > 1)
                {
                    timeStr = raw.Substring(1, closeBracket - 1).Trim();
                    int gtPos = raw.IndexOf('>', closeBracket);
                    if (gtPos > closeBracket)
                    {
                        charName = raw.Substring(closeBracket + 1, gtPos - closeBracket - 1).Trim();
                        msgText = raw.Substring(gtPos + 1).Trim();
                    }
                    else
                    {
                        msgText = raw.Substring(closeBracket + 1).Trim();
                    }
                }
            }

            // Header: time + character
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(timeStr) ? id.IntelTime.ToString("HH:mm:ss") : timeStr,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888")),
                FontSize = 11
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = charName,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5588FF")),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(5, 0, 0, 0)
            });
            panel.Children.Add(headerPanel);

            // Message body with system name highlighting
            var msgBlock = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            if (!string.IsNullOrEmpty(msgText))
            {
                BuildHighlightedText(msgBlock, msgText);
            }
            panel.Children.Add(msgBlock);

            MessageListBox.Items.Add(panel);
        }

        private void BuildHighlightedText(TextBlock textBlock, string message)
        {
            var em = EveManager.Instance;
            var words = message.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].TrimEnd(',', '.', '!', '?', ':', ';', '*');
                bool isSystem = false;
                try
                {
                    isSystem = em.GetEveSystem(word) != null;
                }
                catch { }

                var run = new Run(words[i])
                {
                    Foreground = isSystem
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF8844"))
                        : Brushes.White
                };
                textBlock.Inlines.Add(run);

                if (i < words.Length - 1)
                    textBlock.Inlines.Add(new Run(" "));
            }

            // Check for clear keywords
            var keywords = App.Config.AlertClearKeywords;
            if (!string.IsNullOrEmpty(keywords))
            {
                var keywordList = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var kw in keywordList)
                {
                    if (message.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        var clearRun = new Run(" [CLEAR]")
                        {
                            Foreground = new SolidColorBrush(Colors.LightGreen),
                            FontWeight = FontWeights.Bold
                        };
                        textBlock.Inlines.Add(clearRun);
                        break;
                    }
                }
            }
        }
    }
}
