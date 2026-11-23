using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>Un chemin trouvé dans le réseau des rues.</summary>
    public sealed class RoadPath
    {
        public bool Found;
        public float LengthMeters;
        /// <summary>Points x, z du chemin, du départ à l'arrivée.</summary>
        public float[] Xz = Array.Empty<float>();
    }

    /// <summary>
    /// Réseau des rues en 2D (idée n° 7, première tranche pour sortir du couloir de l'itinéraire) : graphe des routes OSM
    /// de <see cref="WorldData.roads"/> (nœuds partagés aux carrefours, sens uniques respectés) et plus court chemin entre
    /// deux points. Sert à placer des arrêts dans les rues latérales et à mesurer un raccourci ; la logique des courses
    /// (<see cref="Rides.Street"/>) se repère encore le long de l'itinéraire.
    /// </summary>
    public sealed class RoadNetwork
    {
        private readonly List<float> _x = new List<float>();
        private readonly List<float> _z = new List<float>();
        private readonly List<List<(int to, float length)>> _edges = new List<List<(int, float)>>();
        private readonly Dictionary<long, int> _index = new Dictionary<long, int>();

        public int NodeCount => _x.Count;

        /// <summary>Deux sommets à moins de 0,5 m l'un de l'autre sont le même carrefour.</summary>
        private const float Snap = 0.5f;

        public static RoadNetwork Build(WorldData world)
        {
            var net = new RoadNetwork();
            foreach (var r in world.roads ?? Array.Empty<RoadData>())
            {
                if (r.xz == null || r.xz.Length < 4) continue;
                var previous = net.Node(r.xz[0], r.xz[1]);
                for (var i = 2; i + 1 < r.xz.Length; i += 2)
                {
                    var next = net.Node(r.xz[i], r.xz[i + 1]);
                    if (next == previous) continue;
                    var length = Distance(net._x[previous], net._z[previous], net._x[next], net._z[next]);
                    net._edges[previous].Add((next, length));
                    if (!r.oneway) net._edges[next].Add((previous, length));
                    previous = next;
                }
            }
            return net;
        }

        private int Node(float x, float z)
        {
            var key = (long)Math.Round(x / Snap) * 100_000_000L + (long)Math.Round(z / Snap);
            if (_index.TryGetValue(key, out var i)) return i;
            i = _x.Count;
            _x.Add(x);
            _z.Add(z);
            _edges.Add(new List<(int, float)>());
            _index[key] = i;
            return i;
        }

        /// <summary>Nœud le plus proche d'un point (-1 si le réseau est vide), et sa distance.</summary>
        public int Nearest(float x, float z, out float distance)
        {
            var best = -1;
            distance = float.MaxValue;
            for (var i = 0; i < _x.Count; i++)
            {
                var d = Distance(x, z, _x[i], _z[i]);
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        /// <summary>Plus court chemin par les rues entre les nœuds les plus proches des deux points (Dijkstra).</summary>
        public RoadPath ShortestPath(float fromX, float fromZ, float toX, float toZ)
        {
            var start = Nearest(fromX, fromZ, out _);
            var goal = Nearest(toX, toZ, out _);
            var result = new RoadPath();
            if (start < 0 || goal < 0) return result;
            var dist = new float[_x.Count];
            var prev = new int[_x.Count];
            for (var i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[start] = 0f;
            var heap = new MinHeap();
            heap.Push(start, 0f);
            while (heap.Count > 0)
            {
                heap.Pop(out var node, out var d);
                if (d > dist[node]) continue;
                if (node == goal) break;
                foreach (var (to, length) in _edges[node])
                {
                    var nd = d + length;
                    if (nd >= dist[to]) continue;
                    dist[to] = nd;
                    prev[to] = node;
                    heap.Push(to, nd);
                }
            }
            if (dist[goal] == float.MaxValue) return result;
            var nodes = new List<int>();
            for (var n = goal; n >= 0; n = prev[n]) nodes.Add(n);
            nodes.Reverse();
            result.Found = true;
            result.LengthMeters = dist[goal];
            result.Xz = new float[nodes.Count * 2];
            for (var i = 0; i < nodes.Count; i++)
            {
                result.Xz[2 * i] = _x[nodes[i]];
                result.Xz[2 * i + 1] = _z[nodes[i]];
            }
            return result;
        }

        private static float Distance(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        /// <summary>Tas binaire minimal (pas de PriorityQueue dans le .NET d'Unity).</summary>
        private sealed class MinHeap
        {
            private readonly List<(int node, float key)> _items = new List<(int, float)>();
            public int Count => _items.Count;

            public void Push(int node, float key)
            {
                _items.Add((node, key));
                var i = _items.Count - 1;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (_items[parent].key <= _items[i].key) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public void Pop(out int node, out float key)
            {
                (node, key) = _items[0];
                var last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count == 0) return;
                _items[0] = last;
                var i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _items.Count && _items[l].key < _items[m].key) m = l;
                    if (r < _items.Count && _items[r].key < _items[m].key) m = r;
                    if (m == i) break;
                    (_items[m], _items[i]) = (_items[i], _items[m]);
                    i = m;
                }
            }
        }
    }

    /// <summary>
    /// Arrêt placé en 2D (idée n° 7) : un lieu peut être dans une rue latérale (<see cref="Rides.Stop.x"/>,
    /// <see cref="Rides.Stop.z"/>). Son accès sur l'itinéraire est sa projection ; s'il en est loin, il est « hors de
    /// l'itinéraire » et le taxi doit quitter la grande route pour l'atteindre.
    /// </summary>
    public static class StopPlacement
    {
        /// <summary>Au-delà de cette distance à l'itinéraire (m), l'arrêt est dans une rue latérale.</summary>
        public const float OffRouteMeters = 30f;

        /// <summary>Distance le long de l'itinéraire de l'accès à l'arrêt et distance latérale (0 pour un arrêt sans position).</summary>
        public static float AccessAlong(Rides.Stop stop, float[] routeXz, float[] cumulative, out float lateral)
        {
            lateral = 0f;
            if (stop == null || !stop.HasPosition) return stop?.distanceAlongRouteMeters ?? 0f;
            WorldGeometry.Project(routeXz, cumulative, stop.x, stop.z, out var along, out var offset);
            lateral = Math.Abs(offset);
            return along;
        }

        public static bool IsOffRoute(Rides.Stop stop, float[] routeXz, float[] cumulative)
        {
            AccessAlong(stop, routeXz, cumulative, out var lateral);
            return lateral > OffRouteMeters;
        }
    }
}
