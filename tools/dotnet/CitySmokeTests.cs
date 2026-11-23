// Essai du trafic sur le vrai monde (dotnet seulement : il lit les fichiers de données).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.DotnetTests
{
    public class CitySmokeTests
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };

        private static T Load<T>(string name) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", name)), Opts);

        [Test]
        public void RushHour_DriveWholeRoute_NoVehicleStuck()
        {
            var world = Load<WorldData>("world_messassi_etoudi.json");
            var city = new CityLife(Load<CityConfig>("traffic.json"), world, null, 42);
            var route = new TrafficPath(world.route.xz);

            // Le joueur roule à 30 km/h sur la voie de droite de l'itinéraire, du dispensaire à Étoudi.
            PlayerCar Player(float along)
            {
                route.Sample(along, 1.7f, out var x, out var z, out var dx, out var dz);
                return new PlayerCar { X = x, Z = z, DirX = dx, DirZ = dz, Speed = 8.3f, Length = 4.4f, Width = 1.72f };
            }

            const float dt = 1f / 30f;
            var hour = 7.5f;
            city.Populate(hour, Player(0f));
            var stuck = new Dictionary<int, float>();
            var worst = 0f;
            var worstWhat = "";
            var maxVehicles = 0;
            var maxPeople = 0;
            var watch = Stopwatch.StartNew();
            var steps = 0;
            for (var along = 0f; along < route.Length; along += 8.3f * dt, steps++)
            {
                city.Update(dt, hour, Player(along));
                foreach (var v in city.Traffic.Vehicles)
                {
                    var t = v.Speed < 0.5f && v.State != VehicleState.Boarding ? (stuck.TryGetValue(v.Id, out var s) ? s : 0f) + dt : 0f;
                    stuck[v.Id] = t;
                    if (t > worst) { worst = t; worstWhat = $"{v.Kind} {v.State} sens {v.PathIndex} à {v.Along:F0} m"; }
                }
                maxVehicles = Math.Max(maxVehicles, city.Traffic.Vehicles.Count);
                maxPeople = Math.Max(maxPeople, city.Pedestrians.People.Count);
            }
            watch.Stop();
            var msPerStep = watch.Elapsed.TotalMilliseconds / steps;
            TestContext.WriteLine($"{steps} pas, {msPerStep:F3} ms par pas, jusqu'à {maxVehicles} véhicules et {maxPeople} piétons ; plus long arrêt : {worst:F1} s ({worstWhat})");
            Assert.Greater(maxVehicles, 15, "Heure de pointe : du monde sur la route.");
            Assert.Greater(maxPeople, 5);
            Assert.Less(worst, 60f, "Aucun véhicule bloqué plus d'une minute.");
            Assert.Less(msPerStep, 2f, "Coût de calcul raisonnable (PC de développement).");
        }

        [Test]
        public void RushHour_PlayerParkedInLane_TrafficGetsAround()
        {
            var world = Load<WorldData>("world_messassi_etoudi.json");
            var city = new CityLife(Load<CityConfig>("traffic.json"), world, null, 7);
            var path = city.Traffic.Paths[0];
            // Le joueur arrêté sur la voie de droite du trafic vers Étoudi, au milieu du trajet, pendant 3 minutes.
            path.Sample(path.Length / 2f, 1.7f, out var x, out var z, out var dx, out var dz);
            var player = new PlayerCar { X = x, Z = z, DirX = dx, DirZ = dz, Speed = 0f, Length = 4.4f, Width = 1.72f };
            const float dt = 1f / 30f;
            city.Populate(17.5f, player);
            var stuck = new Dictionary<int, float>();
            var worst = 0f;
            var passed = new HashSet<int>();
            var crossings = 0;
            var wasCrossing = new HashSet<int>();
            for (var t = 0f; t < 180f; t += dt)
            {
                city.Update(dt, 17.5f, player);
                foreach (var v in city.Traffic.Vehicles)
                {
                    var s = v.Speed < 0.5f && v.State != VehicleState.Boarding ? (stuck.TryGetValue(v.Id, out var old) ? old : 0f) + dt : 0f;
                    stuck[v.Id] = s;
                    worst = Math.Max(worst, s);
                    if (v.PathIndex == 0 && v.Kind != VehicleKind.Moto && v.Along > path.Length / 2f + 10f && stuck.ContainsKey(v.Id)) passed.Add(v.Id);
                }
                foreach (var p in city.Pedestrians.People)
                    if (p.State == PedestrianState.Crossing && wasCrossing.Add(p.Id)) crossings++;
            }
            TestContext.WriteLine($"plus long arrêt {worst:F1} s, {passed.Count} voitures passées devant le joueur, {crossings} traversées de piétons");
            Assert.Less(worst, 90f, "Personne ne reste bloqué derrière le joueur.");
            Assert.Greater(crossings, 0, "Des piétons traversent.");
        }

        [Test]
        public void FastProjection_MatchesExhaustive_OnRealPaths()
        {
            var world = Load<WorldData>("world_messassi_etoudi.json");
            var rng = new Random(5);
            foreach (var data in new[] { world.traffic.forward, world.traffic.backward })
            {
                var path = TrafficPath.From(data);
                for (var k = 0; k < 5000; k++)
                {
                    // Points autour de la trajectoire (jusqu'à 200 m de côté).
                    path.Sample((float)rng.NextDouble() * path.Length, (float)(rng.NextDouble() * 400 - 200), out var x, out var z, out _, out _);
                    path.Project(x, z, out var a1, out var l1);
                    path.ProjectExhaustive(x, z, out var a2, out var l2);
                    // Près de la trajectoire (là où le jeu s'en sert), même résultat. Plus loin, à un sommet, deux segments
                    // peuvent être à égale distance avec un latéral différent : seul l'ordre de parcours change.
                    if (Math.Abs(l2) < 30f)
                    {
                        Assert.AreEqual(l2, l1, 0.05f, $"point {x}, {z}");
                        Assert.AreEqual(a2, a1, 0.5f);
                    }
                }
            }
        }

        [Test]
        public void RoadNetwork_RealWorld_LinksTheRouteEnds_NoLongerThanTheRoute()
        {
            // Idée n° 7 : les rues OSM forment un réseau ; de Messassi à Étoudi, le plus court chemin ne dépasse pas l'itinéraire.
            var world = Load<WorldData>("world_messassi_etoudi.json");
            var net = RoadNetwork.Build(world);
            var xz = world.route.xz;
            var p = net.ShortestPath(xz[0], xz[1], xz[xz.Length - 2], xz[xz.Length - 1]);
            Assert.IsTrue(p.Found, "Réseau connexe entre les deux bouts de l'itinéraire");
            var length = WorldGeometry.CumulativeLengths(xz)[xz.Length / 2 - 1];
            Assert.LessOrEqual(p.LengthMeters, length * 1.05f);
            Assert.Greater(p.LengthMeters, length * 0.5f);
        }
    }
}
