using System;
using System.Collections.Generic;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Traffic
{
    /// <summary>
    /// Trajectoire suivie par le trafic d'un sens (polyligne xz). Position d'un véhicule = distance le long de la
    /// trajectoire + écart latéral, positif à droite du sens de marche (on roule à droite au Cameroun).
    /// </summary>
    public sealed class TrafficPath
    {
        private readonly float[] _xz;
        private readonly float[] _cum;
        private readonly int[] _twoWay;

        public float Length { get; }
        /// <summary>Carrefours avec une rue latérale à droite, par distance croissante.</summary>
        public IReadOnlyList<Junction> Junctions { get; }

        public TrafficPath(float[] xz, int[] twoWay = null, IReadOnlyList<Junction> junctions = null)
        {
            _xz = xz ?? throw new ArgumentNullException(nameof(xz));
            if (xz.Length < 4) throw new ArgumentException("Il faut au moins deux points.", nameof(xz));
            _cum = WorldGeometry.CumulativeLengths(xz);
            _twoWay = twoWay ?? Array.Empty<int>();
            Length = _cum[_cum.Length - 1];
            Junctions = junctions ?? Array.Empty<Junction>();
        }

        public static TrafficPath From(TrafficPathData data)
        {
            var list = new List<Junction>();
            if (data.junctions != null)
                foreach (var j in data.junctions)
                    if (j.xz != null && j.xz.Length >= 4)
                        list.Add(new Junction(j.along, new TrafficPath(j.xz), j.canEnter != 0, j.canExit != 0, j.highway));
            list.Sort((a, b) => a.Along.CompareTo(b.Along));
            return new TrafficPath(data.xz, data.twoWay, list);
        }

        /// <summary>Point à <paramref name="along"/> m, décalé de <paramref name="lateral"/> m à droite ; direction de marche.</summary>
        public void Sample(float along, float lateral, out float x, out float z, out float dirX, out float dirZ)
        {
            WorldGeometry.SampleAlong(_xz, _cum, along, out x, out z, out dirX, out dirZ);
            // À droite du sens de marche (x = est, z = nord) : (dirZ, -dirX).
            x += dirZ * lateral;
            z += -dirX * lateral;
        }

        /// <summary>Projette (x, z) : distance le long de la trajectoire et écart latéral signé (positif à droite).</summary>
        public void Project(float x, float z, out float along, out float lateral)
        {
            var n = _cum.Length;
            along = 0f;
            lateral = 0f;
            var best = float.MaxValue;
            for (var i = 1; i < n; i++)
            {
                float ax = _xz[i * 2 - 2], az = _xz[i * 2 - 1], bx = _xz[i * 2], bz = _xz[i * 2 + 1];
                float ex = bx - ax, ez = bz - az;
                var len2 = ex * ex + ez * ez;
                var t = len2 > 1e-8f ? ((x - ax) * ex + (z - az) * ez) / len2 : 0f;
                t = Math.Max(0f, Math.Min(1f, t));
                float px = x - (ax + ex * t), pz = z - (az + ez * t);
                var d2 = px * px + pz * pz;
                if (d2 >= best) continue;
                best = d2;
                along = _cum[i - 1] + (_cum[i] - _cum[i - 1]) * t;
                var len = (float)Math.Sqrt(len2);
                // Signe : produit vectoriel (direction × point) ; à droite = négatif dans le repère x est, z nord.
                var cross = len > 1e-6f ? (ex * pz - ez * px) / len : 0f;
                lateral = -cross;
            }
        }

        /// <summary>Direction de marche (unitaire) à <paramref name="along"/>.</summary>
        public void Direction(float along, out float dirX, out float dirZ)
        {
            WorldGeometry.SampleAlong(_xz, _cum, along, out _, out _, out dirX, out dirZ);
        }

        /// <summary>La chaussée est à double sens à cet endroit (le trafic d'en face la partage).</summary>
        public bool IsTwoWay(float along)
        {
            if (_twoWay.Length == 0) return true;
            var i = Array.BinarySearch(_cum, Math.Max(0f, Math.Min(Length, along)));
            if (i < 0) i = ~i;
            var seg = Math.Max(0, Math.Min(_twoWay.Length - 1, i - 1));
            return _twoWay[seg] != 0;
        }
    }

    /// <summary>Carrefour avec une rue latérale à droite de la trajectoire.</summary>
    public sealed class Junction
    {
        public float Along { get; }
        /// <summary>La rue, depuis le carrefour vers l'extérieur.</summary>
        public TrafficPath Side { get; }
        public bool CanEnter { get; }
        public bool CanExit { get; }
        public string Highway { get; }

        public Junction(float along, TrafficPath side, bool canEnter, bool canExit, string highway = "")
        {
            Along = along;
            Side = side ?? throw new ArgumentNullException(nameof(side));
            CanEnter = canEnter;
            CanExit = canExit;
            Highway = highway ?? "";
        }

        /// <summary>Chemin de service (parking, cour) : bien moins de passage qu'une rue.</summary>
        public bool IsService => Highway == "service";
    }
}
