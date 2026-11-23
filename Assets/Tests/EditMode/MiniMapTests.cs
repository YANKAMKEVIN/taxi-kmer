using NUnit.Framework;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class MiniMapTests
    {
        private static WorldData World() => new WorldData
        {
            roads = new[]
            {
                new RoadData { highway = "residential", width = 4f, xz = new[] { 0f, 0f, 100f, 0f } },
                new RoadData { highway = "primary", width = 10f, xz = new[] { 0f, 50f, 100f, 50f } },
            },
            route = new RouteData { xz = new[] { 0f, 50f, 100f, 50f } },
        };

        [Test]
        public void Raster_DrawsRoadsAndRoute_AboveSideStreets()
        {
            var cfg = new MiniMapConfig { radiusMeters = 20f, metersPerPixel = 1f };
            var r = MiniMapRaster.Build(World(), cfg);
            r.ToPixel(50f, 0f, out var sx, out var sy);
            Assert.AreEqual(MiniMapCell.Street, r.At((int)sx, (int)sy));
            r.ToPixel(50f, 50f, out var mx, out var my);
            Assert.AreEqual(MiniMapCell.Route, r.At((int)mx, (int)my));
            r.ToPixel(50f, 25f, out var ex, out var ey);
            Assert.AreEqual(MiniMapCell.Empty, r.At((int)ex, (int)ey));
            Assert.AreEqual(MiniMapCell.Empty, r.At(-1, 0), "hors image");
        }

        [Test]
        public void Raster_StaysUnderTheTextureLimit()
        {
            var w = World();
            w.roads[0].xz = new[] { 0f, 0f, 10000f, 0f };
            var r = MiniMapRaster.Build(w, new MiniMapConfig { metersPerPixel = 1f, maxTexturePixels = 512 });
            Assert.LessOrEqual(r.Width, 513);
            Assert.Greater(r.MetersPerPixel, 1f);
        }

        [Test]
        public void Place_PutsWhatIsAheadAtTheTop_AndToTheRightOnTheRight()
        {
            // Taxi qui roule vers l'est (+x) : un point devant lui est en haut, un point au sud est à sa droite.
            Assert.IsTrue(MiniMapPlacement.Place(0f, 0f, 1f, 0f, true, 100f, 50f, 0f, out var u, out var v));
            Assert.AreEqual(0f, u, 1e-5f);
            Assert.AreEqual(0.5f, v, 1e-5f);
            MiniMapPlacement.Place(0f, 0f, 1f, 0f, true, 100f, 0f, -50f, out u, out v);
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(0f, v, 1e-5f);
            // Nord en haut : la direction du taxi est ignorée.
            MiniMapPlacement.Place(0f, 0f, 1f, 0f, false, 100f, 0f, 50f, out u, out v);
            Assert.AreEqual(0.5f, v, 1e-5f);
        }

        [Test]
        public void Place_ClampsFarMarkersOnTheEdge()
        {
            Assert.IsFalse(MiniMapPlacement.Place(0f, 0f, 0f, 1f, true, 100f, 0f, 500f, out var u, out var v));
            Assert.AreEqual(0f, u, 1e-5f);
            Assert.AreEqual(1f, v, 1e-5f);
        }

        [Test]
        public void Rotation_FollowsTheHeading()
        {
            Assert.AreEqual(0f, MiniMapPlacement.RotationDegrees(0f, 1f, true), 1e-4f);
            Assert.AreEqual(90f, MiniMapPlacement.RotationDegrees(1f, 0f, true), 1e-4f);
            Assert.AreEqual(0f, MiniMapPlacement.RotationDegrees(1f, 0f, false));
        }
    }
}
