using System;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Couleurs des bâtiments (idée n° 23 ; données : Assets/Resources/Data/buildings.json) : murs ocre, beiges,
    /// blancs ou pastel et toits de tôle (grise, rouillée) tirés par bâtiment, toujours les mêmes pour un bâtiment
    /// donné. Valeurs par défaut de Claude d'après les quartiers de Yaoundé ; un atlas de façades CC0 viendra ensuite.
    /// </summary>
    [Serializable]
    public class BuildingLookConfig
    {
        public bool enabled = true;
        /// <summary>Couleurs de mur (r, g, b en 0..1, à la suite) et leurs poids.</summary>
        public float[] wallColors =
        {
            0.80f, 0.62f, 0.42f, // ocre
            0.88f, 0.80f, 0.66f, // beige
            0.92f, 0.91f, 0.87f, // blanc cassé
            0.72f, 0.48f, 0.36f, // terre cuite
            0.78f, 0.84f, 0.76f, // vert pâle
            0.86f, 0.76f, 0.52f, // jaune sable
        };
        public float[] wallWeights = { 3f, 3f, 2f, 1.5f, 0.75f, 1f };
        /// <summary>Couleurs de toit : tôle neuve, tôle grise, tôle rouillée, dalle de béton.</summary>
        public float[] roofColors =
        {
            0.70f, 0.72f, 0.74f,
            0.52f, 0.54f, 0.56f,
            0.55f, 0.33f, 0.22f,
            0.62f, 0.62f, 0.60f,
        };
        public float[] roofWeights = { 2f, 3f, 3f, 0f };
        /// <summary>À partir de ce nombre de niveaux, toit-terrasse en béton (dernière couleur de toit).</summary>
        public int concreteRoofFromLevels = 3;

        public int WallCount => wallColors == null ? 0 : wallColors.Length / 3;
        public int RoofCount => roofColors == null ? 0 : roofColors.Length / 3;
    }

    public static class BuildingLook
    {
        /// <summary>Indice de la couleur de mur du bâtiment (stable : tiré de son id OSM).</summary>
        public static int Wall(BuildingLookConfig c, long buildingId) => Pick(c.wallWeights, c.WallCount, Hash(buildingId, 17));

        /// <summary>Indice de la couleur de toit : tôle tirée au hasard, ou béton pour les immeubles.</summary>
        public static int Roof(BuildingLookConfig c, long buildingId, int levels)
        {
            if (c.RoofCount == 0) return 0;
            if (c.concreteRoofFromLevels > 0 && levels >= c.concreteRoofFromLevels) return c.RoofCount - 1;
            return Pick(c.roofWeights, c.RoofCount, Hash(buildingId, 31));
        }

        /// <summary>Couleur (r, g, b) d'indice <paramref name="index"/> dans une liste à plat.</summary>
        public static float[] Color(float[] flat, int index)
        {
            if (flat == null || flat.Length < 3) return new[] { 0.7f, 0.7f, 0.7f };
            var i = Math.Max(0, Math.Min(flat.Length / 3 - 1, index)) * 3;
            return new[] { flat[i], flat[i + 1], flat[i + 2] };
        }

        private static int Pick(float[] weights, int count, double roll)
        {
            if (count <= 1) return 0;
            double total = 0;
            for (var i = 0; i < count; i++) total += weights != null && i < weights.Length ? Math.Max(0f, weights[i]) : 1f;
            if (total <= 0) return 0;
            var x = roll * total;
            for (var i = 0; i < count; i++)
            {
                x -= weights != null && i < weights.Length ? Math.Max(0f, weights[i]) : 1f;
                if (x < 0) return i;
            }
            return count - 1;
        }

        /// <summary>Nombre pseudo-aléatoire stable dans [0, 1) tiré d'un id (mélange de bits, sans état).</summary>
        private static double Hash(long id, long salt)
        {
            unchecked
            {
                var x = (ulong)id * 0x9E3779B97F4A7C15UL + (ulong)salt;
                x ^= x >> 33; x *= 0xff51afd7ed558ccdUL; x ^= x >> 33; x *= 0xc4ceb9fe1a85ec53UL; x ^= x >> 33;
                return (x >> 11) / (double)(1UL << 53);
            }
        }
    }
}
