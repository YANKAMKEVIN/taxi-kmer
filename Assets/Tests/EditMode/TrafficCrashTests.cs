using System;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Road;
using TaxiKmer.Core.Traffic;

namespace TaxiKmer.Core.Tests
{
    /// <summary>Accrochages du taxi avec le trafic branchés sur la fuite (option B) et la poursuite (Kevin, 21/11).</summary>
    public class TrafficCrashTests
    {
        private static PlayerCar Taxi(float speedMs) => new PlayerCar { X = 0f, Z = 0f, DirX = 0f, DirZ = 1f, Speed = speedMs };

        [Test]
        public void Fault_WhoeverDrivesIntoTheOther()
        {
            // Le taxi à 10 m/s emboutit une voiture arrêtée devant lui.
            var j = CollisionFault.Judge(Taxi(10f), 0f, 4f, 0f, 1f, 0f);
            Assert.IsTrue(j.PlayerAtFault);
            Assert.AreEqual(36f, j.ImpactKmh, 0.01f);
            // Taxi arrêté, une voiture derrière lui arrive à 5 m/s.
            j = CollisionFault.Judge(Taxi(0f), 0f, -4f, 0f, 1f, 5f);
            Assert.IsFalse(j.PlayerAtFault);
            Assert.AreEqual(18f, j.ImpactKmh, 0.01f);
            // Le taxi recule (vitesse négative) dans la voiture arrêtée derrière.
            j = CollisionFault.Judge(Taxi(-3f), 0f, -4f, 0f, 1f, 0f);
            Assert.IsTrue(j.PlayerAtFault);
            Assert.AreEqual(10.8f, j.ImpactKmh, 0.01f);
            // Même sens, même vitesse : pas de rapprochement.
            Assert.AreEqual(0f, CollisionFault.Judge(Taxi(10f), 0f, 4f, 0f, 1f, 10f).ImpactKmh, 0.01f);
        }

        [Test]
        public void AtFault_Stop_PaysAndDamagesTaxi()
        {
            var config = new RoadConfig();
            var (street, shift) = NewStreet();
            var car = new VehicleCondition(config);
            var crash = new TrafficCrash(config, new Random(1));
            var before = shift.Clock.GameMinutes;
            var msg = crash.Report(street, car, 7, new CrashJudgement { ImpactKmh = 20f, PlayerAtFault = true }, 0f);
            StringAssert.Contains("t'arrêter", msg);
            Assert.AreEqual(CrashChoice.StopOrFlee, crash.Pending);
            Assert.AreEqual(16f, car.Damage, 0.01f, "0,8 point par km/h.");
            Assert.AreEqual("", crash.Chase(street), "Mauvais choix : ignoré.");
            StringAssert.Contains("20 000 FCFA", crash.Stop(street));
            Assert.AreEqual(CrashChoice.None, crash.Pending);
            Assert.AreEqual(20000, shift.Ledger.ExpensesTotal);
            Assert.AreEqual(before + 30, shift.Clock.GameMinutes, 1);
            Assert.AreEqual("", crash.Stop(street), "Déjà réglé : on ne paie pas deux fois.");
            Assert.AreEqual(20000, shift.Ledger.ExpensesTotal);
        }

        [Test]
        public void AtFault_Flee_LosesReputation_SometimesFound()
        {
            var config = new RoadConfig();
            int found = 0;
            for (var seed = 0; seed < 400; seed++)
            {
                var (street, shift) = NewStreet();
                street.Reputation = new Career.DriverReputation(new Career.ReputationConfig());
                var start = street.Reputation.Score;
                var crash = new TrafficCrash(config, new Random(seed));
                crash.Report(street, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f, PlayerAtFault = true }, 0f);
                var msg = crash.Flee(street);
                Assert.AreEqual(start - 10, street.Reputation.Score);
                if (shift.Ledger.ExpensesTotal > 0)
                {
                    found++;
                    Assert.AreEqual(30000, shift.Ledger.ExpensesTotal, "Retrouvé : 1,5 × 20 000.");
                    StringAssert.Contains("police", msg);
                }
            }
            Assert.AreEqual(0.5, found / 400.0, 0.08);
        }

        [Test]
        public void AtFault_NoAnswer_DrivingAwayIsFleeing_StoppedIsStopping()
        {
            var config = new RoadConfig();
            var (street, shift) = NewStreet();
            street.Reputation = new Career.DriverReputation(new Career.ReputationConfig());
            var start = street.Reputation.Score;
            var crash = new TrafficCrash(config, new Random(3));
            crash.Report(street, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f, PlayerAtFault = true }, 0f);
            Assert.AreEqual("", crash.Tick(street, config.collisionChoiceSeconds - 1f, 30f));
            Assert.AreNotEqual("", crash.Tick(street, 2f, 30f));
            Assert.AreEqual(start - 10, street.Reputation.Score, "Il roulait encore : il est parti.");

