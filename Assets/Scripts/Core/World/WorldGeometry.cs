using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>Construction des maillages du monde : bandes de route et bâtiments extrudés (blocs gris).</summary>
    public static class WorldGeometry
    {
        public const float LevelHeight = 3f;
        private static readonly Vec3 Up = new Vec3(0, 1, 0);

        /// <summary>Bande plate de largeur <paramref name="width"/> le long de la polyligne xz, à la hauteur y.</summary>
        public static void AddRoadStrip(MeshData mesh, float[] xz, float width, float y)
        {
            var n = xz.Length / 2;
            if (n < 2) return;
            var half = width / 2f;
            var first = mesh.Vertices.Count;
            for (var i = 0; i < n; i++)
            {
                // Direction moyenne des segments voisins → perpendiculaire au point.
                var prev = Math.Max(0, i - 1);
                var next = Math.Min(n - 1, i + 1);
                var dx = xz[next * 2] - xz[prev * 2];
                var dz = xz[next * 2 + 1] - xz[prev * 2 + 1];
                var len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4f) { dx = 1; dz = 0; len = 1; }
                var px = -dz / len * half;
                var pz = dx / len * half;
                var x = xz[i * 2];
                var z = xz[i * 2 + 1];
                mesh.AddVertex(new Vec3(x + px, y, z + pz));
                mesh.AddVertex(new Vec3(x - px, y, z - pz));
            }
            for (var i = 0; i < n - 1; i++)
            {
                var l0 = first + i * 2;
                var r0 = l0 + 1;
                var l1 = l0 + 2;
                var r1 = l0 + 3;
                mesh.AddTriangle(l0, l1, r0, Up);
                mesh.AddTriangle(r0, l1, r1, Up);
            }
        }

        /// <summary>Bloc : murs verticaux et toit plat, à partir de l'emprise (polygone fermé implicite).</summary>
        public static void AddBuilding(MeshData mesh, float[] xz, float height)
        {
            var n = xz.Length / 2;
            if (n < 3) return;
            var ccw = SignedArea(xz) > 0;

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                float ax = xz[i * 2], az = xz[i * 2 + 1], bx = xz[j * 2], bz = xz[j * 2 + 1];
                var ex = bx - ax;
                var ez = bz - az;
                // Normale extérieure : à droite du sens de parcours si le polygone tourne dans le sens trigonométrique.
                var outward = ccw ? new Vec3(ez, 0, -ex) : new Vec3(-ez, 0, ex);
                var a0 = mesh.AddVertex(new Vec3(ax, 0, az));
                var b0 = mesh.AddVertex(new Vec3(bx, 0, bz));
                var a1 = mesh.AddVertex(new Vec3(ax, height, az));
                var b1 = mesh.AddVertex(new Vec3(bx, height, bz));
                mesh.AddTriangle(a0, b0, b1, outward);
                mesh.AddTriangle(a0, b1, a1, outward);
            }

            var roofStart = mesh.Vertices.Count;
            for (var i = 0; i < n; i++) mesh.AddVertex(new Vec3(xz[i * 2], height, xz[i * 2 + 1]));
            foreach (var t in Triangulate(xz))
                mesh.AddTriangle(roofStart + t.A, roofStart + t.B, roofStart + t.C, Up);
        }

        /// <summary>Aire signée (formule du lacet) dans le plan x, z : positive dans le sens trigonométrique.</summary>
        public static float SignedArea(float[] xz)
        {
            var n = xz.Length / 2;
            double sum = 0;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                sum += xz[i * 2] * xz[j * 2 + 1] - xz[j * 2] * xz[i * 2 + 1];
            }
            return (float)(sum / 2);
        }

        public struct Tri
        {
            public int A, B, C;
            public Tri(int a, int b, int c) { A = a; B = b; C = c; }
        }

        /// <summary>Triangulation par oreilles d'un polygone simple (convexe ou non). Repli en éventail si le polygone est dégénéré.</summary>
        public static List<Tri> Triangulate(float[] xz)
        {
            var n = xz.Length / 2;
            var result = new List<Tri>();
            if (n < 3) return result;
            var idx = new List<int>(n);
            for (var i = 0; i < n; i++) idx.Add(i);
            var sign = SignedArea(xz) >= 0 ? 1f : -1f;

            var guard = n * n;
            while (idx.Count > 3 && guard-- > 0)
            {
                var clipped = false;
                for (var k = 0; k < idx.Count; k++)
                {
                    int a = idx[(k + idx.Count - 1) % idx.Count], b = idx[k], c = idx[(k + 1) % idx.Count];
                    if (Cross(xz, a, b, c) * sign <= 0) continue; // angle rentrant
                    var inside = false;
                    foreach (var p in idx)
                    {
                        if (p == a || p == b || p == c) continue;
                        if (PointInTriangle(xz, p, a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(new Tri(a, b, c));
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }

            if (idx.Count == 3) result.Add(new Tri(idx[0], idx[1], idx[2]));
            else for (var k = 1; k < idx.Count - 1; k++) result.Add(new Tri(idx[0], idx[k], idx[k + 1]));
            return result;
        }

        private static float Cross(float[] xz, int a, int b, int c)
        {
            return (xz[b * 2] - xz[a * 2]) * (xz[c * 2 + 1] - xz[a * 2 + 1])
                 - (xz[b * 2 + 1] - xz[a * 2 + 1]) * (xz[c * 2] - xz[a * 2]);
        }

        private static bool PointInTriangle(float[] xz, int p, int a, int b, int c)
        {
            var d1 = Cross(xz, a, b, p);
            var d2 = Cross(xz, b, c, p);
            var d3 = Cross(xz, c, a, p);
            var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        /// <summary>Longueur cumulée d'une polyligne xz (m).</summary>
        public static float[] CumulativeLengths(float[] xz)
        {
            var n = xz.Length / 2;
            var result = new float[Math.Max(n, 0)];
            for (var i = 1; i < n; i++)
            {
                var dx = xz[i * 2] - xz[i * 2 - 2];
                var dz = xz[i * 2 + 1] - xz[i * 2 - 1];
                result[i] = result[i - 1] + (float)Math.Sqrt(dx * dx + dz * dz);
            }
            return result;
        }

        /// <summary>Distance le long de la polyligne du point de la polyligne le plus proche de (x, z).</summary>
        public static float DistanceAlong(float[] xz, float[] cumulative, float x, float z)
        {
            Project(xz, cumulative, x, z, out var along, out _);
            return along;
        }

        /// <summary>Projette (x, z) sur la polyligne : distance le long de la polyligne et écart latéral (m).</summary>
        public static void Project(float[] xz, float[] cumulative, float x, float z, out float along, out float offset)
        {
            var n = cumulative.Length;
            along = 0f;
            offset = 0f;
            if (n < 2) return;
            var best = float.MaxValue;
            for (var i = 1; i < n; i++)
            {
                float ax = xz[i * 2 - 2], az = xz[i * 2 - 1], bx = xz[i * 2], bz = xz[i * 2 + 1];
                float ex = bx - ax, ez = bz - az;
                var len2 = ex * ex + ez * ez;
                var t = len2 > 1e-8f ? ((x - ax) * ex + (z - az) * ez) / len2 : 0f;
                t = Math.Max(0f, Math.Min(1f, t));
                float px = ax + ex * t - x, pz = az + ez * t - z;
                var d2 = px * px + pz * pz;
                if (d2 < best)
                {
                    best = d2;
                    along = cumulative[i - 1] + (cumulative[i] - cumulative[i - 1]) * t;
                }
            }
            offset = (float)Math.Sqrt(best);
        }

        /// <summary>Point et direction à la distance d le long de la polyligne (bornée à ses extrémités).</summary>
        public static void SampleAlong(float[] xz, float[] cumulative, float d, out float x, out float z, out float dirX, out float dirZ)
        {
            var n = cumulative.Length;
            if (n < 2) { x = n == 1 ? xz[0] : 0; z = n == 1 ? xz[1] : 0; dirX = 0; dirZ = 1; return; }
            d = Math.Max(0, Math.Min(cumulative[n - 1], d));
            var i = Array.BinarySearch(cumulative, d);
            if (i < 0) i = ~i;
            i = Math.Max(1, Math.Min(n - 1, i));
            var seg = cumulative[i] - cumulative[i - 1];
            var t = seg > 1e-4f ? (d - cumulative[i - 1]) / seg : 0f;
            float ax = xz[i * 2 - 2], az = xz[i * 2 - 1], bx = xz[i * 2], bz = xz[i * 2 + 1];
            x = ax + (bx - ax) * t;
            z = az + (bz - az) * t;
            var len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            dirX = len > 1e-4f ? (bx - ax) / len : 0f;
            dirZ = len > 1e-4f ? (bz - az) / len : 1f;
        }
    }
}
