using System.IO;

namespace SMT.EVEData
{
    public class EveAppConfig
    {
        public const string CallbackURL = @"http://localhost:8762/callback/";
        public const string ClientID = "c7d20420abb6437e8751090f24872a6f";
        public const string SMT_TITLE = "Fight Club!";
        public const string SMT_VERSION = "SMT_148";
        public const string SMT_USERAGENT_DETAILS = " (+https://github.com/Slazanger/SMT; eve:Slazanger, discord:Slazanger)";
        public static readonly string StorageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMT");
        public static readonly string VersionStorage = Path.Combine(StorageRoot, $"{SMT_VERSION}");
    }
}
