using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class BuildingLookTests
    {
        [Test]
        public void SameBuilding_SameColors_AndAllColorsUsed()
        {
            var c = new BuildingLookConfig();
            Assert.AreEqual(BuildingLook.Wall(c, 123456789L), BuildingLook.Wall(c, 123456789L));
            var walls = Enumerable.Range(1, 3000).Select(i => BuildingLook.Wall(c, 400000000L + i * 7919L)).ToList();
            for (var i = 0; i < c.WallCount; i++) Assert.Greater(walls.Count(w => w == i), 30, $"couleur {i} utilisée");
            // Ocre et beige (poids 3) plus fréquents que le vert pâle (0,75).
            Assert.Greater(walls.Count(w => w == 0), walls.Count(w => w == 4) * 2);
        }

        [Test]
        public void Roofs_TinForHouses_ConcreteForTallBuildings()
        {
            var c = new BuildingLookConfig();
            var houses = Enumerable.Range(1, 1000).Select(i => BuildingLook.Roof(c, i * 104729L, 1)).ToList();
            Assert.IsFalse(houses.Contains(c.RoofCount - 1), "pas de dalle (poids 0) sur une maison basse");
            Assert.IsTrue(houses.Contains(2), "de la tôle rouillée");
            Assert.AreEqual(c.RoofCount - 1, BuildingLook.Roof(c, 42L, 4));
        }

        [Test]
        public void SeparateRoofMesh_KeepsTheSameGeometry()
        {
            var square = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f };
            var together = new MeshData();
            WorldGeometry.AddBuilding(together, square, 6f);
            MeshData walls = new MeshData(), roof = new MeshData();
            WorldGeometry.AddBuilding(walls, roof, square, 6f);
            Assert.AreEqual(together.Vertices.Count, walls.Vertices.Count + roof.Vertices.Count);
            Assert.AreEqual(together.TriangleCount, walls.TriangleCount + roof.TriangleCount);
            Assert.IsTrue(roof.Vertices.All(v => v.Y == 6f), "le toit est en haut");
            Assert.AreEqual(new[] { 0.8f, 0.62f, 0.42f }, BuildingLook.Color(new BuildingLookConfig().wallColors, 0));
        }
    }
}
