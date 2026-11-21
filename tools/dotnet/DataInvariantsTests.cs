// Cohérence de toutes les données (dotnet seulement : lit les fichiers JSON de Assets/Resources/Data).
// Chaque fichier : clés connues du code (une faute de frappe serait ignorée sans bruit par JsonUtility), bornes
// des valeurs, et invariants entre fichiers (carburant, arrêts, prix, versement).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Quality;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;

namespace TaxiKmer.Core.DotnetTests
{
    public class DataInvariantsTests
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };

        private static string DataDir => Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");

        /// <summary>Dossier source des données (pour les .meta d'Unity) : tools/dotnet/bin/…/net8.0 → racine du dépôt.</summary>
        private static string SourceDataDir
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Data"))) dir = dir.Parent;
                Assert.IsNotNull(dir, "Racine du dépôt introuvable.");
                return Path.Combine(dir.FullName, "Assets", "Resources", "Data");
            }
        }

        private static T Load<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(DataDir, name)), Opts);

        /// <summary>Chaque fichier de données et la classe qui le lit (null : lu hors de la logique pure, clés vérifiées à part).</summary>
        public static readonly Dictionary<string, Type> Files = new Dictionary<string, Type>
        {
            ["accidents.json"] = typeof(AccidentConfig),
            ["calendar.json"] = typeof(CalendarConfig),
            ["day.json"] = typeof(DayConfig),
            ["driving.json"] = typeof(TaxiKmer.Core.Driving.DriveConfig),
            ["fares.json"] = typeof(FareConfig),
            ["fuel.json"] = typeof(FuelConfig),
            ["owner.json"] = typeof(OwnerConfig),
            ["quality.json"] = typeof(QualityConfig),
            ["reputation.json"] = typeof(ReputationConfig),
            ["save.json"] = null,
            ["seating.json"] = typeof(SeatingConfig),
            ["stops_messassi_etoudi.json"] = typeof(StopsConfig),
            ["street.json"] = typeof(StreetConfig),
            ["traffic.json"] = typeof(CityConfig),
            ["world_messassi_etoudi.json"] = typeof(TaxiKmer.Core.World.WorldData),
            ["police.json"] = typeof(TaxiKmer.Core.Police.PoliceConfig),
            ["dialogues.json"] = typeof(TaxiKmer.Core.Talk.DialogueConfig),
            ["road.json"] = typeof(TaxiKmer.Core.Road.RoadConfig),
        };

        [Test]
        public void EveryDataFile_IsKnown_HasAUnityMeta_AndParses()
        {
            var onDisk = Directory.GetFiles(SourceDataDir, "*.json").Select(Path.GetFileName).OrderBy(n => n).ToList();
            CollectionAssert.AreEquivalent(Files.Keys.OrderBy(n => n).ToList(), onDisk,
                "Nouveau fichier de données : l'ajouter à DataInvariantsTests.Files (et à ses invariants).");
            foreach (var name in onDisk)
            {
                Assert.IsTrue(File.Exists(Path.Combine(SourceDataDir, name + ".meta")), $"{name} : .meta Unity manquant");
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDir, name)));
                Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind, name);
            }
        }

        [Test]
        public void EveryKey_IsAFieldOfTheCode()
        {
            foreach (var pair in Files)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDir, pair.Key)));
                if (pair.Value == null)
                {
                    // save.json est lu par SaveService (côté Unity) : SaveConfig.
                    var known = new HashSet<string> { "_note", "autosaveSeconds", "saveOnPause", "resumeOnStart" };
                    foreach (var p in doc.RootElement.EnumerateObject()) Assert.IsTrue(known.Contains(p.Name), $"{pair.Key} : clé inconnue « {p.Name} »");
                    continue;
                }
                var unknown = new List<string>();
                CheckKeys(doc.RootElement, pair.Value, pair.Key, unknown);
                CollectionAssert.IsEmpty(unknown, "Clés ignorées par le jeu (faute de frappe ?)");
            }
        }

        private static void CheckKeys(JsonElement e, Type type, string where, List<string> unknown)
        {
            if (e.ValueKind == JsonValueKind.Array)
            {
                var item = type.IsArray ? type.GetElementType() : null;
                if (item == null) return;
                var i = 0;
                foreach (var x in e.EnumerateArray()) CheckKeys(x, item, $"{where}[{i++}]", unknown);
                return;
            }
            if (e.ValueKind != JsonValueKind.Object) return;
            foreach (var p in e.EnumerateObject())
            {
                if (p.Name.StartsWith("_")) continue; // _note : commentaire pour Kevin
                var field = type.GetField(p.Name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) { unknown.Add($"{where}.{p.Name}"); continue; }
                CheckKeys(p.Value, field.FieldType, $"{where}.{p.Name}", unknown);
            }
        }

        [Test]
        public void Day_Fares_Seating_AreSane()
        {
            var d = Load<DayConfig>("day.json");
            Assert.That(d.startHour, Is.InRange(0, 23));
            Assert.That(d.endHour, Is.InRange(d.startHour + 1, 24));
            Assert.Greater(d.dayLengthRealSeconds, 0f);
            Assert.Greater(d.versement, 0);
            Assert.Greater(d.fuelPricePerLitre, 0);
            Assert.Greater(d.fuelLitresPer100Km, 0f);

            var f = Load<FareConfig>("fares.json");
            Assert.IsNotEmpty(f.bands);
            Assert.GreaterOrEqual(f.longDistanceFare, f.bands[f.bands.Length - 1].fare);
            foreach (var b in f.bands) Assert.Greater(b.fare, 0);

            var s = Load<SeatingConfig>("seating.json");
            Assert.AreEqual(5, s.frontMaxPassengers + s.backMaxPassengers, "5 places (décision de Kevin) : 3 derrière, 2 devant.");
            Assert.AreEqual(2, s.frontMaxPassengers);
            Assert.LessOrEqual(s.costaudWidth + s.minceWidth, s.frontBenchWidth, "Devant : un costaud et un mince.");
            Assert.Greater(s.costaudWidth * 2, s.frontBenchWidth, "Devant : pas deux costauds.");
            Assert.LessOrEqual(s.normalWidth * 3, s.backBenchWidth, "Derrière : trois clients normaux.");
            Assert.That(s.minceWidth, Is.LessThan(s.normalWidth));
            Assert.That(s.normalWidth, Is.LessThan(s.costaudWidth));
        }

        [Test]
        public void Stops_Street_AreSane_AndEveryTripHasAFare()
        {
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var fares = Load<FareConfig>("fares.json");
            var street = Load<StreetConfig>("street.json");
            Assert.AreEqual(stops.stops.Length, stops.stops.Select(x => x.id).Distinct().Count(), "Identifiants uniques.");
            foreach (var a in stops.stops)
            {
                Assert.IsNotEmpty(a.name, a.id);
                foreach (var b in stops.stops)
                {
                    if (a == b) continue;
                    var gap = Math.Abs(a.distanceAlongRouteMeters - b.distanceAlongRouteMeters);
                    Assert.Greater(gap, 2 * street.stopRadiusMeters, $"{a.id} et {b.id} : zones de dépose qui se chevauchent");
                    Assert.Greater(fares.FareFor((int)gap), 0);
                }
            }
            Assert.Greater(street.maxWaitingPerStop, 0);
            Assert.Greater(street.spawnIntervalSeconds, 0f);
            Assert.Less(street.boardDistanceMeters, street.announceDistanceMeters, "On entend le client avant de pouvoir le faire monter.");
            Assert.Less(street.boardDistanceMeters, street.missedDistanceMeters);
            Assert.Greater(street.acceptedPatienceSeconds, 0f);
            Assert.Greater(street.stoppedKmh, 0f);
        }

        [Test]
        public void Traffic_Quality_Accidents_AreSane()
        {
            var t = Load<CityConfig>("traffic.json");
            var v = t.vehicles;
            Assert.AreEqual(1f, v.car.share + v.taxi.share + v.moto.share, 1e-3f, "Les parts du trafic font 100 %.");
            Assert.That(v.competitorPickupChance, Is.InRange(0f, 1f));
            Assert.That(v.exitChance, Is.InRange(0f, 1f));
            Assert.That(v.motoRunsRedChance, Is.InRange(0f, 1f));
            Assert.Less(v.minSpawnDistanceMeters, v.activeRadiusMeters);
            Assert.Less(t.pedestrians.minSpawnDistanceMeters, t.pedestrians.activeRadiusMeters);
            Assert.LessOrEqual(t.pedestrians.walkSpeedMin, t.pedestrians.walkSpeedMax);
            foreach (var x in v.vehiclesPerKmByHour) Assert.GreaterOrEqual(x, 0f);
            foreach (var x in t.pedestrians.perKmBySideByHour) Assert.GreaterOrEqual(x, 0f);

            var q = Load<QualityConfig>("quality.json");
            Assert.AreEqual(3, q.levels.Length, "Bas, Moyen, Haut.");
            Assert.That(q.defaultLevel, Is.InRange(0, q.levels.Length - 1));
            foreach (var l in q.levels)
            {
                Assert.Less(l.fogStart, l.fogEnd, l.name);
                Assert.LessOrEqual(l.fogEnd, l.drawDistance, l.name);
                Assert.That(l.trafficScale, Is.InRange(0f, 1f), l.name);
                Assert.GreaterOrEqual(l.targetFrameRate, 30, l.name);
            }

            var a = Load<AccidentConfig>("accidents.json");
            Assert.Less(a.minImpactKmh, a.seriousImpactKmh);
            Assert.LessOrEqual(a.lightCareMin, a.lightCareMax);
            Assert.LessOrEqual(a.seriousCareMin, a.seriousCareMax);
            Assert.LessOrEqual(a.lightCareMax, a.seriousCareMin, "Un blessé grave coûte plus qu'un blessé léger.");
            Assert.That(a.seriousArrestChance, Is.InRange(0f, 1f));
            Assert.That(a.seizureChance, Is.InRange(0f, 1f));
        }

        [Test]
        public void Career_Calendar_Reputation_AreSane()
        {
            var o = Load<OwnerConfig>("owner.json");
            Assert.That(o.missesBeforeFired, Is.InRange(1, o.windowDays), "Le renvoi doit pouvoir arriver dans la fenêtre.");

            var c = Load<CalendarConfig>("calendar.json");
            Assert.That(c.firstWeekday, Is.InRange(0, c.weekdays.Length - 1));
            Assert.AreEqual(c.weekdays.Length, c.weekdays.Select(w => w.name).Distinct().Count());
            foreach (var w in c.weekdays)
            {
                Assert.Greater(w.clients, 0f, w.name);
                Assert.Greater(w.traffic, 0f, w.name);
            }

            var r = Load<ReputationConfig>("reputation.json");
            Assert.Less(r.min, r.max);
            for (var i = 1; i < r.levels.Length; i++) Assert.Greater(r.levels[i].min, r.levels[i - 1].min, "Paliers croissants.");
            Assert.LessOrEqual(r.levels[r.levels.Length - 1].min, r.max);
            Assert.Greater(r.ridePaid, 0);
            Assert.Less(r.acceptedClientGaveUp, 0);
            Assert.Less(r.pedestrianSerious, r.pedestrianLight);
        }

        [Test]
        public void Police_MatchesDefaults_StaysOffUntilKevinDecides()
        {
            var p = Load<TaxiKmer.Core.Police.PoliceConfig>("police.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Police.PoliceConfig(), Opts), JsonSerializer.Serialize(p, Opts));
            Assert.IsFalse(p.enabled, "Contrôles coupés tant que Kevin n'a pas fixé montants et issues (jalon-04.md).");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var end = stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters;
            foreach (var c in p.checkpoints) Assert.That(c.distanceAlongRouteMeters, Is.InRange(0f, end), c.id);
            Assert.AreEqual(p.papers.Length, p.papers.Select(x => x.id).Distinct().Count());
            foreach (var x in p.papers) Assert.LessOrEqual(x.startDaysLeft, x.validityDays, x.id);
            foreach (var x in new[] { p.checkpointStopChance, p.flyingStopChance, p.inventChanceWhenInOrder, p.negotiateFailChance, p.negotiateDiscount })
                Assert.That(x, Is.InRange(0f, 1f));
            Assert.LessOrEqual(p.demandInventedMin, p.demandInventedMax);
            foreach (var o in p.contestOutcomes.Concat(p.callOutcomes)) Assert.GreaterOrEqual(o.weight, 0f, o.id);
            Assert.Greater(p.contestOutcomes.Sum(o => o.weight), 0f);
            Assert.Greater(p.callOutcomes.Sum(o => o.weight), 0f);
        }

        [Test]
        public void Dialogues_Readable_KnownTriggers_ExamplesMarked_Off()
        {
            var d = Load<TaxiKmer.Core.Talk.DialogueConfig>("dialogues.json");
            Assert.IsFalse(d.enabled, "Coupé tant que Kevin n'a pas écrit ou validé les répliques (jalon-05.md).");
            Assert.AreEqual(0f, d.haggleChance, "Conséquences de la négociation : décision de Kevin.");
            Assert.AreEqual(0f, d.refusesToPayChance, "Conséquences du refus de payer : décision de Kevin.");
            Assert.AreEqual(d.lines.Length, d.lines.Select(l => l.id).Distinct().Count());
            var characters = new HashSet<string>(d.characters.Select(c => c.id));
            foreach (var l in d.lines)
            {
                CollectionAssert.Contains(TaxiKmer.Core.Talk.TalkTriggers.All, l.trigger, l.id);
                Assert.LessOrEqual(l.text.Length, d.maxChars, $"{l.id} : trop long pour être lu en conduisant");
                Assert.IsTrue(string.IsNullOrEmpty(l.character) || characters.Contains(l.character), $"{l.id} : personnage inconnu");
                StringAssert.Contains("exemple", l.text, "Seules des lignes d'exemple tant que Kevin n'a pas écrit les siennes.");
            }
            Assert.Greater(d.minShowSeconds, 0f);
            Assert.Greater(d.charsPerSecond, 0f);
        }

        [Test]
        public void Road_MatchesDefaults_Off_Sane()
        {
            var r = Load<TaxiKmer.Core.Road.RoadConfig>("road.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Road.RoadConfig(), Opts), JsonSerializer.Serialize(r, Opts));
            Assert.IsFalse(r.enabled, "Coupé tant que Kevin n'a pas fixé fréquence et qui paie (jalon-06.md).");
            foreach (var x in new[] { r.potholePunctureChance, r.potholeHitChance, r.rainChancePerDay, r.driverShareOfRepairs, r.rainGrip })
                Assert.That(x, Is.InRange(0f, 1f));
            Assert.LessOrEqual(r.rainMinMinutes, r.rainMaxMinutes);
            Assert.Less(r.flatMaxKmh, r.damagedMaxKmh);
            Assert.That(r.damagedThreshold, Is.InRange(0f, 100f));
            Assert.AreEqual(r.otherDriverAtFault.Length, r.otherDriverAtFault.Select(o => o.id).Distinct().Count());
            Assert.IsTrue(r.otherDriverAtFault.Any(o => o.id == "fuit"), "Délit de fuite : feuille de route.");
        }

        [Test]
        public void Fuel_FitsTheRoute_AndTheVersement()
        {
            var f = Load<FuelConfig>("fuel.json");
            var d = Load<DayConfig>("day.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            Assert.That(f.lowFraction, Is.InRange(0.01f, 0.99f));
            Assert.Greater(f.refuelStepFcfa, 0);
            Assert.Less(f.refuelStepFcfa, d.versement);
            Assert.AreEqual(f.stations.Length, f.stations.Select(s => s.id).Distinct().Count());
            var litresPerKm = d.fuelLitresPer100Km / 100f * f.consumptionScale;
            var route = stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters / 1000f;
            Assert.Less(litresPerKm * route * 2f, f.tankLitres, "Un plein fait au moins un aller-retour.");
            var firstStation = f.stations.Min(s => s.distanceAlongRouteMeters) / 1000f;
            Assert.Less(litresPerKm * firstStation, f.startLitres, "Le réservoir de départ atteint la station.");
            Assert.Greater(f.refuelStepFcfa / (float)d.fuelPricePerLitre, 0.5f, "Un appui à la station achète de quoi rouler.");
        }
    }
}
