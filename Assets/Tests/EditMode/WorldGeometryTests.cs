using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class WorldGeometryTests
    {
        private static readonly float[] Square = { 0, 0, 10, 0, 10, 10, 0, 10 };
        private static readonly float[] LShape = { 0, 0, 20, 0, 20, 10, 10, 10, 10, 20, 0, 20 };

        [Test]
        public void SignedArea_PositiveCounterClockwise_NegativeClockwise()
        {
            Assert.AreEqual(100f, WorldGeometry.SignedArea(Square), 1e-3);
            var cw = new float[] { 0, 0, 0, 10, 10, 10, 10, 0 };
            Assert.AreEqual(-100f, WorldGeometry.SignedArea(cw), 1e-3);
        }

        [Test]
        public void Triangulate_ConcavePolygon_CoversItsArea()
        {
            var tris = WorldGeometry.Triangulate(LShape);
            Assert.AreEqual(4, tris.Count, "n - 2 triangles");
            var area = 0f;
            foreach (var t in tris)
            {
                var tri = new[] { LShape[t.A * 2], LShape[t.A * 2 + 1], LShape[t.B * 2], LShape[t.B * 2 + 1], LShape[t.C * 2], LShape[t.C * 2 + 1] };
                area += System.Math.Abs(WorldGeometry.SignedArea(tri));
            }
            Assert.AreEqual(300f, area, 1e-2, "Aire du L : 20×10 + 10×10.");
        }

        [Test]
        public void Building_HasWallsAndRoof_FacingOutAndUp()
        {
            var mesh = new MeshData();
            WorldGeometry.AddBuilding(mesh, Square, 6f);
            Assert.AreEqual(4 * 2 + 2, mesh.TriangleCount, "2 triangles par mur, 2 pour le toit");

            // Le toit regarde vers le haut.
            var t = mesh.Triangles;
            var last = t.Count - 3;
            var n = Vec3.Cross(mesh.Vertices[t[last + 1]] - mesh.Vertices[t[last]], mesh.Vertices[t[last + 2]] - mesh.Vertices[t[last]]);
            Assert.Greater(n.Y, 0);

            // Le premier mur (arête y = 0, côté sud) regarde vers le sud (z négatif).
            var w = Vec3.Cross(mesh.Vertices[t[1]] - mesh.Vertices[t[0]], mesh.Vertices[t[2]] - mesh.Vertices[t[0]]);
            Assert.Less(w.Z, 0);
        }

        [Test]
        public void ClockwiseBuilding_WallsStillFaceOut()
        {
            var mesh = new MeshData();
            WorldGeometry.AddBuilding(mesh, new float[] { 0, 0, 0, 10, 10, 10, 10, 0 }, 3f);
            var t = mesh.Triangles;
            // Premier mur : arête x = 0 (côté ouest) → normale vers x négatif.
            var w = Vec3.Cross(mesh.Vertices[t[1]] - mesh.Vertices[t[0]], mesh.Vertices[t[2]] - mesh.Vertices[t[0]]);
            Assert.Less(w.X, 0);
        }

        [Test]
        public void RoadStrip_HasWidthAndFacesUp()
        {
            var mesh = new MeshData();
            WorldGeometry.AddRoadStrip(mesh, new float[] { 0, 0, 0, 50, 0, 100 }, 7f, 0.02f);
            Assert.AreEqual(6, mesh.Vertices.Count);
            Assert.AreEqual(4, mesh.TriangleCount);
            Assert.AreEqual(7f, System.Math.Abs(mesh.Vertices[0].X - mesh.Vertices[1].X), 1e-4);
            for (var i = 0; i < mesh.Triangles.Count; i += 3)
            {
                var a = mesh.Vertices[mesh.Triangles[i]];
                var n = Vec3.Cross(mesh.Vertices[mesh.Triangles[i + 1]] - a, mesh.Vertices[mesh.Triangles[i + 2]] - a);
                Assert.Greater(n.Y, 0);
            }
        }

        [Test]
        public void SampleAlong_InterpolatesAndClamps()
        {
            var line = new float[] { 0, 0, 0, 100, 100, 100 };
            var cum = WorldGeometry.CumulativeLengths(line);
            Assert.AreEqual(200f, cum[2], 1e-3);

            WorldGeometry.SampleAlong(line, cum, 150f, out var x, out var z, out var dx, out var dz);
            Assert.AreEqual(50f, x, 1e-3);
            Assert.AreEqual(100f, z, 1e-3);
            Assert.AreEqual(1f, dx, 1e-3);
            Assert.AreEqual(0f, dz, 1e-3);

            WorldGeometry.SampleAlong(line, cum, 999f, out x, out z, out _, out _);
            Assert.AreEqual(100f, x, 1e-3);
        }
    }
}
