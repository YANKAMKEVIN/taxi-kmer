using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Emplacements de matériau du monde habillé (un sous-maillage par emplacement dans chaque bloc). L'ordre est celui des
    /// matériaux chargés par l'affichage (World/CityMaterials) : ne pas renuméroter sans lui.
    /// </summary>
    public enum CitySlot
    {
        RoadPaved = 0,
        RoadDirt = 1,
        Median = 2,
        Lamp = 3,
        Wall0 = 4, // 8 murs : 4 à 11
        RoofTin = 12,
        RoofRust = 13,
        RoofConcrete = 14,
        Windows = 15,
        Signs = 16,
    }

    /// <summary>Réglages de l'habillage des bâtiments (city_style.json), valeurs par défaut de Claude à valider sur captures.</summary>
    [Serializable]
    public class CityStyleConfig
    {
        /// <summary>Nombre de variantes de mur (matériaux Wall0…).</summary>
        public int wallVariants = 8;
        /// <summary>Distances à l'itinéraire (m) : en dessous de <see cref="nearRouteMeters"/>, immeubles de 2 à 4 niveaux.</summary>
        public float nearRouteMeters = 45f;
        public float midRouteMeters = 120f;
        /// <summary>Probabilités du nombre de niveaux près de l'itinéraire (R+0, R+1, R+2, R+3).</summary>
        public float[] nearLevels = { 0.25f, 0.35f, 0.25f, 0.15f };
        public float[] midLevels = { 0.6f, 0.3f, 0.1f, 0f };
        public float[] farLevels = { 0.85f, 0.15f, 0f, 0f };
        /// <summary>Part des bâtiments de plain-pied couverts de tôle (le reste en dalle béton) ; part de tôle rouillée.</summary>
        public float tinRoofShare = 0.85f;
        public float rustShare = 0.35f;
        /// <summary>Part des bâtiments près de l'itinéraire dont le rez-de-chaussée est une boutique (rideaux, enseigne).</summary>
        public float shopShare = 0.6f;
        /// <summary>Fenêtres, portes et boutiques dessinées seulement à moins de cette distance de l'itinéraire (m) : perf mobile.</summary>
        public float detailMeters = 90f;
        public float levelHeight = 3f;
        public float windowSpacing = 3.2f;
        /// <summary>Faîte des toits en tôle à deux pans (m au-dessus des murs) et débord du toit (m).</summary>
        public float ridgeHeight = 1.3f;
        public float roofOverhang = 0.45f;
        public float parapetHeight = 0.6f;
    }

    public enum RoofKind { Concrete, TinGable, TinFlat }

    /// <summary>Habillage d'un bâtiment, tiré de façon reproductible (même bâtiment → même façade à chaque lancement).</summary>
    public struct BuildingStyle
    {
        public int Levels;
        public int Wall;
        public RoofKind Roof;
        public bool Rust;
        public bool Shop;
        /// <summary>Case de fenêtre dans l'atlas (0 à 11).</summary>
        public int Window;
        /// <summary>Case d'enseigne (0 à 15) et de porte (12 ou 13).</summary>
        public int Sign;
        public int Door;
        public bool Detailed;
    }

    public static class CityStyle
    {
        /// <summary>Style d'un bâtiment d'après sa distance à l'itinéraire et un tirage reproductible (graine = son rang).</summary>
        public static BuildingStyle For(CityStyleConfig c, int seed, int corners, float distanceToRoute)
        {
            var r = new Random(unchecked(seed * 7919 + 17));
            var near = distanceToRoute < c.nearRouteMeters;
            var weights = near ? c.nearLevels : distanceToRoute < c.midRouteMeters ? c.midLevels : c.farLevels;
            var levels = 1 + Pick(weights, r.NextDouble());
            var tin = levels == 1 && r.NextDouble() < c.tinRoofShare;
            return new BuildingStyle
            {
                Levels = levels,
                Wall = r.Next(Math.Max(1, c.wallVariants)),
                Roof = !tin ? RoofKind.Concrete : corners == 4 ? RoofKind.TinGable : RoofKind.TinFlat,
                Rust = r.NextDouble() < c.rustShare,
                Shop = near && r.NextDouble() < c.shopShare,
                Window = r.Next(12),
                Sign = r.Next(16),
                Door = 12 + r.Next(2),
                Detailed = distanceToRoute < c.detailMeters,
            };
        }

        /// <summary>Indice tiré selon des poids (somme quelconque) ; le dernier si les poids sont nuls.</summary>
        public static int Pick(float[] weights, double roll)
        {
            if (weights == null || weights.Length == 0) return 0;
            var total = 0f;
            foreach (var w in weights) total += Math.Max(0f, w);
            if (total <= 0f) return 0;
            var x = roll * total;
            for (var i = 0; i < weights.Length; i++)
            {
                x -= Math.Max(0f, weights[i]);
                if (x < 0) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>Hauteur des murs (m), parapet non compris.</summary>
        public static float WallHeight(CityStyleConfig c, BuildingStyle s) => s.Levels * c.levelHeight;
    }

    /// <summary>Les sous-maillages d'un bloc du monde, un par emplacement de matériau.</summary>
    public sealed class MeshSet
    {
        private readonly Dictionary<int, MeshData> _meshes = new Dictionary<int, MeshData>();

        public MeshData this[CitySlot slot] => Get((int)slot);

        public MeshData Get(int slot)
        {
            if (!_meshes.TryGetValue(slot, out var m)) _meshes[slot] = m = new MeshData();
            return m;
        }

        public IEnumerable<KeyValuePair<int, MeshData>> Slots => _meshes;
        public int VertexCount
        {
            get
            {
                var n = 0;
                foreach (var kv in _meshes) n += kv.Value.Vertices.Count;
                return n;
            }
        }
    }
}
