using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Position d'un nom sur la frise : bord gauche (pixels depuis le début de la frise) et rangée (0 en haut).</summary>
    public struct StripLabel
    {
        public float Left;
        public int Row;
    }

    /// <summary>
    /// Frise du trajet en haut de l'écran : les lieux du tronçon placés selon leur distance, le taxi qui avance dessus.
    /// Calculs purs ; la présentation (Driving/RouteStripView) dessine.
    /// </summary>
    public static class RouteStrip
    {
        /// <summary>Distance le long de l'itinéraire → fraction de la frise (0 au début, 1 à la fin).</summary>
        public static float Fraction(float along, float routeLength)
        {
            if (routeLength <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, along / routeLength));
        }

        /// <summary>
        /// Place les noms des lieux sans chevauchement : chaque nom est centré sur son lieu (le premier calé à gauche,
        /// le dernier à droite, tous gardés dans la frise), sur la première rangée s'il y tient, sinon sur la rangée qui
        /// le gêne le moins. <paramref name="centers"/> croissants, en pixels depuis le début de la frise.
        /// </summary>
        public static StripLabel[] LayoutLabels(float[] centers, float[] widths, float stripWidth, int rows = 2, float gap = 6f)
        {
            if (centers == null) throw new ArgumentNullException(nameof(centers));
            if (widths == null || widths.Length != centers.Length) throw new ArgumentException("Une largeur par nom.", nameof(widths));
            var n = centers.Length;
            var result = new StripLabel[n];
            var rowEnd = new float[Math.Max(1, rows)];
            for (var r = 0; r < rowEnd.Length; r++) rowEnd[r] = float.NegativeInfinity;

            for (var i = 0; i < n; i++)
            {
                var w = widths[i];
                var left = i == 0 ? centers[i] : i == n - 1 ? centers[i] - w : centers[i] - w / 2f;
                left = Math.Max(0f, Math.Min(stripWidth - w, left));
                var best = 0;
                var bestOverlap = float.MaxValue;
                for (var r = 0; r < rowEnd.Length; r++)
                {
                    var overlap = Math.Max(0f, rowEnd[r] + gap - left);
                    if (overlap < bestOverlap - 1e-3f) { best = r; bestOverlap = overlap; }
                    if (overlap <= 0f) break;
                }
                result[i] = new StripLabel { Left = left, Row = best };
                rowEnd[best] = Math.Max(rowEnd[best], left + w);
            }
            return result;
        }
    }
}
