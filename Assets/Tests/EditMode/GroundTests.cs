using System;
using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    /// <summary>Idée n° 8 (décision de Kevin du 23/11) : ce qui est posé au sol suit le relief, comme le sol construit.</summary>
    public class GroundTests
    {
        // 2 × 2 points tous les 10 m, non plans : seul le coin (10, 10) est haut (4 m).
        private static TerrainGrid Bump() => new TerrainGrid
        {
            originX = 0f, originZ = 0f, cellMeters = 10f, width = 2, height = 2,
            heights = new[] { 0f, 0f, 0f, 4f },
        };

        // Pente régulière de 10 % vers l'est (x), 3 × 3 points.
        private static TerrainGrid Slope() => new TerrainGrid
        {
            originX = 0f, originZ = 0f, cellMeters = 10f, width = 3, height = 3,
            heights = new[] { 0f, 1f, 2f, 0f, 1f, 2f, 0f, 1f, 2f },
        };

        [Test]
        public void HeightAt_FollowsTheGroundMeshTriangles()
        {
            // Le sol de WorldBuilder coupe chaque case par la diagonale (10, 0)-(0, 10) : sous elle, tout est à 0 ;
            // au-dessus, le plan monte vers le coin haut. Une interpolation bilinéaire donnerait 1 m au centre.
            var t = Bump();
            Assert.AreEqual(0f, t.HeightAt(5f, 5f), 1e-4f, "centre, sur la diagonale");
            Assert.AreEqual(0f, t.HeightAt(2f, 6f), 1e-4f, "sous la diagonale");
            Assert.AreEqual(2f, t.HeightAt(7.5f, 7.5f), 1e-4f, "au-dessus de la diagonale");
            Assert.AreEqual(4f, t.HeightAt(10f, 10f), 0.02f, "coin haut");
        }

        [Test]
        public void HeightAt_IsContinuousAcrossTheDiagonal()
        {
            var t = Bump();
            for (var x = 0.5f; x < 10f; x += 0.5f)
            {
                var z = 10f - x;
                Assert.AreEqual(t.HeightAt(x - 0.001f, z - 0.001f), t.HeightAt(x + 0.001f, z + 0.001f), 0.01f);
            }
        }

        [Test]
        public void Height_IsZeroWithoutTerrain()
        {
            Assert.AreEqual(0f, Ground.Height(null, 10f, 10f));
            Assert.AreEqual(0f, Ground.Height(new TerrainGrid(), 10f, 10f));
            Assert.AreEqual(0f, Ground.Vehicle(null, 5f, 5f, 1f, 0f, 4.3f, out var rise));
            Assert.AreEqual(0f, rise);
        }

        [Test]
        public void Vehicle_UphillIsPitchedAndNeitherSinksNorFloats()
        {
            var t = Slope();
            var h = Ground.Vehicle(t, 10f, 10f, 1f, 0f, 4f, out var rise);
            Assert.AreEqual(1f, h, 1e-4f, "centre à la hauteur du sol");
            Assert.AreEqual(0.4f, rise, 1e-4f, "10 % sur 4 m");
            Assert.AreEqual(5.71f, Ground.PitchDegrees(rise, 4f), 0.01f);
            // Dans l'autre sens, il descend ; en travers de la pente, il ne pique pas du nez.
            Ground.Vehicle(t, 10f, 10f, -1f, 0f, 4f, out var down);
            Assert.AreEqual(-0.4f, down, 1e-4f);
            Ground.Vehicle(t, 10f, 10f, 0f, 3f, 4f, out var across);
            Assert.AreEqual(0f, across, 1e-4f);
        }

        [Test]
        public void Vehicle_OnACrest_SitsBetweenFrontAndBackWheels()
        {
            // Sommet : le centre est plus haut que la moyenne avant / arrière ; le véhicule est posé sur ses deux essieux.
            var t = new TerrainGrid { originX = 0f, originZ = 0f, cellMeters = 10f, width = 3, height = 2, heights = new[] { 0f, 2f, 0f, 0f, 2f, 0f } };
            var h = Ground.Vehicle(t, 10f, 0f, 1f, 0f, 4f, out var rise);
            Assert.AreEqual(1.6f, h, 1e-4f);
            Assert.AreEqual(0f, rise, 1e-4f);
            Assert.LessOrEqual(h, t.HeightAt(10f, 0f));
        }

        [Test]
        public void Densify_KeepsThePointsAndLimitsTheStep()
        {
            var xz = new[] { 0f, 0f, 12f, 0f, 12f, 3f };
            var d = Ground.Densify(xz, 5f);
            // 12 m → 3 pas de 4 m, puis 3 m → 1 pas.
            Assert.AreEqual(new[] { 0f, 0f, 4f, 0f, 8f, 0f, 12f, 0f, 12f, 3f }, d);
            for (var i = 2; i < d.Length; i += 2)
                Assert.LessOrEqual(Math.Sqrt(Math.Pow(d[i] - d[i - 2], 2) + Math.Pow(d[i + 1] - d[i - 1], 2)), 5.0001);
            Assert.AreSame(xz, Ground.Densify(xz, 20f), "déjà assez fine");
            Assert.AreEqual(new[] { 0f, 0f, 5f, 0f, 10f, 0f }, Ground.Densify(new[] { 0f, 0f, 10f, 0f }, 5f), "10 m en 2 pas de 5 m");
        }
    }
}
