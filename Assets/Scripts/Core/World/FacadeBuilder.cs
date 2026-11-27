using System;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Construit un bâtiment habillé (direction « réaliste stylisé poussé », planche d'ambiance v1) : murs texturés,
    /// toit en tôle à deux pans ou plat, ou dalle béton avec acrotère ; près de l'itinéraire, fenêtres à chaque niveau,
    /// porte, et pour les boutiques des rideaux métalliques ouverts avec une enseigne côté route. Logique pure : sommets,
    /// coordonnées de texture et triangles par emplacement de matériau (<see cref="CitySlot"/>).
    /// </summary>
    public static class FacadeBuilder
    {
        /// <summary>Taille (m) d'une répétition de texture sur les murs.</summary>
        public const float WallTile = 3f;
        private const float DetailOffset = 0.04f; // fenêtres et enseignes juste devant le mur (pas de scintillement)

        /// <param name="facesRoute">Pour un point (x, z) et une normale extérieure (nx, nz) : le mur regarde-t-il la route ?</param>
        public static void Build(MeshSet set, float[] xz, BuildingStyle s, CityStyleConfig c, Func<float, float, float, float, bool> facesRoute)
        {
            var n = xz.Length / 2;
            if (n < 3) return;
            var ccw = WorldGeometry.SignedArea(xz) > 0;
            var height = CityStyle.WallHeight(c, s);
            var wallTop = s.Roof == RoofKind.Concrete ? height + c.parapetHeight : height;
            var walls = set.Get((int)CitySlot.Wall0 + Math.Max(0, Math.Min(7, s.Wall)));
            var perimeter = 0f;
            var shopDone = false;

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                float ax = xz[i * 2], az = xz[i * 2 + 1], bx = xz[j * 2], bz = xz[j * 2 + 1];
                float ex = bx - ax, ez = bz - az;
                var len = (float)Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-3f) continue;
                // Normale extérieure unitaire.
                float nx = ccw ? ez / len : -ez / len, nz = ccw ? -ex / len : ex / len;
                var u0 = perimeter / WallTile;
                var u1 = (perimeter + len) / WallTile;
                perimeter += len;
                WallQuad(walls, ax, az, bx, bz, 0f, wallTop, u0, u1, nx, nz, 0f);

                if (!s.Detailed || len < 2.2f) continue;
                var shopSide = s.Shop && !shopDone && len >= 3f && facesRoute((ax + bx) / 2f, (az + bz) / 2f, nx, nz);
                if (shopSide) shopDone = true; // une devanture par boutique, sur le mur côté route
                for (var level = 0; level < s.Levels; level++)
                {
                    var y = level * c.levelHeight;
                    if (level == 0 && shopSide) Shopfront(set, ax, az, ex / len, ez / len, len, nx, nz, s, c);
                    else Openings(set, ax, az, ex / len, ez / len, len, nx, nz, y, level == 0 && i == 0, s, c);
                }
            }

            switch (s.Roof)
            {
                case RoofKind.TinGable when n == 4:
                    GableRoof(set, xz, height, s, c, walls);
                    break;
                case RoofKind.TinFlat:
                case RoofKind.TinGable:
                    FlatRoof(set[s.Rust ? CitySlot.RoofRust : CitySlot.RoofTin], xz, height + 0.15f);
                    break;
                default:
                    FlatRoof(set[CitySlot.RoofConcrete], xz, height);
                    break;
            }
        }

        /// <summary>Paroi verticale de a à b entre y0 et y1, poussée de <paramref name="offset"/> m le long de la normale.</summary>
        private static void WallQuad(MeshData m, float ax, float az, float bx, float bz, float y0, float y1, float u0, float u1,
            float nx, float nz, float offset, float v0 = float.NaN, float v1 = float.NaN)
        {
            ax += nx * offset; az += nz * offset; bx += nx * offset; bz += nz * offset;
            if (float.IsNaN(v0)) { v0 = y0 / WallTile; v1 = y1 / WallTile; }
            var a0 = m.AddVertex(new Vec3(ax, y0, az), u0, v0);
            var b0 = m.AddVertex(new Vec3(bx, y0, bz), u1, v0);
            var a1 = m.AddVertex(new Vec3(ax, y1, az), u0, v1);
            var b1 = m.AddVertex(new Vec3(bx, y1, bz), u1, v1);
            var facing = new Vec3(nx, 0, nz);
            m.AddTriangle(a0, b0, b1, facing);
            m.AddTriangle(a0, b1, a1, facing);
        }

        /// <summary>Rectangle d'une case d'atlas (colonnes × rangées, rangée 0 en haut de l'image) posé sur le mur.</summary>
        private static void AtlasQuad(MeshData m, float ax, float az, float dx, float dz, float from, float width, float y0, float y1,
            float nx, float nz, int cell, int cols, int rows)
        {
            var col = cell % cols;
            var row = cell / cols;
            float u0 = col / (float)cols, u1 = (col + 1) / (float)cols;
            float v0 = 1f - (row + 1) / (float)rows, v1 = 1f - row / (float)rows;
            WallQuad(m, ax + dx * from, az + dz * from, ax + dx * (from + width), az + dz * (from + width), y0, y1, u0, u1, nx, nz,
                DetailOffset, v0, v1);
        }

        /// <summary>Fenêtres régulières d'un niveau (et la porte d'entrée au rez-de-chaussée du premier mur).</summary>
        private static void Openings(MeshSet set, float ax, float az, float dx, float dz, float len, float nx, float nz, float y,
            bool door, BuildingStyle s, CityStyleConfig c)
        {
            var m = set[CitySlot.Windows];
            var count = Math.Max(1, (int)Math.Floor((len - 0.6f) / c.windowSpacing));
            var step = len / count;
            for (var k = 0; k < count; k++)
            {
                var center = step * (k + 0.5f);
                if (door && k == count / 2)
                {
                    AtlasQuad(m, ax, az, dx, dz, center - 0.5f, 1f, y, y + 2.15f, nx, nz, s.Door, 4, 4);
                    continue;
                }
                AtlasQuad(m, ax, az, dx, dz, center - 0.6f, 1.2f, y + 0.95f, y + 2.25f, nx, nz, s.Window, 4, 4);
            }
        }

        /// <summary>Devanture : rideaux métalliques ouverts sur la boutique, et l'enseigne au-dessus sur toute la largeur.</summary>
        private static void Shopfront(MeshSet set, float ax, float az, float dx, float dz, float len, float nx, float nz,
            BuildingStyle s, CityStyleConfig c)
        {
            var m = set[CitySlot.Windows];
            var count = Math.Max(1, (int)Math.Floor(len / 3.2f));
            var step = len / count;
            for (var k = 0; k < count; k++)
                AtlasQuad(m, ax, az, dx, dz, step * k + 0.25f, step - 0.5f, 0f, 2.45f, nx, nz, 14 + (k + s.Sign) % 2, 4, 4);
            var signWidth = Math.Min(len - 0.3f, 6.5f);
            AtlasQuad(set[CitySlot.Signs], ax, az, dx, dz, (len - signWidth) / 2f, signWidth, 2.55f, Math.Min(c.levelHeight - 0.05f, 2.55f + signWidth / 4f),
                nx, nz, s.Sign, 2, 8);
        }

        /// <summary>Dalle (béton ou tôle) sur l'emprise, à la hauteur donnée.</summary>
        private static void FlatRoof(MeshData m, float[] xz, float y)
        {
            var start = m.Vertices.Count;
            var n = xz.Length / 2;
            for (var i = 0; i < n; i++) m.AddVertex(new Vec3(xz[i * 2], y, xz[i * 2 + 1]));
            foreach (var t in WorldGeometry.Triangulate(xz)) m.AddTriangle(start + t.A, start + t.B, start + t.C, new Vec3(0, 1, 0));
        }

        /// <summary>
        /// Toit en tôle à deux pans sur une emprise à quatre coins : faîte le long du grand côté, débord tout autour, pignons
        /// triangulaires dans le matériau du mur. Les ondulations de la tôle suivent la pente.
        /// </summary>
        private static void GableRoof(MeshSet set, float[] xz, float height, BuildingStyle s, CityStyleConfig c, MeshData walls)
        {
            // Coins dans l'ordre ; le grand côté (p0 → p1) porte le faîte.
            var p = new Vec3[4];
            for (var i = 0; i < 4; i++) p[i] = new Vec3(xz[i * 2], height, xz[i * 2 + 1]);
            if (Dist(p[0], p[1]) < Dist(p[1], p[2])) p = new[] { p[1], p[2], p[3], p[0] };
            var center = new Vec3((p[0].X + p[1].X + p[2].X + p[3].X) / 4f, height, (p[0].Z + p[1].Z + p[2].Z + p[3].Z) / 4f);
            // Débord : chaque coin écarté du centre.
            var q = new Vec3[4];
            for (var i = 0; i < 4; i++)
            {
                float vx = p[i].X - center.X, vz = p[i].Z - center.Z;
                var l = (float)Math.Sqrt(vx * vx + vz * vz);
                q[i] = l > 1e-3f ? new Vec3(p[i].X + vx / l * c.roofOverhang, height - 0.15f, p[i].Z + vz / l * c.roofOverhang) : p[i];
            }
            // Faîte : milieux des petits côtés (p1-p2 et p3-p0), relevés.
            var r0 = Mid(p[3], p[0], height + c.ridgeHeight);
            var r1 = Mid(p[1], p[2], height + c.ridgeHeight);
            var roof = set[s.Rust ? CitySlot.RoofRust : CitySlot.RoofTin];
            var up = new Vec3(0, 1, 0);
            var span = Dist(q[0], q[1]) / FacadeBuilder.WallTile;
            var slope = Dist(q[0], new Vec3((q[0].X + q[3].X) / 2f, height + c.ridgeHeight, (q[0].Z + q[3].Z) / 2f)) / 1.5f;
            // Pan côté p0-p1 et pan côté p2-p3 (u le long du faîte, v dans la pente : ondulations dans la pente).
            AddPanel(roof, q[0], q[1], r1, r0, span, slope, up);
            AddPanel(roof, q[2], q[3], r0, r1, span, slope, up);
            // Pignons (triangles sous le faîte, dans le matériau du mur).
            Gable(walls, p[3], p[0], r0, center);
            Gable(walls, p[1], p[2], r1, center);
        }

        private static void AddPanel(MeshData m, Vec3 a, Vec3 b, Vec3 c, Vec3 d, float span, float slope, Vec3 up)
        {
            var ia = m.AddVertex(a, 0f, 0f);
            var ib = m.AddVertex(b, span, 0f);
            var ic = m.AddVertex(c, span, slope);
            var id = m.AddVertex(d, 0f, slope);
            m.AddTriangle(ia, ib, ic, up);
            m.AddTriangle(ia, ic, id, up);
        }

        private static void Gable(MeshData m, Vec3 a, Vec3 b, Vec3 top, Vec3 center)
        {
            var outward = new Vec3((a.X + b.X) / 2f - center.X, 0, (a.Z + b.Z) / 2f - center.Z);
            var w = Dist(a, b) / WallTile;
            var ia = m.AddVertex(a, 0f, a.Y / WallTile);
            var ib = m.AddVertex(b, w, b.Y / WallTile);
            var it = m.AddVertex(top, w / 2f, top.Y / WallTile);
            m.AddTriangle(ia, ib, it, outward);
        }

        private static Vec3 Mid(Vec3 a, Vec3 b, float y) => new Vec3((a.X + b.X) / 2f, y, (a.Z + b.Z) / 2f);

        private static float Dist(Vec3 a, Vec3 b) =>
            (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y) + (b.Z - a.Z) * (b.Z - a.Z));
    }
}
