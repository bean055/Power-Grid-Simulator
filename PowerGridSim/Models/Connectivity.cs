using System.Collections.Generic;
using System.Linq;

namespace PowerGridSim.Models
{
    public static class Connectivity
    {
        // Each Connection is a list of node indices -  ignoresOpen cables 
        public static List<List<int>> FindConnections(PowerGridBlueprint bp, ISet<string> openCableIds)
        {
            int n = bp.Nodes.Count;
            var index = bp.BuildNodeIndex();
            var parent = Enumerable.Range(0, n).ToArray();

            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }

            foreach (var c in bp.Cables)
            {
                if (openCableIds.Contains(c.Id)) continue;
                if (!index.TryGetValue(c.FromNodeId, out int a) || !index.TryGetValue(c.ToNodeId, out int b)) continue;
                parent[Find(a)] = Find(b);
            }

            return Enumerable.Range(0, n)
                .GroupBy(Find)
                .Select(g => g.ToList())
                .ToList();
        }
    }
}