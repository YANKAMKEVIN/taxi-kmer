using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>Point 3D minimal (la logique pure n'utilise pas UnityEngine.Vector3).</summary>
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    /// <summary>Coordonnée de texture (la logique pure n'utilise pas UnityEngine.Vector2).</summary>
    public struct Vec2
    {
        public float U, V;
        public Vec2(float u, float v) { U = u; V = v; }
    }

    /// <summary>
    /// Maillage en cours de construction. Les triangles sont orientés pour que leur face visible
    /// pointe vers la normale demandée (convention Unity : normale = (b - a) × (c - a)).
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<Vec3> Vertices = new List<Vec3>();
        public readonly List<int> Triangles = new List<int>();
        /// <summary>Une coordonnée de texture par sommet (par défaut : projection du dessus, 1 unité = <see cref="PlanarTile"/> m).</summary>
        public readonly List<Vec2> Uvs = new List<Vec2>();

        /// <summary>Taille (m) d'une répétition de texture en projection du dessus (sols, routes, toits).</summary>
        public const float PlanarTile = 4f;

        public int TriangleCount => Triangles.Count / 3;

        public int AddVertex(Vec3 v) => AddVertex(v, v.X / PlanarTile, v.Z / PlanarTile);

        /// <summary>Sommet avec sa coordonnée de texture (u, w).</summary>
        public int AddVertex(Vec3 v, float u, float w)
        {
            Vertices.Add(v);
            Uvs.Add(new Vec2(u, w));
            return Vertices.Count - 1;
        }

        /// <summary>Triangle déjà orienté (sommets dans l'ordre de la face visible).</summary>
        public void AddTriangleRaw(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Ajoute le triangle (a, b, c), retourné si besoin pour faire face à <paramref name="facing"/>.</summary>
        public void AddTriangle(int a, int b, int c, Vec3 facing)
        {
            var n = Vec3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (Vec3.Dot(n, facing) < 0) { var t = b; b = c; c = t; }
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
            Uvs.Clear();
        }
    }
}
