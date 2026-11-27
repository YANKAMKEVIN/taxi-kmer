using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Position d'un nom sur la frise : bord gauche (pixels depuis le début de la frise) et rangée (0 en haut).</summary>
    public struct StripLabel
    {
        public float Left;
        public int Row;
        /// <summary>Le nom ne tient nulle part sans chevaucher un autre : il n'est pas affiché (le repère reste).</summary>
        public bool Hidden;
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
        /// le dernier à droite, tous gardés dans la frise), sur la première rangée s'il y tient, sinon sur la suivante, sinon
        /// caché (jamais deux noms l'un sur l'autre) ; le dernier lieu est toujours nommé. <paramref name="centers"/> croissants, en pixels depuis le début de la frise.
        /// </summary>
        public static StripLabel[] LayoutLabels(float[] centers, float[] widths, float stripWidth, int rows = 2, float gap = 6f)
        {
            if (centers == null) throw new ArgumentNullException(nameof(centers));
            if (widths == null || widths.Length != centers.Length) throw new ArgumentException("Une largeur par nom.", nameof(widths));
            var n = centers.Length;
            var result = new StripLabel[n];
            var rowEnd = new float[Math.Max(1, rows)];
            for (var r = 0; r < rowEnd.Length; r++) rowEnd[r] = float.NegativeInfinity;

            // Le dernier lieu (le terminus) est toujours nommé : sa place est réservée sur la première rangée.
            var lastLeft = n > 0 ? Math.Max(0f, Math.Min(stripWidth - widths[n - 1], centers[n - 1] - widths[n - 1])) : 0f;
            for (var i = 0; i < n; i++)
            {
                var w = widths[i];
                var left = i == 0 ? centers[i] : i == n - 1 ? centers[i] - w : centers[i] - w / 2f;
                left = Math.Max(0f, Math.Min(stripWidth - w, left));
                if (i == n - 1 && n > 1)
                {
                    result[i] = new StripLabel { Left = left, Row = 0 };
                    break;
                }
                var row = -1;
                for (var r = 0; r < rowEnd.Length; r++)
                {
                    var free = rowEnd[r] + gap <= left;
                    if (free && r == 0 && n > 1 && left + w + gap > lastLeft) free = false; // gênerait le terminus
                    if (free) { row = r; break; }
                }
                // Quand aucune rangée n'a la place, le nom est caché plutôt que superposé (vu le 27/11 : « CaPont d'EBorne »).
                result[i] = row < 0 ? new StripLabel { Left = left, Row = 0, Hidden = true } : new StripLabel { Left = left, Row = row };
                if (row >= 0) rowEnd[row] = left + w;
            }
            return result;
        }
    }
}
