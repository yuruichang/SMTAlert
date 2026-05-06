namespace SMT.EVEData
{
    public enum RoutingMode { Shortest, Safest, PreferLow }

    public class Navigation
    {
        public enum GateType { StarGate, Ansiblex, JumpTo, Thera, Zarzakh, Turnur }

        private static Dictionary<string, MapNode> MapNodes { get; set; } = new();

        public static void InitNavigation(List<System> eveSystems)
        {
            MapNodes = new Dictionary<string, MapNode>();

            foreach (System sys in eveSystems)
            {
                MapNode mn = new MapNode
                {
                    Name = sys.Name,
                    Connections = new List<string>(),
                };

                foreach (string s in sys.Jumps)
                {
                    mn.Connections.Add(s);
                }

                MapNodes[mn.Name] = mn;
            }
        }

        public static List<string> GetSystemsXJumpsFrom(List<string> sysList, string start, int X)
        {
            if (MapNodes == null || !MapNodes.ContainsKey(start))
                return sysList;

            if (X != 0)
            {
                if (!sysList.Contains(start))
                    sysList.Add(start);

                MapNode mn = MapNodes[start];

                foreach (string mm in mn.Connections)
                {
                    if (!sysList.Contains(mm))
                        sysList.Add(mm);

                    List<string> connected = GetSystemsXJumpsFrom(sysList, mm, X - 1);
                    foreach (string s in connected)
                    {
                        if (!sysList.Contains(s))
                            sysList.Add(s);
                    }
                }
            }
            return sysList;
        }

        public class RoutePoint
        {
            public GateType GateToTake { get; set; }
            public decimal LY { get; set; }
            public string SystemName { get; set; }
            public System ActualSystem { get; set; }

            public override string ToString() => SystemName;
        }

        private class MapNode
        {
            public List<string> Connections { get; set; }
            public string Name { get; set; }
        }
    }
}
