namespace SMT.EVEData
{
    public class System
    {
        public System()
        {
            Jumps = new List<string>();
        }

        public System(string name, long id, string region, bool station, bool iceBelt)
        {
            Name = name;
            ID = id;
            Region = region;
            HasNPCStation = station;
            HasIceBelt = iceBelt;
            Jumps = new List<string>();
        }

        public decimal ActualX { get; set; }
        public decimal ActualY { get; set; }
        public decimal ActualZ { get; set; }
        public string ConstellationID { get; set; }
        public string ConstellationName { get; set; }
        public bool HasIceBelt { get; set; }
        public bool HasNPCStation { get; set; }
        public bool HasJoveObservatory { get; set; }
        public long ID { get; set; }
        public List<string> Jumps { get; set; }
        public string Name { get; set; }
        public long RegionID { get; set; }
        public string Region { get; set; }
        public double TrueSec { get; set; }
        public int SOVAllianceID { get; set; }

        public string LocalizedName => Name;

        public override string ToString() => $"{Name} ({Region})";
    }
}
