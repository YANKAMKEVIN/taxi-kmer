using System;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Mini-carte GPS ronde (idée n° 6 ; données : Assets/Resources/Data/minimap.json). Valeurs par défaut de Claude.</summary>
    [Serializable]
    public class MiniMapConfig
    {
        public bool enabled = true;
        /// <summary>Rayon de terrain montré par la carte (m).</summary>
        public float radiusMeters = 220f;
        /// <summary>Résolution souhaitée de l'image des routes (m par pixel), relevée si le monde dépasse maxTexturePixels.</summary>
        public float metersPerPixel = 2f;
        public int maxTexturePixels = 2048;
        /// <summary>Placement en haut à droite, sous le klaxon (fractions de la hauteur d'écran, comme ControlLayout).</summary>
        public float side = 0.04f;
        public float edge = 0.32f;
        public float size = 0.24f;
        /// <summary>La carte tourne avec le taxi (le haut = devant) ; faux = nord en haut.</summary>
        public bool rotateWithTaxi = true;
    }

    /// <summary>Ce que contient un pixel de l'image des routes.</summary>
    public static class MiniMapCell
    {
        public const byte Empty = 0;
        public const byte Street = 1;
        public const byte MainRoad = 2;
        public const byte Route = 3;
    }

    /// <summary>Image des routes de la mini-carte, calculée une fois au lancement (testable sans moteur).</summary>
    public sealed class MiniMapRaster
    {
        public int Width { get; }
        public int Height { get; }
        public float OriginX { get; }
        public float OriginZ { get; }
        public float MetersPerPixel { get; }
        /// <summary>Pixels ligne par ligne, depuis (OriginX, OriginZ) ; valeurs de <see cref="MiniMapCell"/>.</summary>
        public byte[] Cells { get; }

        private MiniMapRaster(int w, int h, float ox, float oz, float mpp)
        {
            Width = w; Height = h; OriginX = ox; OriginZ = oz; MetersPerPixel = mpp;
            Cells = new byte[w * h];
        }

        public byte At(int px, int py) => px < 0 || py < 0 || px >= Width || py >= Height ? MiniMapCell.Empty : Cells[py * Width + px];

        /// <summary>Position du monde → pixel (non borné).</summary>
        public void ToPixel(float x, float z, out float px, out float py)
        {
            px = (x - OriginX) / MetersPerPixel;
            py = (z - OriginZ) / MetersPerPixel;
        }

        public static MiniMapRaster Build(WorldData world, MiniMapConfig config)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            void Grow(float[] xz)
            {
                if (xz == null) return;
                for (var i = 0; i + 1 < xz.Length; i += 2)
                {
                    minX = Math.Min(minX, xz[i]); maxX = Math.Max(maxX, xz[i]);
                    minZ = Math.Min(minZ, xz[i + 1]); maxZ = Math.Max(maxZ, xz[i + 1]);
                }
            }
            foreach (var r in world.roads ?? new RoadData[0]) Grow(r.xz);
            Grow(world.route?.xz);
            if (minX > maxX) { minX = minZ = 0f; maxX = maxZ = 1f; }
            var margin = config.radiusMeters;
            minX -= margin; minZ -= margin; maxX += margin; maxZ += margin;
            var mpp = Math.Max(config.metersPerPixel, Math.Max(maxX - minX, maxZ - minZ) / Math.Max(16, config.maxTexturePixels));
            var w = Math.Max(1, (int)Math.Ceiling((maxX - minX) / mpp));
            var h = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / mpp));
            var raster = new MiniMapRaster(w, h, minX, minZ, mpp);
            foreach (var r in world.roads ?? new RoadData[0])
            {
                var main = r.highway == "trunk" || r.highway == "primary" || r.highway == "secondary"
                    || r.highway == "trunk_link" || r.highway == "primary_link" || r.highway == "secondary_link";
                raster.Draw(r.xz, Math.Max(3f, r.width), main ? MiniMapCell.MainRoad : MiniMapCell.Street);
            }
            raster.Draw(world.route?.xz, 8f, MiniMapCell.Route);
            return raster;
        }

        /// <summary>Trace une polyligne de largeur <paramref name="widthMeters"/> (au moins un pixel).</summary>
        private void Draw(float[] xz, float widthMeters, byte value)
        {
            if (xz == null || xz.Length < 4) return;
            var half = Math.Max(0.5f, widthMeters / 2f / MetersPerPixel);
            for (var i = 0; i + 3 < xz.Length; i += 2)
            {
                ToPixel(xz[i], xz[i + 1], out var ax, out var ay);
                ToPixel(xz[i + 2], xz[i + 3], out var bx, out var by);
                var len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                var steps = Math.Max(1, (int)Math.Ceiling(len));
                for (var s = 0; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    Stamp(ax + (bx - ax) * t, ay + (by - ay) * t, half, value);
                }
            }
        }

        private void Stamp(float cx, float cy, float half, byte value)
        {
            var r = (int)Math.Ceiling(half);
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > half * half + 0.25f) continue;
                int px = (int)Math.Floor(cx) + dx, py = (int)Math.Floor(cy) + dy;
                if (px < 0 || py < 0 || px >= Width || py >= Height) continue;
                var i = py * Width + px;
                if (Cells[i] < value) Cells[i] = value; // l'itinéraire passe devant les grands axes, eux devant les rues
            }
        }
    }

    /// <summary>Placement des repères sur la mini-carte ronde.</summary>
    public static class MiniMapPlacement
    {
        /// <summary>
        /// Position d'un point du monde sur la carte, en fractions du rayon (-1..1 : u vers la droite, v vers le haut).
        /// Le haut de la carte est la direction du taxi (<paramref name="dirX"/>, <paramref name="dirZ"/>) ou le nord.
        /// Renvoie faux si le point est hors du cercle ; il est alors ramené sur le bord (repère « par là »).
        /// </summary>
        public static bool Place(float taxiX, float taxiZ, float dirX, float dirZ, bool rotate, float radius,
            float x, float z, out float u, out float v)
        {
            float dx = x - taxiX, dz = z - taxiZ;
            if (!rotate || dirX * dirX + dirZ * dirZ < 1e-8f) { dirX = 0f; dirZ = 1f; }
            var n = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            dirX /= n; dirZ /= n;
            // Droite du taxi = (dirZ, -dirX) dans le plan x / z (z vers le nord).
            u = (dx * dirZ - dz * dirX) / radius;
            v = (dx * dirX + dz * dirZ) / radius;
            var d = (float)Math.Sqrt(u * u + v * v);
            if (d <= 1f) return true;
            u /= d; v /= d;
            return false;
        }

        /// <summary>Angle (degrés) dont il faut tourner l'image des routes pour que le devant du taxi soit en haut.</summary>
        public static float RotationDegrees(float dirX, float dirZ, bool rotate) =>
            rotate ? (float)(Math.Atan2(dirX, dirZ) * 180.0 / Math.PI) : 0f;
    }
}
