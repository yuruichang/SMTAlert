namespace SMT.EVEData
{
    public class Character
    {
        public Character()
        {
            CorporationID = -1;
            AllianceID = -1;
            ID = -1;
        }

        public long AllianceID { get; set; }
        public string AllianceName { get; set; }
        public string AllianceTicker { get; set; }
        public long CorporationID { get; set; }
        public string CorporationName { get; set; }
        public string CorporationTicker { get; set; }
        public long ID { get; set; }
        public string Name { get; set; }
    }
}
