using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SMT.EVEData;

namespace SMTAlert
{
    public class ZKBBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var zs = value as ZKillRedisQ.ZKBDataSimple;
            Color rowCol = (Color)ColorConverter.ConvertFromString("#FF333333");

            if (zs != null)
            {
                var c = App.ActiveCharacter;
                if (c != null)
                {
                    float standing = 0.0f;

                    // Same alliance → friendly
                    if (c.AllianceID != 0 && c.AllianceID == zs.VictimAllianceID)
                        standing = 10.0f;

                    // Alliance standings from character's alliance contacts override
                    if (c.Standings.TryGetValue(zs.VictimAllianceID, out float allianceStanding))
                        standing = allianceStanding;

                    if (standing == -10.0f)
                        rowCol = Colors.Red;
                    else if (standing == -5.0f)
                        rowCol = Colors.Orange;
                    else if (standing == 5.0f)
                        rowCol = Colors.LightBlue;
                    else if (standing == 10.0f)
                        rowCol = Colors.Blue;
                }

                // Highlight high-value kills (>1B ISK) with a gold tint
                if (zs.TotalValue > 1_000_000_000)
                {
                    Color gold = Color.FromRgb(255, 215, 0);
                    rowCol = Color.FromArgb(
                        255,
                        (byte)(rowCol.R * 0.55 + gold.R * 0.45),
                        (byte)(rowCol.G * 0.55 + gold.G * 0.45),
                        (byte)(rowCol.B * 0.55 + gold.B * 0.45));
                }
            }

            return new SolidColorBrush(rowCol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }

    public class ZKBForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var zs = value as ZKillRedisQ.ZKBDataSimple;
            Color rowCol = Colors.White;

            if (zs != null)
            {
                var c = App.ActiveCharacter;
                if (c != null)
                {
                    float standing = 0.0f;

                    if (c.AllianceID != 0 && c.AllianceID == zs.VictimAllianceID)
                        standing = 10.0f;

                    if (c.Standings.TryGetValue(zs.VictimAllianceID, out float allianceStanding))
                        standing = allianceStanding;

                    bool highValue = zs.TotalValue > 1_000_000_000;

                    if (standing == -10.0f || standing == -5.0f)
                    {
                        // Dark backgrounds (red/orange) need light text even with gold tint
                        rowCol = highValue ? Color.FromRgb(255, 255, 200) : Colors.Black;
                    }
                    else if (highValue)
                    {
                        // Gold text for high-value rows on dark/blue backgrounds
                        rowCol = Color.FromRgb(255, 230, 80);
                    }
                }
            }

            return new SolidColorBrush(rowCol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }

    public class ZKBFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var zs = value as ZKillRedisQ.ZKBDataSimple;
            if (zs != null && zs.TotalValue > 1_000_000_000)
                return FontWeights.Bold;

            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
