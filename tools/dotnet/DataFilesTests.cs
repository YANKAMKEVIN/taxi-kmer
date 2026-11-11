// Tests propres à dotnet (hors Unity) : les fichiers de données se lisent et sont cohérents.
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.DotnetTests
{
    public class DataFilesTests
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };

        private static T Load<T>(string name)
        {
            var dir = TestContext.CurrentContext.TestDirectory;
            var path = Path.Combine(dir, "Data", name);
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts);
        }

        [Test]
        public void Seating_MatchesDefaults()
        {
            var s = Load<SeatingConfig>("seating.json");
            Assert.AreEqual(JsonSerializer.Serialize(new SeatingConfig(), Opts), JsonSerializer.Serialize(s, Opts));
        }

        [Test]
        public void Fares_AscendingAndMatchDefaults()
        {
            var f = Load<FareConfig>("fares.json");
            for (var i = 1; i < f.bands.Length; i++)
            {
                Assert.Greater(f.bands[i].maxDistanceMeters, f.bands[i - 1].maxDistanceMeters);
                Assert.GreaterOrEqual(f.bands[i].fare, f.bands[i - 1].fare);
            }
            Assert.AreEqual(JsonSerializer.Serialize(FareConfig.Default(), Opts), JsonSerializer.Serialize(f, Opts));
        }

        [Test]
        public void Day_MatchesDefaults()
        {
            var d = Load<DayConfig>("day.json");
            Assert.AreEqual(JsonSerializer.Serialize(new DayConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void Driving_MatchesDefaults()
        {
            var d = Load<TaxiKmer.Core.Driving.DriveConfig>("driving.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Driving.DriveConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void Street_MatchesDefaults()
        {
            var d = Load<StreetConfig>("street.json");
            Assert.AreEqual(JsonSerializer.Serialize(new StreetConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void World_MessassiEtoudi_IsUsable()
        {
            var w = Load<TaxiKmer.Core.World.WorldData>("world_messassi_etoudi.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            StringAssert.Contains("OpenStreetMap", w.attribution);
            Assert.Greater(w.roads.Length, 50);
            Assert.Greater(w.buildings.Length, 100);
            Assert.GreaterOrEqual(w.route.xz.Length, 4);
            Assert.AreEqual(0, w.route.xz.Length % 2);
            foreach (var r in w.roads) Assert.AreEqual(0, r.xz.Length % 2, $"route {r.id}");
            foreach (var b in w.buildings) Assert.GreaterOrEqual(b.xz.Length, 6, $"bâtiment {b.id}");

            var cum = TaxiKmer.Core.World.WorldGeometry.CumulativeLengths(w.route.xz);
            Assert.AreEqual(w.route.lengthMeters, cum[cum.Length - 1], 2f);
            Assert.AreEqual(w.route.lengthMeters, stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters, 2f,
                "Le dernier lieu est au bout de l'itinéraire.");
        }
    }
}

namespace TaxiKmer.Core.DotnetTests
{
    public class StopsDataTests
    {
        [Test]
        public void MessassiEtoudiStops_AreOrderedAndUsable()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "stops_messassi_etoudi.json");
            var cfg = JsonSerializer.Deserialize<StopsConfig>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
            Assert.GreaterOrEqual(cfg.stops.Length, 2);
            Assert.AreEqual("dispensaire-messassi", cfg.stops[0].id);
            Assert.AreEqual("etoudi", cfg.stops[cfg.stops.Length - 1].id);
            for (var i = 1; i < cfg.stops.Length; i++)
                Assert.Greater(cfg.stops[i].distanceAlongRouteMeters, cfg.stops[i - 1].distanceAlongRouteMeters);
            Assert.DoesNotThrow(() => new ClientFactory(cfg, FareConfig.Default(), 0).CreateAt("etoudi"));
        }
    }
}
