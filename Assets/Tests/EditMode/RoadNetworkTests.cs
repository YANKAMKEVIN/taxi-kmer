using NUnit.Framework;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class RoadNetworkTests
    {
        // Grande route de (0,0) à (1000,0) en coude par (500,400), et une rue latérale droite (0,0) → (1000,0) qui raccourcit.
        private static WorldData World(bool shortcutOneWayBack = false) => new WorldData
        {
            roads = new[]
            {
                new RoadData { highway = "primary", xz = new[] { 0f, 0f, 500f, 400f, 1000f, 0f } },
                new RoadData { highway = "residential", oneway = shortcutOneWayBack, xz = new[] { 1000f, 0f, 500f, 0f, 0f, 0f } },
            },
        };

        [Test]
        public void ShortestPath_TakesTheSideStreetShortcut()
        {
            var net = RoadNetwork.Build(World());
            Assert.AreEqual(4, net.NodeCount, "les extrémités partagées sont un seul carrefour");
            var p = net.ShortestPath(0f, 0f, 1000f, 0f);
            Assert.IsTrue(p.Found);
            Assert.AreEqual(1000f, p.LengthMeters, 0.1f);
            Assert.AreEqual(new[] { 0f, 0f, 500f, 0f, 1000f, 0f }, p.Xz);
        }

        [Test]
        public void ShortestPath_RespectsOneWayStreets()
        {
            var net = RoadNetwork.Build(World(shortcutOneWayBack: true));
            var p = net.ShortestPath(0f, 0f, 1000f, 0f);
            Assert.IsTrue(p.Found);
            Assert.Greater(p.LengthMeters, 1200f, "la rue latérale est à sens unique dans l'autre sens");
            Assert.AreEqual(1000f, net.ShortestPath(1000f, 0f, 0f, 0f).LengthMeters, 0.1f);
        }

        [Test]
        public void ShortestPath_NotFound_WhenDisconnected()
        {
            var w = World();
            w.roads = new[] { w.roads[0], new RoadData { xz = new[] { 5000f, 5000f, 5100f, 5000f } } };
            var p = RoadNetwork.Build(w).ShortestPath(0f, 0f, 5100f, 5000f);
            Assert.IsFalse(p.Found);
        }

        [Test]
        public void StopPlacement_ProjectsSideStreetStops_OnTheRoute()
        {
            var route = new[] { 0f, 0f, 1000f, 0f };
            var cum = WorldGeometry.CumulativeLengths(route);
            var onRoute = new Stop { id = "a", distanceAlongRouteMeters = 300 };
            Assert.AreEqual(300f, StopPlacement.AccessAlong(onRoute, route, cum, out var lat0));
            Assert.AreEqual(0f, lat0);
            Assert.IsFalse(StopPlacement.IsOffRoute(onRoute, route, cum));
            var side = new Stop { id = "b", x = 600f, z = 120f };
            Assert.AreEqual(600f, StopPlacement.AccessAlong(side, route, cum, out var lat), 0.01f);
            Assert.AreEqual(120f, lat, 0.01f);
            Assert.IsTrue(StopPlacement.IsOffRoute(side, route, cum));
        }
    }
}
