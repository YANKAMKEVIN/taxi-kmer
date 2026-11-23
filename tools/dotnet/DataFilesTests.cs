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
        public void DayLight_MatchesDefaults()
        {
            var d = Load<TaxiKmer.Core.Ux.DayLightConfig>("daylight.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Ux.DayLightConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void MiniMap_MatchesDefaults()
        {
            var d = Load<TaxiKmer.Core.Ux.MiniMapConfig>("minimap.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Ux.MiniMapConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void Phone_MatchesDefaults()
        {
            var d = Load<TaxiKmer.Core.Phone.PhoneConfig>("phone.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Phone.PhoneConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void Rain_MatchesDefaults()
        {
            var d = Load<TaxiKmer.Core.Road.RainLookConfig>("rain.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Road.RainLookConfig(), Opts), JsonSerializer.Serialize(d, Opts));
            Assert.AreEqual(0, d.dropsPerSecondByLevel[0], "pas de gouttes au niveau Bas");
        }

        [Test]
        public void CityEvents_MatchDefaults_AndTheirLinesExist()
        {
            var e = Load<TaxiKmer.Core.Day.CityEventsConfig>("city_events.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Day.CityEventsConfig(), Opts), JsonSerializer.Serialize(e, Opts));
            var d = Load<TaxiKmer.Core.Talk.DialogueConfig>("dialogues.json");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var ev in e.events) ids.Add(ev.id);
            foreach (var l in d.lines)
                if (!string.IsNullOrEmpty(l.cityEvent)) Assert.IsTrue(ids.Contains(l.cityEvent), $"{l.id} : événement inconnu");
            foreach (var ev in e.events)
                Assert.IsTrue(System.Array.Exists(d.lines, l => l.cityEvent == ev.id), $"{ev.id} : au moins une réplique");
        }

        [Test]
        public void Buildings_MatchDefaults()
        {
            var d = Load<TaxiKmer.Core.World.BuildingLookConfig>("buildings.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.World.BuildingLookConfig(), Opts), JsonSerializer.Serialize(d, Opts));
            Assert.AreEqual(d.WallCount, d.wallWeights.Length);
            Assert.AreEqual(d.RoofCount, d.roofWeights.Length);
        }

        [Test]
        public void Vendors_MatchDefaults()
        {
            var d = Load<TaxiKmer.Core.Traffic.VendorsConfig>("vendors.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Traffic.VendorsConfig(), Opts), JsonSerializer.Serialize(d, Opts));
        }

        [Test]
        public void Goals_MatchDefaults_KnownKinds()
        {
            var d = Load<TaxiKmer.Core.Day.DailyGoalsConfig>("goals.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Day.DailyGoalsConfig(), Opts), JsonSerializer.Serialize(d, Opts));
            foreach (var g in d.goals) CollectionAssert.Contains(TaxiKmer.Core.Day.GoalKinds.All, g.kind, g.id);
            Assert.IsFalse(d.rewardsEnabled, "Récompenses à trancher par Kevin");
        }

        [Test]
        public void UiTexts_MatchDefaults_CiteOnlyAllowedValues_AreReadable()
        {
            var d = Load<TaxiKmer.Core.Ux.UiTextsConfig>("ui_texts.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Ux.UiTextsConfig(), Opts), JsonSerializer.Serialize(d, Opts));
            foreach (var f in typeof(TaxiKmer.Core.Ux.UiTextsConfig).GetFields())
            {
                if (f.FieldType != typeof(string)) continue;
                var text = (string)f.GetValue(d);
                Assert.IsNotEmpty(text, f.Name);
                Assert.LessOrEqual(text.Length, 120, $"{f.Name} : lisible en conduisant");
                TaxiKmer.Core.Ux.UiTextsConfig.Allowed.TryGetValue(f.Name, out var allowed);
                foreach (var name in TaxiKmer.Core.Talk.DialogueText.Placeholders(text))
                    CollectionAssert.Contains(allowed ?? new string[0], name, $"{f.Name} : {{{name}}} inconnu");
            }
        }

        [Test]
        public void Quality_MatchesDefaults()
        {
            var q = Load<TaxiKmer.Core.Quality.QualityConfig>("quality.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Quality.QualityConfig(), Opts), JsonSerializer.Serialize(q, Opts));
        }

        [Test]
        public void Traffic_MatchesDefaults()
        {
            var t = Load<TaxiKmer.Core.Traffic.CityConfig>("traffic.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Traffic.CityConfig(), Opts), JsonSerializer.Serialize(t, Opts));
            Assert.AreEqual(24, t.vehicles.vehiclesPerKmByHour.Length);
            Assert.AreEqual(24, t.pedestrians.perKmBySideByHour.Length);
        }

        [Test]
        public void World_TrafficPaths_FollowTheRoute()
        {
            var w = Load<TaxiKmer.Core.World.WorldData>("world_messassi_etoudi.json");
            var route = new TaxiKmer.Core.Traffic.TrafficPath(w.route.xz);
            foreach (var data in new[] { w.traffic.forward, w.traffic.backward })
            {
                Assert.GreaterOrEqual(data.xz.Length, 4);
                Assert.AreEqual(data.xz.Length / 2 - 1, data.twoWay.Length);
                var path = TaxiKmer.Core.Traffic.TrafficPath.From(data);
                Assert.AreEqual(route.Length, path.Length, 300f, "Même longueur à quelques centaines de mètres près.");
                for (var i = 0; i < data.xz.Length; i += 2)
                {
                    route.Project(data.xz[i], data.xz[i + 1], out _, out var lateral);
                    Assert.Less(System.Math.Abs(lateral), 20f, $"point {i / 2} à moins de 20 m de l'itinéraire");
                }
            }
        }

        [Test]
        public void Accidents_MatchDefaults()
        {
            var a = Load<TaxiKmer.Core.Incidents.AccidentConfig>("accidents.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Incidents.AccidentConfig(), Opts), JsonSerializer.Serialize(a, Opts));
        }

        [Test]
        public void Owner_MatchesDefaults()
        {
            var o = Load<TaxiKmer.Core.Career.OwnerConfig>("owner.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Career.OwnerConfig(), Opts), JsonSerializer.Serialize(o, Opts));
        }

        [Test]
        public void Calendar_MatchesDefaults()
        {
            var c = Load<CalendarConfig>("calendar.json");
            Assert.AreEqual(JsonSerializer.Serialize(new CalendarConfig(), Opts), JsonSerializer.Serialize(c, Opts));
            Assert.AreEqual(7, c.weekdays.Length);
        }

        [Test]
        public void Fuel_MatchesDefaults_StationsOnTheRoute_AwayFromStops()
        {
            var f = Load<TaxiKmer.Core.Fuel.FuelConfig>("fuel.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Fuel.FuelConfig(), Opts), JsonSerializer.Serialize(f, Opts));
            Assert.LessOrEqual(f.startLitres, f.tankLitres);
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var street = Load<StreetConfig>("street.json");
            var end = stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters;
            Assert.IsNotEmpty(f.stations);
            foreach (var s in f.stations)
            {
                Assert.That(s.distanceAlongRouteMeters, Is.InRange(0f, end), s.id);
                foreach (var stop in stops.stops)
                    Assert.Greater(System.Math.Abs(stop.distanceAlongRouteMeters - s.distanceAlongRouteMeters),
                        street.stopRadiusMeters + f.stationRadiusMeters, $"{s.id} empiète sur {stop.id}");
            }
        }

        [Test]
        public void Reputation_MatchesDefaults()
        {
            var r = Load<TaxiKmer.Core.Career.ReputationConfig>("reputation.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Career.ReputationConfig(), Opts), JsonSerializer.Serialize(r, Opts));
            Assert.That(r.start, Is.InRange(r.min, r.max));
            Assert.AreEqual(r.min, r.levels[0].min, "Chaque score a un nom.");
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
