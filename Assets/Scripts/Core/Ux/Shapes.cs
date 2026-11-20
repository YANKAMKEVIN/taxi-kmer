using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Formes de l'interface dessinées par le code (pas d'image importée) : masques d'opacité carrés, ligne par ligne
    /// depuis le bas (convention des textures Unity), 0 = transparent, 255 = opaque, bords lissés sur un pixel.
    /// La présentation en fait des sprites (Driving/UiKit).
    /// </summary>
    public static class Shapes
    {
        /// <summary>Rectangle aux coins arrondis de rayon <paramref name="radius"/> pixels (pour un sprite découpé en 9).</summary>
        public static byte[] RoundedRect(int size, float radius)
            => Draw(size, (x, y) => RoundedBox(x, y, 0f, size, radius));

        /// <summary>Contour d'un rectangle arrondi, d'épaisseur <paramref name="thickness"/> pixels.</summary>
        public static byte[] RoundedFrame(int size, float radius, float thickness)
        {
            return Draw(size, (x, y) =>
                Math.Min(RoundedBox(x, y, 0f, size, radius), -RoundedBox(x, y, thickness, size - thickness, Math.Max(0f, radius - thickness))));
        }

        /// <summary>Distance signée (positive dedans) au carré arrondi [lo, hi]² de rayon r.</summary>
        private static float RoundedBox(float x, float y, float lo, float hi, float r)
        {
            var c = (lo + hi) / 2f;
            var half = (hi - lo) / 2f;
            r = Math.Min(r, half);
            var qx = Math.Abs(x - c) - (half - r);
            var qy = Math.Abs(y - c) - (half - r);
            var ox = Math.Max(qx, 0f);
            var oy = Math.Max(qy, 0f);
            var outside = (float)Math.Sqrt(ox * ox + oy * oy);
            var inside = Math.Min(Math.Max(qx, qy), 0f);
            return r - (outside + inside);
        }

        /// <summary>Disque plein qui touche les bords.</summary>
        public static byte[] Disc(int size) => Draw(size, (x, y) => size / 2f - Dist(x, y, size / 2f, size / 2f));

        /// <summary>Anneau d'épaisseur <paramref name="thickness"/> pixels qui touche les bords.</summary>
        public static byte[] Ring(int size, float thickness)
        {
            return Draw(size, (x, y) =>
            {
                var d = Dist(x, y, size / 2f, size / 2f);
                return Math.Min(size / 2f - d, d - (size / 2f - thickness));
            });
        }

        /// <summary>
        /// Remplit un masque à partir d'une distance signée au bord (positive dedans, en pixels), évaluée au centre
        /// de chaque pixel : l'opacité passe de 0 à 255 sur un pixel autour du bord.
        /// </summary>
        public static byte[] Draw(int size, Func<float, float, float> inside)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var a = new byte[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = inside(x + 0.5f, y + 0.5f);
                a[y * size + x] = (byte)Math.Round(Math.Max(0f, Math.Min(1f, d + 0.5f)) * 255f);
            }
            return a;
        }

        internal static float Dist(float x, float y, float cx, float cy)
        {
            var dx = x - cx;
            var dy = y - cy;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
