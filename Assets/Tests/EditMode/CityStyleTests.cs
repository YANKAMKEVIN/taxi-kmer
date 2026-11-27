using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class CityStyleTests
    {
        private static readonly float[] Square = { 0, 0, 10, 0, 10, 8, 0, 8 };

        [Test]
        public void Style_IsReproducible_AndTallerNearTheRoute()
        {
            var c = new CityStyleConfig();
            Assert.AreEqual(CityStyle.For(c, 42, 4, 10f).Levels, CityStyle.For(c, 42, 4, 10f).Levels, "Même bâtiment, même style");
            var near = Enumerable.Range(0, 400).Average(i => CityStyle.For(c, i, 4, 10f).Levels);
            var far = Enumerable.Range(0, 400).Average(i => CityStyle.For(c, i, 4, 500f).Levels);
            Assert.Greater(near, far + 0.8f, "Immeubles le long de la route, maisons basses plus loin");
            Assert.IsTrue(Enumerable.Range(0, 400).All(i => !CityStyle.For(c, i, 4, 500f).Detailed), "Pas de détails loin de la route");
            Assert.IsTrue(Enumerable.Range(0, 400).All(i => !CityStyle.For(c, i, 4, 500f).Shop), "Boutiques seulement près de la route");
        }

        [Test]
        public void Pick_FollowsWeights()
        {
            Assert.AreEqual(0, CityStyle.Pick(new[] { 1f, 1f }, 0.2));
            Assert.AreEqual(1, CityStyle.Pick(new[] { 1f, 1f }, 0.7));
            Assert.AreEqual(1, CityStyle.Pick(new[] { 0f, 1f }, 0.0));
            Assert.AreEqual(0, CityStyle.Pick(new float[0], 0.5));
        }

        [Test]
        public void Facade_ShopFacingTheRoad_GetsShuttersAndSign_AndEveryVertexHasUv()
        {
            var c = new CityStyleConfig();
            var s = new BuildingStyle { Levels = 2, Wall = 3, Roof = RoofKind.Concrete, Shop = true, Detailed = true, Window = 5, Sign = 7, Door = 12 };
            var set = new MeshSet();
            FacadeBuilder.Build(set, Square, s, c, (x, z, nx, nz) => nz < -0.5f); // la route est au sud (z négatif)
            Assert.Greater(set[CitySlot.Wall0 + 3].TriangleCount, 0, "Murs dans la variante tirée");
            Assert.Greater(set[CitySlot.Signs].TriangleCount, 0, "Une enseigne côté route");
            Assert.AreEqual(2, set[CitySlot.Signs].TriangleCount, "Une seule enseigne par boutique");
            Assert.Greater(set[CitySlot.Windows].TriangleCount, 4, "Fenêtres, rideaux, porte");
            Assert.Greater(set[CitySlot.RoofConcrete].TriangleCount, 0, "Dalle béton");
            foreach (var kv in set.Slots) Assert.AreEqual(kv.Value.Vertices.Count, kv.Value.Uvs.Count, $"UV pour chaque sommet ({kv.Key})");
            var top = set[CitySlot.Wall0 + 3].Vertices.Max(v => v.Y);
            Assert.AreEqual(2 * c.levelHeight + c.parapetHeight, top, 1e-4f, "Murs jusqu'à l'acrotère");
        }

        [Test]
        public void Facade_TinGable_OnFourCorners_RidgeAboveWalls_FarBuildingsPlain()
        {
            var c = new CityStyleConfig();
            var s = new BuildingStyle { Levels = 1, Roof = RoofKind.TinGable, Rust = true, Detailed = false };
            var set = new MeshSet();
            FacadeBuilder.Build(set, Square, s, c, (x, z, nx, nz) => true);
            var roof = set[CitySlot.RoofRust];
            Assert.AreEqual(4, roof.TriangleCount, "Deux pans de deux triangles");
            Assert.AreEqual(c.levelHeight + c.ridgeHeight, roof.Vertices.Max(v => v.Y), 1e-4f, "Faîte au-dessus des murs");
            Assert.AreEqual(0, set[CitySlot.Windows].TriangleCount, "Pas de fenêtres hors du rayon de détail");
        }
    }
}