            (street, shift) = NewStreet();
            crash = new TrafficCrash(config, new Random(3));
            crash.Report(street, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f, PlayerAtFault = true }, 0f);
            crash.Tick(street, config.collisionChoiceSeconds + 1f, 0f);
            Assert.AreEqual(20000, shift.Ledger.ExpensesTotal, "Arrêté : il paie.");
        }

        [Test]
        public void PlayerCannotFlee_PaysAtOnce()
        {
            var config = new RoadConfig { playerCanFlee = false };
            var (street, shift) = NewStreet();
            var crash = new TrafficCrash(config, new Random(1));
            crash.Report(street, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f, PlayerAtFault = true }, 0f);
            Assert.AreEqual(CrashChoice.None, crash.Pending);
            Assert.AreEqual(20000, shift.Ledger.ExpensesTotal);
        }

        [Test]
        public void HitAndRun_ChaseOrLetGo()
        {
            var config = new RoadConfig { otherDriverAtFault = new[] { new OtherDriverData { id = "fuit", weight = 1f, lostMinutes = 5 } } };
            int caught = 0;
            for (var seed = 0; seed < 400; seed++)
            {
                var (street, shift) = NewStreet();
                var crash = new TrafficCrash(config, new Random(seed));
                var before = shift.Clock.GameMinutes;
                StringAssert.Contains("fuite", crash.Report(street, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f }, 0f));
                Assert.AreEqual(CrashChoice.ChaseOrLetGo, crash.Pending);
                crash.Chase(street);
                Assert.AreEqual(before + 20, shift.Clock.GameMinutes, 1);
                if (shift.Ledger.Compensations > 0) { caught++; Assert.AreEqual(8000, shift.Ledger.Compensations); }
            }
            Assert.AreEqual(0.4, caught / 400.0, 0.08);

            var (s2, sh2) = NewStreet();
            var c2 = new TrafficCrash(config, new Random(1));
            c2.Report(s2, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f }, 0f);
            var t0 = sh2.Clock.GameMinutes;
            c2.Tick(s2, config.collisionChoiceSeconds + 1f, 0f);
            Assert.AreEqual(CrashChoice.None, c2.Pending, "Sans réponse : on le laisse filer.");
            Assert.AreEqual(t0 + 5, sh2.Clock.GameMinutes, 1);
            Assert.AreEqual(0, sh2.Ledger.Compensations);
        }

        [Test]
        public void OtherAtFault_PaysOrNegotiates_IntoRevenue()
        {
            var config = new RoadConfig { otherDriverAtFault = new[] { new OtherDriverData { id = "negocie", weight = 1f, payFactor = 0.5f, lostMinutes = 30 } } };
            var (street, shift) = NewStreet();
            var crash = new TrafficCrash(config, new Random(1));
            StringAssert.Contains("négocie", crash.Report(street, new VehicleCondition(config), 1, new CrashJudgement { ImpactKmh = 20f }, 0f));
            Assert.AreEqual(CrashChoice.None, crash.Pending);
            Assert.AreEqual(4000, shift.Ledger.Compensations, "Moitié de 16 points × 500.");
            Assert.AreEqual(0, shift.Ledger.RideCount);
        }

        [Test]
        public void Ignored_LightTouch_SameVehicle_Cooldown_Disabled()
        {
            var config = new RoadConfig { otherDriverAtFault = new[] { new OtherDriverData { id = "paie", weight = 1f, payFactor = 1f } } };
            var (street, _) = NewStreet();
            var car = new VehicleCondition(config);
            var crash = new TrafficCrash(config, new Random(1));
            var hit = new CrashJudgement { ImpactKmh = 20f };
            Assert.AreEqual("", crash.Report(street, car, 1, new CrashJudgement { ImpactKmh = config.collisionMinKmh - 1f }, 0f), "Frôlement.");
            Assert.AreNotEqual("", crash.Report(street, car, 1, hit, 0f));
            Assert.AreEqual("", crash.Report(street, car, 2, hit, config.collisionCooldownSeconds - 1f), "Trop tôt après le précédent.");
            Assert.AreEqual("", crash.Report(street, car, 1, hit, 100f), "Même véhicule : déjà compté.");
            Assert.AreNotEqual("", crash.Report(street, car, 2, hit, 100f));
            Assert.AreEqual(2, crash.Count);

            var off = new RoadConfig { enabled = false };
            Assert.AreEqual("", new TrafficCrash(off, new Random(1)).Report(street, car, 3, hit, 0f), "Jalon coupé.");
        }

        [Test]
        public void Minutes_Readable()
        {
            Assert.AreEqual("30 min", TrafficCrash.Minutes(30));
            Assert.AreEqual("1 h 30", TrafficCrash.Minutes(90));
            Assert.AreEqual("2 h", TrafficCrash.Minutes(120));
        }

        private static (Street, Shift) NewStreet()
        {
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 3000 },
                },
            };
            return (new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), 1), shift, 3000f), shift);
        }
    }
}
