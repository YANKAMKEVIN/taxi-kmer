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
        {
            return Draw(size, (x, y) =>
            {
                var r = Math.Min(radius, size / 2f);
                var dx = Math.Max(Math.Max(r - x, x - (size - r)), 0f);
                var dy = Math.Max(Math.Max(r - y, y - (size - r)), 0f);
                return r - (float)Math.Sqrt(dx * dx + dy * dy);
            });
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
