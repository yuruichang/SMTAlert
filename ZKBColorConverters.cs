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

            if (zs != null && App.ActiveCharacter != null)
            {
                var c = App.ActiveCharacter;
                float standing = 0.0f;

                // Same alliance or same corporation → friendly
                if ((c.AllianceID != 0 && c.AllianceID == zs.VictimAllianceID) ||
                    (c.CorporationID != 0 && c.CorporationID == zs.VictimCorpID))
                {
                    standing = 10.0f;
                }
                else
                {
                    // Check standings for both alliance and corporation, take the lowest (most relevant)
                    float bestStanding = 0.0f;
                    bool hasStanding = false;

                    if (zs.VictimAllianceID != 0 && c.Standings.TryGetValue(zs.VictimAllianceID, out float aStanding))
                    {
                        bestStanding = aStanding;
                        hasStanding = true;
                    }
                    if (zs.VictimCorpID != 0 && c.Standings.TryGetValue(zs.VictimCorpID, out float cStanding))
                    {
                        if (!hasStanding || cStanding < bestStanding)
                            bestStanding = cStanding;
                        hasStanding = true;
                    }

                    standing = bestStanding;
                }

                if (standing <= -5.0f)
                    rowCol = Colors.Red;
                else if (standing < 0.0f)
                    rowCol = Colors.Orange;
                else if (standing > 0.0f && standing < 5.0f)
                    rowCol = Colors.LightBlue;
                else if (standing >= 5.0f)
                    rowCol = Colors.Blue;
            }

            return new SolidColorBrush(rowCol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }

    public class ZKBForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
