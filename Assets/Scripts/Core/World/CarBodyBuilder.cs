using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Carrosserie de berline (la Corolla des taxis de Yaoundé, photos de référence de Kevin) en polygones simples : bas de
    /// caisse extrudé d'après un profil latéral (capot, coffre, pare-chocs), habitacle vitré qui se resserre vers le toit,
    /// pare-chocs et bas de caisse sombres, phares et feux. Origine au sol entre les roues, avant = +z. Logique pure.
    /// </summary>
    public static class CarBodyBuilder
    {
        public const int Paint = 0, Glass = 1, Trim = 2, Headlight = 3, Taillight = 4, Tyre = 5;

        /// <summary>Profil du bas de caisse (z, y), de l'avant bas vers l'arrière bas, pour 4,4 m de long.</summary>
        private static readonly float[] LowerProfile =
        {
            2.2f, 0.42f, 2.24f, 0.62f, 2.14f, 0.8f, 1.0f, 0.97f, -1.55f, 1.0f, -2.08f, 0.98f, -2.22f, 0.66f, -2.2f, 0.42f,
        };

        /// <summary>Profil de l'habitacle (z, y) : pied de pare-brise, haut du pare-brise, haut de la lunette, pied de lunette.</summary>
        private static readonly float[] CabinProfile = { 0.98f, 0.97f, 0.2f, 1.42f, -0.88f, 1.42f, -1.52f, 1.0f };

        public static MeshSet Sedan(float length = 4.4f, float width = 1.72f, bool wheels = false, float wheelRadius = 0.31f,
            float wheelBase = 2.6f, float track = 1.48f)
        {
            var set = new MeshSet();
            var k = length / 4.4f;
            Extrude(set.Get(Paint), LowerProfile, k, width / 2f, width / 2f);
            Cabin(set, k, width / 2f - 0.06f, width / 2f - 0.24f);
            // Bas de caisse et pare-chocs sombres.
            var trim = set.Get(Trim);
            WorldGeometry.AddBox(trim, 0f, 0.36f, 0f, width - 0.04f, 0.14f, length * 0.86f, 0f, 1f);
            foreach (var end in new[] { -1f, 1f })
                WorldGeometry.AddBox(trim, 0f, 0.52f, end * (length / 2f + 0.03f), width + 0.02f, 0.2f, 0.12f, 0f, 1f);
            // Rétroviseurs.
            foreach (var side in new[] { -1f, 1f })
                WorldGeometry.AddBox(trim, side * (width / 2f + 0.08f), 1.05f, 0.85f * k, 0.16f, 0.12f, 0.08f, 0f, 1f);
            // Phares et feux, juste devant la carrosserie.
            foreach (var side in new[] { -1f, 1f })
            {
                WorldGeometry.AddBox(set.Get(Headlight), side * 0.6f, 0.73f, 2.215f * k, 0.36f, 0.13f, 0.04f, 0f, 1f);
                WorldGeometry.AddBox(set.Get(Taillight), side * 0.62f, 0.82f, -2.205f * k, 0.3f, 0.16f, 0.04f, 0f, 1f);
            }
            if (wheels)
                foreach (var front in new[] { -1f, 1f })
                    foreach (var side in new[] { -1f, 1f })
                        Wheel(set.Get(Tyre), side * track / 2f, wheelRadius, front * wheelBase / 2f + 0.05f, wheelRadius, 0.2f);
            return set;
        }

        /// <summary>Roue (cylindre couché, axe x) avec ses deux flancs.</summary>
        public static void Wheel(MeshData m, float x, float y, float z, float radius, float width)
        {
            const int sides = 10;
            var start = m.Vertices.Count;
            for (var i = 0; i <= sides; i++)
            {
                var a = i * 2f * (float)Math.PI / sides;
                float cy = (float)Math.Cos(a) * radius, cz = (float)Math.Sin(a) * radius;
                m.AddVertex(new Vec3(x - width / 2f, y + cy, z + cz), i / (float)sides, 0f);
                m.AddVertex(new Vec3(x + width / 2f, y + cy, z + cz), i / (float)sides, 1f);
            }
            for (var i = 0; i < sides; i++)
            {
                var a0 = start + i * 2;
                var mid = (i + 0.5f) * 2f * (float)Math.PI / sides;
                var outward = new Vec3(0, (float)Math.Cos(mid), (float)Math.Sin(mid));
                m.AddTriangle(a0, a0 + 2, a0 + 3, outward);
                m.AddTriangle(a0, a0 + 3, a0 + 1, outward);
            }
            foreach (var side in new[] { -1f, 1f })
            {
                var c = m.AddVertex(new Vec3(x + side * width / 2f, y, z), 0.5f, 0.5f);
                for (var i = 0; i < sides; i++)
                {
                    var a0 = start + i * 2 + (side > 0 ? 1 : 0);
                    m.AddTriangle(c, a0, a0 + 2, new Vec3(side, 0, 0));
                }
            }
        }

        /// <summary>Profil (z, y) extrudé en largeur : deux flancs (demi-largeur en bas et en haut) et le pourtour.</summary>
        private static void Extrude(MeshData m, float[] profile, float k, float halfBottom, float halfTop)
        {
            var n = profile.Length / 2;
            var yMin = float.MaxValue;
            var yMax = float.MinValue;
            for (var i = 0; i < n; i++) { yMin = Math.Min(yMin, profile[i * 2 + 1]); yMax = Math.Max(yMax, profile[i * 2 + 1]); }
            float Half(float y) => halfBottom + (halfTop - halfBottom) * (y - yMin) / Math.Max(1e-3f, yMax - yMin);

            // Flancs : polygone du profil, triangulé (dans le plan z, y).
            var poly = new float[n * 2];
            for (var i = 0; i < n; i++) { poly[i * 2] = profile[i * 2] * k; poly[i * 2 + 1] = profile[i * 2 + 1]; }
            var tris = WorldGeometry.Triangulate(poly);
            foreach (var side in new[] { -1f, 1f })
            {
                var start = m.Vertices.Count;
                for (var i = 0; i < n; i++)
                {
                    var y = poly[i * 2 + 1];
                    m.AddVertex(new Vec3(side * Half(y), y, poly[i * 2]), poly[i * 2] / 2f, y / 2f);
                }
                foreach (var t in tris) m.AddTriangle(start + t.A, start + t.B, start + t.C, new Vec3(side, 0, 0));
            }
            // Pourtour : un quadrilatère par arête du profil, tourné vers l'extérieur du profil.
            var cz = 0f;
            var cy = 0f;
            for (var i = 0; i < n; i++) { cz += poly[i * 2]; cy += poly[i * 2 + 1]; }
            cz /= n; cy /= n;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                float z0 = poly[i * 2], y0 = poly[i * 2 + 1], z1 = poly[j * 2], y1 = poly[j * 2 + 1];
                var a = m.AddVertex(new Vec3(-Half(y0), y0, z0), 0f, z0 / 2f);
                var b = m.AddVertex(new Vec3(Half(y0), y0, z0), 1f, z0 / 2f);
                var c = m.AddVertex(new Vec3(Half(y1), y1, z1), 1f, z1 / 2f);
                var d = m.AddVertex(new Vec3(-Half(y1), y1, z1), 0f, z1 / 2f);
                var outward = new Vec3(0, (y0 + y1) / 2f - cy, (z0 + z1) / 2f - cz);
                m.AddTriangle(a, b, c, outward);
                m.AddTriangle(a, c, d, outward);
            }
        }

        /// <summary>Habitacle : vitres sur les côtés, pare-brise et lunette, toit peint.</summary>
        private static void Cabin(MeshSet set, float k, float halfBottom, float halfTop)
        {
            var glass = set.Get(Glass);
            var paint = set.Get(Paint);
            Extrude(glass, CabinProfile, k, halfBottom, halfTop);
            // Toit peint : dalle mince par-dessus la partie haute (entre le haut du pare-brise et le haut de la lunette).
            var roofFront = CabinProfile[2] * k;
            var roofBack = CabinProfile[4] * k;
            WorldGeometry.AddBox(paint, 0f, CabinProfile[3] + 0.015f, (roofFront + roofBack) / 2f, halfTop * 2f + 0.04f, 0.05f,
                roofFront - roofBack + 0.08f, 0f, 1f);
            // Montants entre les vitres latérales (pied milieu).
            foreach (var side in new[] { -1f, 1f })
                WorldGeometry.AddBox(paint, side * ((halfBottom + halfTop) / 2f + 0.005f), 1.2f, -0.3f * k, 0.03f, 0.44f, 0.1f, 0f, 1f);
        }
    }
}
