using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Poser les choses sur le relief (idée n° 8, décision de Kevin du 23/11 : trafic, piétons, clients, arrêts, postes de
    /// police, vendeurs… sur les collines de Yaoundé). Pur, testable hors Unity ; les vues Unity n'ont plus qu'à prendre
    /// la hauteur et l'inclinaison rendues ici. Sans relief (grille nulle ou vide), tout reste à y = 0 comme avant.
    /// </summary>
    public static class Ground
    {
        /// <summary>Pas maximal (m) entre deux points d'une route posée sur le relief (le sol est une grille de 30 m).</summary>
        public const float RoadStepMeters = 5f;

        /// <summary>Altitude du sol au point x, z (0 sans relief).</summary>
        public static float Height(TerrainGrid terrain, float x, float z) =>
            terrain != null && terrain.HasData ? terrain.HeightAt(x, z) : 0f;

        /// <summary>
        /// Véhicule de longueur <paramref name="length"/> centré en x, z et tourné vers (dirX, dirZ) : rend la hauteur du
        /// centre (moyenne des hauteurs sous l'avant et l'arrière, pour qu'il ne s'enfonce ni ne flotte dans une pente) et
        /// la montée <paramref name="rise"/> (m, hauteur sous l'avant moins hauteur sous l'arrière) qui donne son tangage.
        /// </summary>
        public static float Vehicle(TerrainGrid terrain, float x, float z, float dirX, float dirZ, float length, out float rise)
        {
            rise = 0f;
            if (terrain == null || !terrain.HasData) return 0f;
            var len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            if (len < 1e-5f || length <= 0f) return terrain.HeightAt(x, z);
            var half = length / 2f;
            float ux = dirX / len * half, uz = dirZ / len * half;
            var front = terrain.HeightAt(x + ux, z + uz);
            var back = terrain.HeightAt(x - ux, z - uz);
            rise = front - back;
            return (front + back) / 2f;
        }

        /// <summary>Tangage (degrés, positif en montée) d'un véhicule de longueur <paramref name="length"/> pour une montée donnée.</summary>
        public static float PitchDegrees(float rise, float length) =>
            length <= 0f ? 0f : (float)(Math.Atan2(rise, length) * 180.0 / Math.PI);

        /// <summary>
        /// Polyligne xz redécoupée pour qu'aucun segment ne dépasse <paramref name="maxStep"/> m (mêmes points d'origine,
        /// points ajoutés en ligne droite) : une route posée point par point sur le relief suit alors le sol au lieu de le
        /// traverser entre deux sommets éloignés. Rend la même polyligne si elle est déjà assez fine.
        /// </summary>
        public static float[] Densify(float[] xz, float maxStep)
        {
            if (xz == null || xz.Length < 4 || maxStep <= 0f) return xz;
            var n = xz.Length / 2;
            var output = new List<float>(xz.Length) { xz[0], xz[1] };
            for (var i = 1; i < n; i++)
            {
                float ax = xz[(i - 1) * 2], az = xz[(i - 1) * 2 + 1], bx = xz[i * 2], bz = xz[i * 2 + 1];
                var d = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                var parts = Math.Max(1, (int)Math.Ceiling(d / maxStep - 1e-4f));
                for (var k = 1; k < parts; k++)
                {
                    var t = (float)k / parts;
                    output.Add(ax + (bx - ax) * t);
                    output.Add(az + (bz - az) * t);
                }
                output.Add(bx);
                output.Add(bz);
            }
            return output.Count == xz.Length ? xz : output.ToArray();
        }
    }
}
