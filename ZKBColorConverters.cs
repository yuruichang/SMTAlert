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

                    if (standing == -10.0f || standing == -5.0f)
                        rowCol = Colors.Black;
                }
            }

            return new SolidColorBrush(rowCol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
