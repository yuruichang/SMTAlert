using System.Globalization;
using System.Windows.Data;
using Media = System.Windows.Media;

namespace SMTAlert.Converters
{
    public abstract class StandingConverterBase
    {
        protected static float GetStanding(object value)
        {
            if (value is not SMT.EVEData.ZKillRedisQ.ZKBDataSimple zs)
                return 0f;
            if (AlertManager.Instance?.ActiveCharacter is not { ESILinked: true } c)
                return 0f;

            if (c.AllianceID != 0 && c.AllianceID == zs.VictimAllianceID)
                return 10.0f;
            if (c.Standings.TryGetValue(zs.VictimAllianceID, out float standing))
                return standing;
            return 0f;
        }
    }

    public class StandingBackgroundConverter : StandingConverterBase, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float standing = GetStanding(value);
            var rowCol = standing switch
            {
                -10.0f => Media.Colors.Red,
                -5.0f => Media.Colors.Orange,
                5.0f => Media.Colors.LightBlue,
                10.0f => Media.Colors.Blue,
                _ => (Media.Color)Media.ColorConverter.ConvertFromString("#FF333333")
            };
            return new Media.SolidColorBrush(rowCol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }

    public class StandingForegroundConverter : StandingConverterBase, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float standing = GetStanding(value);
            var rowCol = standing switch
            {
                -10.0f => Media.Colors.Black,
                -5.0f => Media.Colors.Black,
                5.0f => Media.Colors.Black,
                10.0f => Media.Colors.White,
                _ => Media.Colors.White
            };
            return new Media.SolidColorBrush(rowCol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
