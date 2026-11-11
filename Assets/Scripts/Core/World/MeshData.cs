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

    /// <summary>
    /// Maillage en cours de construction. Les triangles sont orientés pour que leur face visible
    /// pointe vers la normale demandée (convention Unity : normale = (b - a) × (c - a)).
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<Vec3> Vertices = new List<Vec3>();
        public readonly List<int> Triangles = new List<int>();

        public int TriangleCount => Triangles.Count / 3;

        public int AddVertex(Vec3 v)
        {
            Vertices.Add(v);
            return Vertices.Count - 1;
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
        }
    }
}
