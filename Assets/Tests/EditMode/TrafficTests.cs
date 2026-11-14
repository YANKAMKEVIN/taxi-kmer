using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class TrafficPathTests
    {
        [Test]
        public void SampleAndProject_RoundTrip_RightIsPositive()
        {
            // Vers le nord : la droite est à l'est (+x).
            var path = new TrafficPath(new float[] { 0, 0, 0, 1000 });
            path.Sample(300f, 2f, out var x, out var z, out var dx, out var dz);
            Assert.AreEqual(2f, x, 1e-4f);
            Assert.AreEqual(300f, z, 1e-4f);
            Assert.AreEqual(1f, dz, 1e-4f);
            path.Project(x, z, out var along, out var lateral);
            Assert.AreEqual(300f, along, 1e-3f);
            Assert.AreEqual(2f, lateral, 1e-3f);
            path.Project(-3f, 500f, out _, out lateral);
            Assert.AreEqual(-3f, lateral, 1e-3f, "À gauche : négatif.");
        }

        [Test]
        public void IsTwoWay_PerSegment()
        {
            var path = new TrafficPath(new float[] { 0, 0, 0, 100, 0, 200 }, new[] { 0, 1 });
            Assert.IsFalse(path.IsTwoWay(50f));
            Assert.IsTrue(path.IsTwoWay(150f));
            Assert.IsTrue(new TrafficPath(new float[] { 0, 0, 0, 100 }).IsTwoWay(50f), "Sans données : double sens.");
        }
    }

    public class TrafficSimTests
    {
        internal static TrafficPath North(float length = 2000f) => new TrafficPath(new[] { 0f, 0f, 0f, length });
        internal static TrafficPath South(float length = 2000f) => new TrafficPath(new[] { 0f, length, 0f, 0f });

        internal static PlayerCar PlayerAt(float z, float speed = 0f, float x = 30f) =>
            new PlayerCar { X = x, Z = z, DirX = 0f, DirZ = 1f, Speed = speed, Length = 4.4f, Width = 1.72f };

        private static TrafficConfig Quiet()
        {
            var cfg = new TrafficConfig { vehiclesPerKmByHour = new float[24], competitorPickupChance = 1f };
            return cfg;
        }

        [Test]
        public void Density_RushHoursBusierThanMidday()
        {
            var cfg = new TrafficConfig();
            Assert.Greater(cfg.DensityAt(7.5f), cfg.DensityAt(13f));
            Assert.Greater(cfg.DensityAt(17.5f), cfg.DensityAt(13f));
            Assert.AreEqual((cfg.DensityAt(7f) + cfg.DensityAt(8f)) / 2f, cfg.DensityAt(7.5f), 1e-4f);
        }

        [Test]
        public void Idm_FreeRoadAccelerates_StoppedLeaderBrakes()
        {
            var k = new TrafficConfig().car;
            Assert.Greater(TrafficSim.Idm(k, 5f, 14f, float.MaxValue, 0f), 0f);
            Assert.AreEqual(0f, TrafficSim.Idm(k, 14f, 14f, float.MaxValue, 0f), 1e-4f, "À sa vitesse voulue : plus d'accélération.");
            Assert.Less(TrafficSim.Idm(k, 14f, 14f, 15f, 0f), -k.comfortDecel, "Véhicule arrêté à 15 m : freinage fort.");
        }

        [Test]
        public void Populate_FillsAroundPlayer_AtTheHourDensity()
        {
            var sim = new TrafficSim(new TrafficConfig(), North(), South(), 3);
            sim.Populate(8f, PlayerAt(1000f, x: 0f));
            var expected = new TrafficConfig().DensityAt(8f) * 2f * 0.38f; // par sens, rayon de 380 m
            foreach (var p in new[] { 0, 1 })
            {
                var count = sim.Vehicles.Count(v => v.PathIndex == p);
                Assert.That(count, Is.InRange(expected * 0.6f, expected * 1.1f), $"sens {p}");
            }
        }

        [Test]
        public void RushHour_TwoMinutes_NoOverlap_NoReversing_SpeedsBounded()
        {
            var cfg = new TrafficConfig();
            var sim = new TrafficSim(cfg, North(3000f), South(3000f), 11);
            var player = PlayerAt(1500f, x: 40f); // loin de la route : n'interfère pas
            sim.Populate(17.5f, player);
            var previous = sim.Vehicles.ToDictionary(v => v.Id, v => v.Along);
            for (var step = 0; step < 1200; step++)
            {
                sim.Update(0.1f, 17.5f, player);
                foreach (var v in sim.Vehicles)
                {
                    Assert.LessOrEqual(v.Speed, v.DesiredSpeed * 1.05f + 0.1f, $"véhicule {v.Id}");
                    if (previous.TryGetValue(v.Id, out var before)) Assert.GreaterOrEqual(v.Along, before - 1e-3f, "jamais en arrière");
                }
                previous = sim.Vehicles.ToDictionary(v => v.Id, v => v.Along);
            }
            // Pas deux véhicules l'un dans l'autre sur la même voie.
            foreach (var a in sim.Vehicles)
            foreach (var b in sim.Vehicles)
            {
                if (a == b || a.PathIndex != b.PathIndex || Math.Abs(a.Lateral - b.Lateral) > 1f) continue;
                Assert.GreaterOrEqual(Math.Abs(a.Along - b.Along), (a.Length + b.Length) / 2f - 0.5f, $"{a.Id} / {b.Id}");
            }
            Assert.Greater(sim.Vehicles.Count, 10);
        }

        [Test]
        public void StoppedPlayerInLane_TrafficQueues_ThenOvertakes()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            // Taxi du joueur arrêté sur la voie de droite (x = +1.7 vers le nord).
            var player = PlayerAt(1000f, x: cfg.laneOffsetMeters);
            var car = sim.TrySpawn(0, 900f, VehicleKind.Car);
            Assert.IsNotNull(car);
            var minGap = float.MaxValue;
            for (var step = 0; step < 600 && car.Along < 1010f; step++)
            {
                sim.Update(0.1f, 12f, player);
                if (Math.Abs(car.Lateral - cfg.laneOffsetMeters) < 0.3f) minGap = Math.Min(minGap, 1000f - car.Along);
            }
            Assert.Greater(minGap, (car.Length + 4.4f) / 2f, "S'arrête derrière le joueur sans le toucher.");
            Assert.Greater(car.Along, 1010f, "Finit par doubler (personne en face).");
        }

        [Test]
        public void Overtake_WaitsForOncomingTraffic()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            var player = PlayerAt(1000f, x: cfg.laneOffsetMeters);
            var car = sim.TrySpawn(0, 960f, VehicleKind.Car);
            var oncoming = sim.TrySpawn(1, 2000f - 1060f, VehicleKind.Car); // arrêté en face, à 60 m devant le joueur
            Assert.IsNotNull(oncoming);
            oncoming.DesiredSpeed = 0.01f;
            for (var step = 0; step < 200; step++)
            {
                sim.Update(0.1f, 12f, player);
                Assert.AreNotEqual(VehicleState.Overtaking, car.State, "Ne double pas avec quelqu'un en face.");
            }
        }

        [Test]
        public void Moto_PassesCarsOnTheRight()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            var player = PlayerAt(1000f, x: 40f);
            var car = sim.TrySpawn(0, 1000f, VehicleKind.Car);
            car.DesiredSpeed = 4f;
            var moto = sim.TrySpawn(0, 960f, VehicleKind.Moto);
            for (var step = 0; step < 300; step++) sim.Update(0.1f, 12f, player);
            Assert.Greater(moto.Along, car.Along + 5f, "Le mototaxi est passé devant.");
            Assert.Greater(moto.Lateral, car.Lateral + 1f, "Par la droite.");
        }

        [Test]
        public void CompetitorTaxi_StopsAndTakesWaitingClient()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            var player = PlayerAt(800f, x: 40f);
            var taxi = sim.TrySpawn(0, 900f, VehicleKind.Taxi);
            taxi.Passengers = 0;
            var key = new object();
            var clients = new List<ClientSpot> { new ClientSpot { Key = key, PathIndex = 0, Along = 960f } };
            var taken = false;
            for (var step = 0; step < 400 && !taken; step++)
            {
                sim.Update(0.1f, 12f, player, clients);
                taken = sim.TakenClients.Any(t => t.Key == key && !t.ByMoto);
            }
            Assert.IsTrue(taken);
            Assert.AreEqual(1, taxi.Passengers);
            Assert.AreEqual(960f, taxi.Along, 10f, "Arrêté devant le client.");
        }

        [Test]
        public void CompetitorTaxi_Resumes_WhenClientLeaves()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            var player = PlayerAt(800f, x: 40f);
            var taxi = sim.TrySpawn(0, 900f, VehicleKind.Taxi);
            taxi.Passengers = 0;
            var clients = new List<ClientSpot> { new ClientSpot { Key = "c", PathIndex = 0, Along = 960f } };
            sim.Update(0.1f, 12f, player, clients);
            Assert.AreEqual(VehicleState.StoppingForClient, taxi.State);
            clients.Clear(); // le joueur l'a pris
            sim.Update(0.1f, 12f, player, clients);
            Assert.AreEqual(VehicleState.Driving, taxi.State);
        }

        [Test]
        public void Moto_TakesEvenTheClientAcceptedByPlayer_TaxiDoesNot()
        {
            foreach (var kind in new[] { VehicleKind.Moto, VehicleKind.Taxi })
            {
                var cfg = Quiet();
                var sim = new TrafficSim(cfg, North(), South(), 5);
                var v = sim.TrySpawn(0, 900f, kind);
                v.Passengers = 0;
                var clients = new List<ClientSpot> { new ClientSpot { Key = "c", PathIndex = 0, Along = 960f, AcceptedByPlayer = true } };
                var taken = false;
                for (var step = 0; step < 300 && !taken; step++)
                {
                    sim.Update(0.1f, 12f, PlayerAt(800f, x: 40f), clients);
                    taken = sim.TakenClients.Any(t => (string)t.Key == "c");
                }
                if (kind == VehicleKind.Moto)
                {
                    Assert.IsTrue(taken, "Le mototaxi prend le client avant qu'il monte chez le joueur.");
                    Assert.IsTrue(sim.TakenClients.Single().ByMoto);
                }
                else Assert.IsFalse(taken, "Un taxi concurrent ne prend pas le client accepté par le joueur.");
            }
        }

        [Test]
        public void FullMoto_DoesNotStop()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            var moto = sim.TrySpawn(0, 900f, VehicleKind.Moto);
            moto.Passengers = cfg.motoCapacity;
            var clients = new List<ClientSpot> { new ClientSpot { Key = "c", PathIndex = 0, Along = 960f } };
            for (var step = 0; step < 200; step++) sim.Update(0.1f, 12f, PlayerAt(800f, x: 40f), clients);
            Assert.IsEmpty(sim.TakenClients);
            Assert.Greater(moto.Along, 960f);
        }

        [Test]
        public void Traffic_StopsForCrossingPedestrian()
        {
            var cfg = Quiet();
            var sim = new TrafficSim(cfg, North(), South(), 5);
            var car = sim.TrySpawn(0, 900f, VehicleKind.Car);
            var peds = new List<PedestrianObstacle> { new PedestrianObstacle { X = cfg.laneOffsetMeters, Z = 960f } };
            for (var step = 0; step < 300; step++) sim.Update(0.1f, 12f, PlayerAt(800f, x: 40f), null, peds);
            Assert.Less(car.Along, 960f - car.Length / 2f);
            Assert.Less(car.Speed, 0.2f);
        }

        [Test]
        public void Despawns_FarFromPlayer()
        {
            var sim = new TrafficSim(Quiet(), North(), South(), 5);
            sim.TrySpawn(0, 1500f, VehicleKind.Car);
            sim.Update(0.1f, 12f, PlayerAt(200f, x: 0f));
            Assert.IsEmpty(sim.Vehicles);
        }
    }

    public class JunctionTests
    {
        // Axe vers le nord (x = 0), rue latérale à l'est (à droite) au carrefour z = 1000.
        private static TrafficPath NorthWithJunction(bool canEnter = true, bool canExit = true)
        {
            var side = new TrafficPath(new float[] { 0, 1000, 40, 1000 });
            var j = new Junction(1000f, side, canEnter, canExit, "residential");
            return new TrafficPath(new float[] { 0, 0, 0, 2000 }, null, new[] { j });
        }

        private static TrafficConfig Config()
        {
            return new TrafficConfig
            {
                vehiclesPerKmByHour = Enumerable.Repeat(10f, 24).ToArray(),
                minSpawnDistanceMeters = 5000f, // pas d'apparition sur l'axe pendant ces tests
                junctionEntriesPerMinuteAtPeak = 0f,
            };
        }

        [Test]
        public void Vehicle_TurnsRight_IntoSideRoad_ThenDisappears()
        {
            var cfg = Config();
            cfg.exitChance = 1f;
            var sim = new TrafficSim(cfg, NorthWithJunction(), TrafficSimTests.South(), 4);
            var car = sim.TrySpawn(0, 940f, VehicleKind.Car);
            var player = TrafficSimTests.PlayerAt(1000f, x: 60f);
            var sawSideRoad = false;
            var maxX = 0f;
            for (var step = 0; step < 400 && sim.Vehicles.Contains(car); step++)
            {
                sim.Update(0.1f, 12f, player);
                if (car.State == VehicleState.LeavingOnSideRoad)
                {
                    sawSideRoad = true;
                    maxX = Math.Max(maxX, car.X);
                    Assert.AreEqual(1f, car.DirX, 1e-3f, "Roule vers l'est, dans la rue de droite.");
                    Assert.Less(car.Z, 1000f, "Sur la voie de droite de la rue (côté sud).");
                }
                if (car.State == VehicleState.Exiting) Assert.LessOrEqual(car.Speed, car.DesiredSpeed + 0.1f);
            }
            Assert.IsTrue(sawSideRoad);
            Assert.Greater(maxX, 30f, "A parcouru la rue.");
            CollectionAssert.DoesNotContain(sim.Vehicles.ToList(), car, "Disparu au bout de la rue.");
        }

        [Test]
        public void NoExit_IntoOneWayComingTowardsTheAxis()
        {
            var cfg = Config();
            cfg.exitChance = 1f;
            var sim = new TrafficSim(cfg, NorthWithJunction(canExit: false), TrafficSimTests.South(), 4);
            var car = sim.TrySpawn(0, 940f, VehicleKind.Car);
            for (var step = 0; step < 200; step++) sim.Update(0.1f, 12f, TrafficSimTests.PlayerAt(1000f, x: 60f));
            Assert.Greater(car.Along, 1050f);
            Assert.AreEqual(VehicleState.Driving, car.State);
        }

        [Test]
        public void SideRoadVehicle_WaitsForGap_ThenJoinsTheAxis()
        {
            var cfg = Config();
            cfg.exitChance = 0f;
            cfg.junctionEntriesPerMinuteAtPeak = 6000f; // apparition immédiate
            cfg.maxQueuePerJunction = 1;
            var sim = new TrafficSim(cfg, NorthWithJunction(), TrafficSimTests.South(), 4);
            // Un flot serré arrive sur l'axe : pas de trou pendant un moment.
            var cars = new List<TrafficVehicle>();
            for (var z = 700f; z <= 960f; z += 20f) cars.Add(sim.TrySpawn(0, z, VehicleKind.Car));
            foreach (var c in cars) { c.DesiredSpeed = 8f; c.Speed = 8f; }
            var player = TrafficSimTests.PlayerAt(1000f, x: 80f);
            sim.Update(0.1f, 12f, player);
            var waiting = sim.Vehicles.Single(v => v.State == VehicleState.EnteringFromSideRoad);
            Assert.Greater(waiting.X, 10f, "Apparu au bout de la rue latérale.");

            var joined = false;
            for (var step = 0; step < 900 && !joined; step++)
            {
                sim.Update(0.1f, 12f, player);
                if (waiting.State == VehicleState.EnteringFromSideRoad)
                {
                    // Tant que le flot passe, il reste derrière la ligne d'arrêt.
                    if (cars.Any(c => sim.Vehicles.Contains(c) && c.Along > 985f && c.Along < 1010f))
                        Assert.GreaterOrEqual(waiting.SideDistance, cfg.stopLineMeters, "Attend à la ligne d'arrêt.");
                }
                else joined = true;
            }
            Assert.IsTrue(joined, "Finit par entrer.");
            Assert.AreEqual(cfg.laneOffsetMeters, waiting.Lateral, 1e-3f);
            Assert.Greater(waiting.Along, 1000f);
            Assert.Less(waiting.X, 2.6f, "Sur la voie de droite de l'axe.");
        }

        [Test]
        public void SideRoadVehicle_YieldsToThePlayer()
        {
            var cfg = Config();
            cfg.junctionEntriesPerMinuteAtPeak = 6000f;
            cfg.maxQueuePerJunction = 1;
            var sim = new TrafficSim(cfg, NorthWithJunction(), TrafficSimTests.South(), 4);
            // Le joueur arrive sur la voie de droite à 40 km/h, à 40 m du carrefour.
            var player = new PlayerCar { X = 1.7f, Z = 960f, DirX = 0f, DirZ = 1f, Speed = 11f, Length = 4.4f, Width = 1.72f };
            sim.Update(0.1f, 12f, TrafficSimTests.PlayerAt(1000f, x: 80f)); // apparition loin du joueur
            var waiting = sim.Vehicles.Single(v => v.State == VehicleState.EnteringFromSideRoad);
            for (var step = 0; step < 100; step++)
            {
                sim.Update(0.1f, 12f, player);
                Assert.AreEqual(VehicleState.EnteringFromSideRoad, waiting.State, "Ne coupe pas la route du joueur.");
            }
        }
    }

    public class TrafficLightTests
    {
        private static TrafficPath NorthWithLight() =>
            new TrafficPath(new float[] { 0, 0, 0, 2000 }, null, null, new[] { new PathLight(1, 1000f) });

        private static TrafficConfig Config() => new TrafficConfig
        {
            vehiclesPerKmByHour = new float[24],
            lightGreenSeconds = 20f,
            lightAmberSeconds = 3f,
            lightRedSeconds = 15f,
            motoRunsRedChance = 0f,
        };

        /// <summary>Avance la simulation jusqu'à ce que le feu 1 ait la couleur voulue.</summary>
        private static void WaitFor(TrafficSim sim, LightColor color, PlayerCar player)
        {
            for (var i = 0; i < 1000 && sim.LightFor(1) != color; i++) sim.Update(0.1f, 12f, player);
            Assert.AreEqual(color, sim.LightFor(1));
        }

        [Test]
        public void Cycle_GreenAmberRed()
        {
            var sim = new TrafficSim(Config(), NorthWithLight(), TrafficSimTests.South(), 1);
            // Feu 1 décalé de 11 s : à t = 0, il est à 11 s de son cycle (vert).
            Assert.AreEqual(LightColor.Green, sim.LightAt(1, 0f));
            Assert.AreEqual(LightColor.Amber, sim.LightAt(1, 10f));
            Assert.AreEqual(LightColor.Red, sim.LightAt(1, 13f));
            Assert.AreEqual(LightColor.Green, sim.LightAt(1, 28f));
        }

        [Test]
        public void Car_StopsAtRed_BeforeTheLine_ThenGoesOnGreen()
        {
            var cfg = Config();
            var sim = new TrafficSim(cfg, NorthWithLight(), TrafficSimTests.South(), 1);
            var player = TrafficSimTests.PlayerAt(1000f, x: 60f);
            WaitFor(sim, LightColor.Red, player);
            var car = sim.TrySpawn(0, 920f, VehicleKind.Car);
            for (var step = 0; step < 100 && sim.LightFor(1) == LightColor.Red; step++) sim.Update(0.1f, 12f, player);
            Assert.Less(car.Along + car.Length / 2f, 1000f - cfg.lightStopLineMeters + 0.5f, "Arrêté avant la ligne.");
            WaitFor(sim, LightColor.Green, player);
            for (var step = 0; step < 100; step++) sim.Update(0.1f, 12f, player);
            Assert.Greater(car.Along, 1010f, "Repart au vert.");
        }

        [Test]
        public void Queue_AtRed_DoesNotOvertake()
        {
            var cfg = Config();
            cfg.lightRedSeconds = 60f;
            var sim = new TrafficSim(cfg, NorthWithLight(), TrafficSimTests.South(), 1);
            var player = TrafficSimTests.PlayerAt(1000f, x: 60f);
            WaitFor(sim, LightColor.Red, player);
            var first = sim.TrySpawn(0, 970f, VehicleKind.Car);
            var second = sim.TrySpawn(0, 940f, VehicleKind.Car);
            for (var step = 0; step < 300 && sim.LightFor(1) == LightColor.Red; step++)
            {
                sim.Update(0.1f, 12f, player);
                Assert.AreNotEqual(VehicleState.Overtaking, second.State, "On attend au feu, on ne double pas.");
            }
            Assert.Less(second.Along, first.Along);
        }

        [Test]
        public void SomeMotos_RunTheRedLight()
        {
            var cfg = Config();
            cfg.motoRunsRedChance = 1f;
            var sim = new TrafficSim(cfg, NorthWithLight(), TrafficSimTests.South(), 1);
            var player = TrafficSimTests.PlayerAt(1000f, x: 60f);
            WaitFor(sim, LightColor.Red, player);
            var moto = sim.TrySpawn(0, 950f, VehicleKind.Moto);
            for (var step = 0; step < 60; step++) sim.Update(0.1f, 12f, player);
            Assert.Greater(moto.Along, 1000f);
        }

        [Test]
        public void SideRoadNearLight_EntersOnlyOnAxisRed()
        {
            var cfg = Config();
            cfg.junctionEntriesPerMinuteAtPeak = 6000f;
            cfg.maxQueuePerJunction = 1;
            cfg.vehiclesPerKmByHour = Enumerable.Repeat(10f, 24).ToArray();
            cfg.minSpawnDistanceMeters = 5000f;
            cfg.exitChance = 0f;
            var side = new TrafficPath(new float[] { 0, 1010, 40, 1010 });
            var path = new TrafficPath(new float[] { 0, 0, 0, 2000 }, null,
                new[] { new Junction(1010f, side, true, true) }, new[] { new PathLight(1, 1000f) });
            var sim = new TrafficSim(cfg, path, TrafficSimTests.South(), 1);
            var player = TrafficSimTests.PlayerAt(1000f, x: 80f);
            sim.Update(0.1f, 12f, player); // apparition dans la rue latérale
            WaitFor(sim, LightColor.Green, player);
            var waiting = sim.Vehicles.FirstOrDefault(v => v.State == VehicleState.EnteringFromSideRoad);
            Assert.IsNotNull(waiting);
            var joinedOnGreen = false;
            var joined = false;
            for (var step = 0; step < 900 && !joined; step++)
            {
                var colorBefore = sim.LightFor(1);
                sim.Update(0.1f, 12f, player);
                if (waiting.State == VehicleState.Driving)
                {
                    joined = true;
                    joinedOnGreen = colorBefore != LightColor.Red;
                }
            }
            Assert.IsTrue(joined);
            Assert.IsFalse(joinedOnGreen, "Entre seulement quand l'axe est au rouge.");
        }
    }

    public class PedestrianSimTests
    {
        [Test]
        public void Walkers_StayOnSidewalks_AndCrossWhenClear()
        {
            var cfg = new PedestrianConfig { crossChancePerMinute = 1f };
            var sim = new PedestrianSim(cfg, TrafficSimTests.North(), TrafficSimTests.South(), 2);
            var player = TrafficSimTests.PlayerAt(1000f, x: 60f);
            sim.Populate(8f, player);
            Assert.Greater(sim.People.Count, 3);
            var crossed = false;
            for (var step = 0; step < 1200; step++)
            {
                sim.Update(0.1f, 8f, player, Array.Empty<TrafficVehicle>());
                foreach (var p in sim.People)
                {
                    if (p.State == PedestrianState.Crossing) crossed = true;
                    else Assert.GreaterOrEqual(Math.Abs(p.Lateral), cfg.sidewalkOffsetMeters - 1e-3f, "Sur le trottoir.");
                }
            }
            Assert.IsTrue(crossed);
        }

        [Test]
        public void DoesNotCross_InFrontOfApproachingCar()
        {
            var cfg = new PedestrianConfig();
            var sim = new PedestrianSim(cfg, TrafficSimTests.North(), TrafficSimTests.South(), 2);
            var ped = sim.Spawn(0, 1000f);
            Assert.IsNotNull(ped);
            sim.Update(0.01f, 12f, TrafficSimTests.PlayerAt(500f, x: 60f), Array.Empty<TrafficVehicle>());
            // Le joueur arrive à 50 km/h, à 60 m.
            var player = new PlayerCar { X = 1.7f, Z = ped.Z - 60f, DirX = 0f, DirZ = 1f, Speed = 14f, Length = 4.4f, Width = 1.72f };
            Assert.IsFalse(sim.SafeToCross(ped, player, Array.Empty<TrafficVehicle>()));
            player.Z = ped.Z - 200f;
            Assert.IsTrue(sim.SafeToCross(ped, player, Array.Empty<TrafficVehicle>()));
            player.Speed = -2f; // il recule : il s'éloigne
            player.Z = ped.Z - 40f;
            Assert.IsTrue(sim.SafeToCross(ped, player, Array.Empty<TrafficVehicle>()));
        }
    }

    public class CityLifeTests
    {
        private static WorldData StraightWorld() => new WorldData
        {
            route = new RouteData { lengthMeters = 1000, xz = new float[] { 0, 0, 0, 1000 } },
        };

        private static Street NewStreet(int seed = 3)
        {
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                    new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
                },
            };
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), seed), shift, 1000f);
        }

        [Test]
        public void Clients_StandOnTheCurbOfTheirDirection()
        {
            var street = NewStreet();
            var city = new CityLife(new CityConfig(), StraightWorld(), street, 1, clientCurbMeters: 5f);
            foreach (var c in street.Waiting)
            {
                var place = city.Place(c);
                // Vers le nord (sens +1) : à l'est ; vers le sud : à l'ouest.
                Assert.AreEqual(5f * c.Direction, place.X, 0.01f);
                Assert.AreEqual(c.Along, place.Z, 0.5f);
                Assert.AreEqual(-c.Direction, place.FaceZ, 1e-3f, "Regarde vers les taxis qui arrivent.");
            }
        }

        [Test]
        public void CompetitorTaxi_TakesClient_FromStreet()
        {
            var street = NewStreet();
            var config = new CityConfig();
            config.vehicles.vehiclesPerKmByHour = new float[24];
            config.vehicles.competitorPickupChance = 1f;
            config.pedestrians.perKmBySideByHour = new float[24];
            var city = new CityLife(config, StraightWorld(), street, 1);
            var client = street.Waiting.First(c => c.Pickup.id == "b" && c.Direction == 1);
            var taxi = city.Traffic.TrySpawn(0, client.Along - 60f, VehicleKind.Taxi);
            taxi.Passengers = 0;
            var player = TrafficSimTests.PlayerAt(client.Along - 100f, x: 40f);
            var lost = false;
            for (var step = 0; step < 600 && !lost; step++)
            {
                street.Update(0.1f, new TaxiOnRoute { Along = 100f, Direction = 1, Lateral = 40f });
                city.Update(0.1f, 12f, player);
                lost = street.Lost.Any(l => l.Client == client && l.Reason == ClientLostReason.TookOtherTaxi);
            }
            Assert.IsTrue(lost, "Le client est parti avec le taxi concurrent.");
            CollectionAssert.DoesNotContain(street.Waiting.ToList(), client);
        }
    }

    public class StreetCompetitionTests
    {
        private static Street NewStreet(out StreetConfig cfg)
        {
            cfg = new StreetConfig();
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                    new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
                },
            };
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(cfg, stops, new ClientFactory(stops, FareConfig.Default(), 7), shift, 1000f);
        }

        private static RoadsideClient AcceptOne(Street street, out TaxiOnRoute at)
        {
            var c = street.Waiting.First(x => x.Pickup.id == "b" && x.Direction == 1);
            at = new TaxiOnRoute { Along = c.Along - 30f, Direction = 1, Lateral = 2f };
            street.Update(0.1f, at);
            Assert.AreNotEqual(HonkResult.NobodyCalling, street.Honk(at, out var accepted));
            return accepted;
        }

        [Test]
        public void TakeByOther_RefusesClientAcceptedByPlayer()
        {
            var street = NewStreet(out _);
            var c = AcceptOne(street, out _);
            Assert.IsFalse(street.TakeByOther(c));
            Assert.Contains(c, street.Waiting.ToList());
        }

        [Test]
        public void Mototaxi_TakesAcceptedClient_BeforeHeBoards()
        {
            var street = NewStreet(out _);
            var c = AcceptOne(street, out var at);
            street.Update(0.1f, at);
            Assert.IsTrue(street.TakeByOther(c, byMoto: true));
            CollectionAssert.DoesNotContain(street.Waiting.ToList(), c);
            var lost = street.Lost.Single(l => l.Client == c);
            Assert.IsTrue(lost.WasAccepted);
            Assert.IsTrue(lost.ByMoto);
        }

        [Test]
        public void AcceptedClient_GivesUp_AfterPatience()
        {
            var street = NewStreet(out var cfg);
            var c = AcceptOne(street, out var at);
            var gaveUp = false;
            for (var t = 0f; t < cfg.acceptedPatienceSeconds + 2f && !gaveUp; t += 1f)
            {
                street.Update(1f, at);
                gaveUp = street.Lost.Any(l => l.Client == c && l.Reason == ClientLostReason.GaveUp && l.WasAccepted);
            }
            Assert.IsTrue(gaveUp);
            Assert.AreEqual(RoadsideState.Waiting, c.State);
            Assert.IsTrue(street.TakeByOther(c), "Il peut alors partir avec un autre taxi.");
        }

        [Test]
        public void Boarding_NeedsTaxiOnTheClientSide()
        {
            var street = NewStreet(out _);
            var c = AcceptOne(street, out _);
            var wrongSide = new TaxiOnRoute { Along = c.Along, Direction = 1, Lateral = -3f };
            Assert.AreEqual(StreetActionKind.None, street.AvailableAction(wrongSide).Kind, "Arrêté de l'autre côté de la route.");
            var rightSide = new TaxiOnRoute { Along = c.Along, Direction = 1, Lateral = 2f };
            Assert.AreEqual(StreetActionKind.Board, street.AvailableAction(rightSide).Kind);
        }
    }
}
