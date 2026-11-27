using System;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Maillages des décors de bas-côté (<see cref="RoadsideProps"/>) : poteau en bois avec traverse et fils qui pendent,
    /// manguier (tronc et houppier en boules irrégulières), palmier (stipe courbé, palmes), étal sous parasol avec sa
    /// marchandise. Formes simples, matières vraies (direction « réaliste stylisé poussé ») ; logique pure.
    /// </summary>
    public static class PropBuilder
    {
        // Emplacements de matériau des décors (à la suite de CitySlot ; mêmes noms côté affichage).
        public const int Pole = 17, Wire = 18, Trunk = 19, Foliage = 20, Stall = 21, Umbrella0 = 22, Goods = 25;
        public const int SlotCount = 26;

        public static void Build(MeshSet set, Prop p, RoadsideConfig c)
        {
            switch (p.Kind)
            {
                case PropKind.Pole: PoleWithWires(set, p, c); break;
                case PropKind.Mango: Mango(set, p); break;
                case PropKind.Palm: Palm(set, p); break;
                case PropKind.Stall: StallWithUmbrella(set, p); break;
            }
        }

        private static void PoleWithWires(MeshSet set, Prop p, RoadsideConfig c)
        {
            var h = c.poleHeight * p.Scale;
            // Poteau légèrement penché (comme sur les photos de Kevin), traverse perpendiculaire à la route.
            var lean = (p.Variant - 1) * 0.12f;
            Cylinder(set.Get(Pole), p.X, 0f, p.Z, p.X + p.DirX * lean, h, p.Z + p.DirZ * lean, 0.14f, 0.11f, 6);
            float rx = p.DirZ, rz = -p.DirX; // perpendiculaire à la route
            var tx = p.X + p.DirX * lean;
            var tz = p.Z + p.DirZ * lean;
            WorldGeometry.AddBox(set.Get(Pole), tx, h - 0.45f, tz, 0.1f, 0.1f, 1.9f, rx, rz);
            if (float.IsNaN(p.NextX)) return;
            for (var k = -1; k <= 1; k++)
            {
                float ax = tx + rx * k * 0.8f, az = tz + rz * k * 0.8f;
                float bx = p.NextX + rx * k * 0.8f, bz = p.NextZ + rz * k * 0.8f;
                Catenary(set.Get(Wire), ax, h - 0.38f, az, bx, h - 0.38f, bz, c.wireSag);
            }
        }

        private static void Catenary(MeshData m, float ax, float ay, float az, float bx, float by, float bz, float sag)
        {
            const int steps = 6;
            for (var i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                var y0 = ay + (by - ay) * t0 - sag * 4f * t0 * (1f - t0);
                var y1 = ay + (by - ay) * t1 - sag * 4f * t1 * (1f - t1);
                float x0 = ax + (bx - ax) * t0, z0 = az + (bz - az) * t0, x1 = ax + (bx - ax) * t1, z1 = az + (bz - az) * t1;
                var dx = x1 - x0;
                var dz = z1 - z0;
                var len = (float)Math.Sqrt(dx * dx + dz * dz + (y1 - y0) * (y1 - y0));
                WorldGeometry.AddBox(m, (x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f, 0.05f, 0.05f + Math.Abs(y1 - y0), len, dx, dz);
            }
        }

        private static void Mango(MeshSet set, Prop p)
        {
            var s = p.Scale;
            Cylinder(set.Get(Trunk), p.X, 0f, p.Z, p.X, 3.2f * s, p.Z, 0.32f * s, 0.22f * s, 7);
            var rnd = new Random((int)(p.X * 31 + p.Z * 17));
            // Houppier dense et arrondi : quatre boules irrégulières serrées.
            for (var k = 0; k < 4; k++)
            {
                var a = k * 1.7f + p.Variant;
                var r = (2.1f + (float)rnd.NextDouble() * 0.8f) * s;
                Blob(set.Get(Foliage), p.X + (float)Math.Cos(a) * 1.3f * s, (4.4f + (k == 0 ? 1.2f : 0f)) * s, p.Z + (float)Math.Sin(a) * 1.3f * s, r, rnd);
            }
        }

        private static void Palm(MeshSet set, Prop p)
        {
            var s = p.Scale;
            var h = 7.5f * s;
            // Stipe courbé : trois tronçons qui s'écartent un peu de la verticale.
            float lx = p.DirZ * 0.5f, lz = -p.DirX * 0.5f;
            float x = p.X, z = p.Z, y = 0f;
            for (var i = 1; i <= 3; i++)
            {
                var t = i / 3f;
                var nx = p.X + lx * t * t;
                var nz = p.Z + lz * t * t;
                Cylinder(set.Get(Trunk), x, y, z, nx, h * t, nz, 0.24f * s, 0.2f * s, 6);
                x = nx; z = nz; y = h * t;
            }
            // Palmes : huit, retombantes, à double face.
            for (var k = 0; k < 8; k++)
            {
                var a = k * (float)Math.PI / 4f + p.Variant * 0.3f;
                float dx = (float)Math.Cos(a), dz = (float)Math.Sin(a);
                var len = 3.6f * s;
                Frond(set.Get(Foliage), x, y, z, dx, dz, len);
            }
        }

        private static void Frond(MeshData m, float x, float y, float z, float dx, float dz, float len)
        {
            float rx = -dz * 0.55f, rz = dx * 0.55f;
            var mid = new Vec3(x + dx * len * 0.55f, y + 0.5f, z + dz * len * 0.55f);
            var tip = new Vec3(x + dx * len, y - 1.2f, z + dz * len);
            var root = new Vec3(x, y, z);
            var l = new Vec3(mid.X + rx, mid.Y - 0.25f, mid.Z + rz);
            var r = new Vec3(mid.X - rx, mid.Y - 0.25f, mid.Z - rz);
            // Deux faces (la palme se voit de dessus et de dessous).
            foreach (var up in new[] { 1f, -1f })
            {
                var i0 = m.AddVertex(root, 0.5f, 0f);
                var il = m.AddVertex(l, 0f, 0.55f);
                var ir = m.AddVertex(r, 1f, 0.55f);
                var it = m.AddVertex(tip, 0.5f, 1f);
                var n = new Vec3(0, up, 0);
                m.AddTriangle(i0, il, ir, n);
                m.AddTriangle(il, it, ir, n);
            }
        }

        private static void StallWithUmbrella(MeshSet set, Prop p)
        {
            float rx = p.DirZ, rz = -p.DirX;
            var wood = set.Get(Stall);
            WorldGeometry.AddBox(wood, p.X, 0.85f, p.Z, 0.9f, 0.08f, 1.7f, p.DirX, p.DirZ); // plateau
            foreach (var (a, b) in new[] { (-0.75f, -0.38f), (0.75f, -0.38f), (-0.75f, 0.38f), (0.75f, 0.38f) })
                WorldGeometry.AddBox(wood, p.X + p.DirX * a + rx * b, 0.42f, p.Z + p.DirZ * a + rz * b, 0.06f, 0.84f, 0.06f, p.DirX, p.DirZ);
            // Marchandise : petits tas colorés (texture des enseignes : couleurs vives).
            var goods = set.Get(Goods);
            for (var k = 0; k < 5; k++)
            {
                var a = -0.6f + k * 0.3f;
                WorldGeometry.AddBox(goods, p.X + p.DirX * a, 0.98f, p.Z + p.DirZ * a, 0.5f, 0.18f + (k % 2) * 0.08f, 0.22f, p.DirX, p.DirZ);
            }
            // Parasol : mât et toile en cône à huit pans, de la couleur tirée.
            Cylinder(wood, p.X, 0.85f, p.Z, p.X, 2.45f, p.Z, 0.03f, 0.03f, 5);
            Cone(set.Get(Umbrella0 + Math.Max(0, Math.Min(2, p.Variant))), p.X, 2.1f, p.Z, 2.55f, 1.45f, 8);
        }

        /// <summary>Cylindre (tronc conique) entre deux points, sans fond, texture enroulée (u) et en hauteur (v).</summary>
        public static void Cylinder(MeshData m, float x0, float y0, float z0, float x1, float y1, float z1, float r0, float r1, int sides)
        {
            var height = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
            var start = m.Vertices.Count;
            for (var i = 0; i <= sides; i++)
            {
                var a = i * 2f * (float)Math.PI / sides;
                float cx = (float)Math.Cos(a), cz = (float)Math.Sin(a);
                var u = i / (float)sides * 2f;
                m.AddVertex(new Vec3(x0 + cx * r0, y0, z0 + cz * r0), u, 0f);
                m.AddVertex(new Vec3(x1 + cx * r1, y1, z1 + cz * r1), u, height / 2f);
            }
            for (var i = 0; i < sides; i++)
            {
                var a0 = start + i * 2;
                var a1 = a0 + 1;
                var b0 = a0 + 2;
                var b1 = a0 + 3;
                var mid = (i + 0.5f) * 2f * (float)Math.PI / sides;
                var outward = new Vec3((float)Math.Cos(mid), 0, (float)Math.Sin(mid));
                m.AddTriangle(a0, b0, b1, outward);
                m.AddTriangle(a0, b1, a1, outward);
            }
        }

        /// <summary>Cône de parasol : pointe en haut, bord à <paramref name="yEdge"/>, vu dessus et dessous.</summary>
        private static void Cone(MeshData m, float x, float yEdge, float z, float yTop, float radius, int sides)
        {
            var top = new Vec3(x, yTop, z);
            for (var i = 0; i < sides; i++)
            {
                float a0 = i * 2f * (float)Math.PI / sides, a1 = (i + 1) * 2f * (float)Math.PI / sides;
                var p0 = new Vec3(x + (float)Math.Cos(a0) * radius, yEdge, z + (float)Math.Sin(a0) * radius);
                var p1 = new Vec3(x + (float)Math.Cos(a1) * radius, yEdge, z + (float)Math.Sin(a1) * radius);
                foreach (var up in new[] { 1f, -1f })
                {
                    var it = m.AddVertex(top, 0.5f, 1f);
                    var i0 = m.AddVertex(p0, 0f, 0f);
                    var i1 = m.AddVertex(p1, 1f, 0f);
                    m.AddTriangle(it, i0, i1, new Vec3((p0.X + p1.X) / 2f - x, up, (p0.Z + p1.Z) / 2f - z));
                }
            }
        }

        /// <summary>Boule irrégulière (houppier) : sphère basse définition dont le rayon varie un peu.</summary>
        private static void Blob(MeshData m, float cx, float cy, float cz, float radius, Random rnd)
        {
            const int rings = 5, segs = 8;
            var start = m.Vertices.Count;
            for (var r = 0; r <= rings; r++)
            {
                var phi = r * (float)Math.PI / rings;
                for (var s = 0; s <= segs; s++)
                {
                    var theta = s * 2f * (float)Math.PI / segs;
                    var k = (r == 0 || r == rings || s == segs) ? 1f : 0.82f + (float)rnd.NextDouble() * 0.3f;
                    var rr = radius * k;
                    var y = (float)Math.Cos(phi) * rr * 0.8f; // un peu aplati
                    var ring = (float)Math.Sin(phi) * rr;
                    m.AddVertex(new Vec3(cx + (float)Math.Cos(theta) * ring, cy + y, cz + (float)Math.Sin(theta) * ring),
                        s / (float)segs * 3f, r / (float)rings * 2f);
                }
            }
            for (var r = 0; r < rings; r++)
                for (var s = 0; s < segs; s++)
                {
                    var a = start + r * (segs + 1) + s;
                    var b = a + segs + 1;
                    var va = m.Vertices[a];
                    var outward = new Vec3(va.X - cx, va.Y - cy, va.Z - cz);
                    m.AddTriangle(a, b, b + 1, outward);
                    m.AddTriangle(a, b + 1, a + 1, outward);
                }
        }
    }
}
