namespace SMT.EVEData
{
    public class MapRegion
    {
        public MapRegion() { }
        public MapRegion(string name, string id, string faction, double universeViewX, double universeViewY, bool metaRegion = false)
        {
            Name = name;
            ID = id;
            Faction = faction;
        }

        public string ID { get; set; }
        public string Name { get; set; }
        public string Faction { get; set; }

        public string LocalizedName => Name;
        public override string ToString() => Name;
    }
}
