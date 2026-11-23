using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class TerrainTests
    {
        // 3 × 2 points tous les 10 m : altitude = x / 10 (pente de 10 % vers l'est), plus 5 m sur la 2e ligne.
        private static TerrainGrid Grid() => new TerrainGrid
        {
            originX = 0f, originZ = 0f, cellMeters = 10f, width = 3, height = 2,
            heights = new[] { 0f, 1f, 2f, 5f, 6f, 7f },
        };

        [Test]
        public void Flat_WithoutData()
        {
            var t = new TerrainGrid();
            Assert.IsFalse(t.HasData);
            Assert.AreEqual(0f, t.HeightAt(100f, 100f));
        }

        [Test]
        public void HeightAt_InterpolatesAndClampsAtTheEdges()
        {
            var t = Grid();
            Assert.AreEqual(0.5f, t.HeightAt(5f, 0f), 1e-4f);
            Assert.AreEqual(3.5f, t.HeightAt(10f, 5f), 1e-4f);
            Assert.AreEqual(7f, t.HeightAt(500f, 500f), 0.02f, "borné au coin");
        }

        [Test]
        public void Grade_IsPositiveUphill()
        {
            var t = Grid();
            var east = new[] { 0f, 0f, 20f, 0f };
            var cum = WorldGeometry.CumulativeLengths(east);
            Assert.AreEqual(10f, t.GradePercent(east, cum, 10f, 5f), 0.01f);
            var west = new[] { 20f, 0f, 0f, 0f };
            Assert.AreEqual(-10f, t.GradePercent(west, WorldGeometry.CumulativeLengths(west), 10f, 5f), 0.01f);
        }
    }
}
