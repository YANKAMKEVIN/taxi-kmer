using System;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Silhouettes humaines simples et lisibles de loin (piétons, clients, mototaxis) : buste qui s'affine à la taille,
    /// tête et cou, bras le long du corps, et jambes à part (elles se balancent quand la personne marche). Deux tenues :
    /// pantalon, ou pagne/robe jusqu'aux mollets. Trois corpulences (mince, normale, costaud, comme les clients du jeu).
    /// Origine aux pieds, face = +z. Logique pure.
    /// </summary>
    public static class PersonBuilder
    {
        public const int Skin = 0, Top = 1, Bottom = 2, Dark = 3;
        /// <summary>Hauteur de la hanche : pivot des jambes.</summary>
        public const float HipHeight = 0.9f;

        /// <param name="corpulence">0 mince, 1 normale, 2 costaud.</param>
        public static MeshSet Body(int corpulence, bool dress)
        {
            var set = new MeshSet();
            var w = corpulence == 0 ? 0.36f : corpulence == 2 ? 0.56f : 0.44f; // largeur des épaules
            var d = w * 0.55f;
            var top = set.Get(Top);
            // Buste : tronc de pyramide (épaules larges, taille plus fine), de la taille aux épaules.
            Frustum(top, 0f, HipHeight + 0.05f, 0f, w * 0.82f, d * 0.95f, HipHeight + 0.6f, w, d);
            // Épaules arrondies (dalle du haut) et cou.
            WorldGeometry.AddBox(set.Get(Skin), 0f, HipHeight + 0.67f, 0f, 0.11f, 0.1f, 0.11f, 0f, 1f);
            // Tête : boîte arrondie (octogone en plan), cheveux courts sombres sur le dessus.
            Head(set, 0f, HipHeight + 0.72f, 0f, 0.2f, 0.24f);
            // Bras le long du corps : manche courte (haut) puis avant-bras (peau).
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (w / 2f + 0.05f);
                WorldGeometry.AddBox(top, x, HipHeight + 0.5f, 0f, 0.1f, 0.22f, 0.11f, 0f, 1f);
                WorldGeometry.AddBox(set.Get(Skin), x, HipHeight + 0.18f, 0.01f, 0.08f, 0.44f, 0.09f, 0f, 1f);
            }
            if (dress)
                // Pagne ou robe : de la taille jusqu'aux mollets, qui s'évase un peu.
                Frustum(set.Get(Bottom), 0f, 0.35f, 0f, w * 0.95f, d * 1.25f, HipHeight + 0.08f, w * 0.84f, d * 1f);
            else
                WorldGeometry.AddBox(set.Get(Bottom), 0f, HipHeight - 0.02f, 0f, w * 0.84f, 0.16f, d * 0.95f, 0f, 1f); // bassin
            return set;
        }

        /// <summary>Une jambe (pivot à la hanche, vers le bas) : pantalon ou peau, chaussure sombre.</summary>
        public static MeshSet Leg(int corpulence, bool dress)
        {
            var set = new MeshSet();
            var t = corpulence == 2 ? 0.17f : corpulence == 0 ? 0.12f : 0.14f;
            // Sous un pagne, on ne voit que le bas de la jambe (peau) ; sinon tout le pantalon.
            WorldGeometry.AddBox(set.Get(dress ? Skin : Bottom), 0f, -HipHeight / 2f + 0.04f, 0f, t, HipHeight - 0.08f, t, 0f, 1f);
            WorldGeometry.AddBox(set.Get(Dark), 0f, -HipHeight + 0.04f, 0.04f, t + 0.02f, 0.08f, 0.26f, 0f, 1f);
            return set;
        }

        /// <summary>Angle (degrés) de balancement d'une jambe en marche : ± amplitude, opposé pour l'autre jambe.</summary>
        public static float Swing(float phase, float speed, bool left)
        {
            var amplitude = Math.Min(28f, speed * 22f);
            var a = (float)Math.Sin(phase) * amplitude;
            return left ? a : -a;
        }

        private static void Head(MeshSet set, float cx, float y0, float cz, float width, float height)
        {
            const int sides = 8;
            var skin = set.Get(Skin);
            var r = width / 2f;
            var start = skin.Vertices.Count;
            for (var i = 0; i <= sides; i++)
            {
                var a = i * 2f * (float)Math.PI / sides;
                float x = cx + (float)Math.Cos(a) * r, z = cz + (float)Math.Sin(a) * r * 1.1f;
                skin.AddVertex(new Vec3(x, y0, z), i / (float)sides, 0f);
                skin.AddVertex(new Vec3(x, y0 + height * 0.8f, z), i / (float)sides, 1f);
            }
            for (var i = 0; i < sides; i++)
            {
                var a0 = start + i * 2;
                var mid = (i + 0.5f) * 2f * (float)Math.PI / sides;
                var outward = new Vec3((float)Math.Cos(mid), 0, (float)Math.Sin(mid));
                skin.AddTriangle(a0, a0 + 2, a0 + 3, outward);
                skin.AddTriangle(a0, a0 + 3, a0 + 1, outward);
            }
            // Dessus de la tête : cheveux (dôme bas à huit pans).
            var hair = set.Get(Dark);
            var top = hair.AddVertex(new Vec3(cx, y0 + height, cz), 0.5f, 0.5f);
            for (var i = 0; i < sides; i++)
            {
                float a0 = i * 2f * (float)Math.PI / sides, a1 = (i + 1) * 2f * (float)Math.PI / sides;
                var p0 = hair.AddVertex(new Vec3(cx + (float)Math.Cos(a0) * r * 1.04f, y0 + height * 0.78f, cz + (float)Math.Sin(a0) * r * 1.14f), 0f, 0f);
                var p1 = hair.AddVertex(new Vec3(cx + (float)Math.Cos(a1) * r * 1.04f, y0 + height * 0.78f, cz + (float)Math.Sin(a1) * r * 1.14f), 1f, 0f);
                hair.AddTriangle(top, p0, p1, new Vec3(0, 1, 0));
            }
        }

        /// <summary>Tronc de pyramide à base rectangulaire (bas : largeur w0, profondeur d0 ; haut : w1, d1), fermé en haut.</summary>
        private static void Frustum(MeshData m, float cx, float y0, float cz, float w0, float d0, float y1, float w1, float d1)
        {
            var b = new[] { new Vec3(cx - w0 / 2, y0, cz - d0 / 2), new Vec3(cx + w0 / 2, y0, cz - d0 / 2), new Vec3(cx + w0 / 2, y0, cz + d0 / 2), new Vec3(cx - w0 / 2, y0, cz + d0 / 2) };
            var t = new[] { new Vec3(cx - w1 / 2, y1, cz - d1 / 2), new Vec3(cx + w1 / 2, y1, cz - d1 / 2), new Vec3(cx + w1 / 2, y1, cz + d1 / 2), new Vec3(cx - w1 / 2, y1, cz + d1 / 2) };
            for (var i = 0; i < 4; i++)
            {
                var j = (i + 1) % 4;
                var outward = new Vec3((b[i].X + b[j].X) / 2f - cx, 0, (b[i].Z + b[j].Z) / 2f - cz);
                var a0 = m.AddVertex(b[i], 0f, 0f);
                var a1 = m.AddVertex(b[j], 1f, 0f);
                var a2 = m.AddVertex(t[j], 1f, 1f);
                var a3 = m.AddVertex(t[i], 0f, 1f);
                m.AddTriangle(a0, a1, a2, outward);
                m.AddTriangle(a0, a2, a3, outward);
            }
            var c0 = m.AddVertex(t[0]);
            var c1 = m.AddVertex(t[1]);
            var c2 = m.AddVertex(t[2]);
            var c3 = m.AddVertex(t[3]);
            m.AddTriangle(c0, c1, c2, new Vec3(0, 1, 0));
            m.AddTriangle(c0, c2, c3, new Vec3(0, 1, 0));
        }
    }
}
