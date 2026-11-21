using System;
using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Road;

namespace TaxiKmer.Core.Tests
{
    public class RoadTests
    {
        [Test]
        public void Condition_LimitsSpeed_GarageRepairsAndBills()
        {
            var config = new RoadConfig();
            var car = new VehicleCondition(config);
            Assert.AreEqual(float.PositiveInfinity, car.SpeedCapKmh);
            car.Puncture();
            Assert.AreEqual(15f, car.SpeedCapKmh);
            car.FitSpareWheel();
            car.AddDamage(70f);
            Assert.AreEqual(40f, car.SpeedCapKmh, "Très abîmé : il roule moins vite.");
            car.BreakDown();
            Assert.AreEqual(0f, car.SpeedCapKmh, "En panne : il ne roule plus.");
            car.AddDamage(500f);
            Assert.AreEqual(100f, car.Damage, "Dégâts bornés à 100.");

            car.Quote(out var cost, out var minutes);
            Assert.AreEqual(100 * 500 + 10000, cost);
            Assert.AreEqual(100 * 2 + 120, minutes);
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            Assert.AreEqual(cost, car.RepairAtGarage(shift));
            Assert.AreEqual(cost, shift.Ledger.ExpensesTotal);
            Assert.AreEqual("Garage", shift.Ledger.Expenses[0].Label);
            Assert.IsFalse(car.NeedsRepair);
            Assert.AreEqual(0, car.RepairAtGarage(shift), "Rien à réparer : rien à payer.");
        }

        [Test]
        public void Garage_OwnerShare_IsData()
        {
            var config = new RoadConfig { driverShareOfRepairs = 0.5f };
            var car = new VehicleCondition(config, 10f);
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            Assert.AreEqual(2500, car.RepairAtGarage(shift), "Moitié de 5 000 (question 3 du jalon 6).");
        }

        [Test]
        public void Collision_PlayerAtFault_Pays_OtherAtFault_PaysNegotiatesOrFlees()
        {
            var config = new RoadConfig();
            var mine = RoadIncidents.Collision(config, 20f, true, new Random(1));
            Assert.IsTrue(mine.PlayerAtFault);
            Assert.AreEqual(20000, mine.PaidToOther);
            Assert.AreEqual(16f, mine.Damage, 1e-4f);

            var seen = new HashSet<string>();
            for (var seed = 0; seed < 200; seed++)
            {
                var o = RoadIncidents.Collision(config, 20f, false, new Random(seed));
                seen.Add(o.OtherDriver);
                if (o.HitAndRun) Assert.AreEqual(0, o.Received, "Délit de fuite : rien.");
                if (o.OtherDriver == "paie") Assert.AreEqual(8000, o.Received, "16 points × 500.");
                if (o.OtherDriver == "negocie") Assert.AreEqual(4000, o.Received);
            }
            CollectionAssert.AreEquivalent(new[] { "paie", "negocie", "fuit" }, seen);
        }

        [Test]
        public void Potholes_SlowIsSafe_FastDamagesAndPunctures()
        {
            var config = new RoadConfig { potholeHitChance = 1f, potholePunctureChance = 1f };
            var field = new PotholeField(config, 6000f, 3);
            Assert.AreEqual(18, field.Positions.Count, "3 par km.");
            var slow = new VehicleCondition(config);
            var hits = field.Drive(0f, 6000f, 15f, slow, new Random(1));
            Assert.AreEqual(18, hits.Count);
            Assert.AreEqual(0f, slow.Damage, "Au pas : sans danger.");
            Assert.IsFalse(slow.FlatTyre);
            var fast = new VehicleCondition(config);
            field.Drive(6000f, 0f, 60f, fast, new Random(1));
            Assert.AreEqual(18 * 40 * 0.05f, fast.Damage, 1e-3f);
            Assert.IsTrue(fast.FlatTyre);
        }

        [Test]
        public void Breakdown_MoreLikelyWhenDamaged()
        {
            var config = new RoadConfig();
            int Count(float damage)
            {
                var n = 0;
                var rng = new Random(7);
                for (var i = 0; i < 2000; i++)
                    if (RoadIncidents.BreakdownWhileDriving(config, new VehicleCondition(config, damage), 1000f, rng)) n++;
                return n;
            }
            Assert.Less(Count(0f), Count(90f));
            Assert.IsFalse(RoadIncidents.BreakdownWhileDriving(config, new VehicleCondition(config), 0f, new Random(1)));
        }

        [Test]
        public void Rain_WithinTheDay_ChangesGripTrafficClients()
        {
            var config = new RoadConfig { rainChancePerDay = 1f };
            var w = new WeatherDay(config, 360, 1200, new Random(2));
            Assert.IsTrue(w.Rains);
            Assert.GreaterOrEqual(w.RainFromMinute, 360);
            Assert.LessOrEqual(w.RainToMinute, 1200);
            var mid = (w.RainFromMinute + w.RainToMinute) / 2;
            Assert.AreEqual(0.7f, w.Grip(mid));
            Assert.AreEqual(1.3f, w.TrafficFactor(mid));
            Assert.AreEqual(1.3f, w.ClientsFactor(mid));
            Assert.AreEqual(0.7f, w.PatienceFactor(mid));
            Assert.AreEqual(1f, w.Grip(w.RainToMinute));
            Assert.IsFalse(new WeatherDay(new RoadConfig { rainChancePerDay = 0f }, 360, 1200, new Random(2)).Rains);
        }

        [Test]
        public void Week_EveryIncidentHappens_AndCanBeSettled()
        {
            // Critère du jalon 6 : chaque incident arrive au moins une fois sur une semaine de jeu et peut être réglé.
            // Moyenne sur 20 semaines : les fréquences par défaut donnent au moins un incident de chaque sorte par semaine.
            var config = new RoadConfig();
            var rng = new Random(11);
            var field = new PotholeField(config, 6132f, 1);
            var car = new VehicleCondition(config);
            int punctures = 0, breakdowns = 0, rainyDays = 0, collisions = 0, hitAndRuns = 0;
            const int weeks = 20;
            var dayLength = new DayConfig().dayLengthRealSeconds; // la vraie durée d'une journée (day.json)
            for (var day = 0; day < 7 * weeks; day++)
            {
                var shift = new Shift(new DayConfig(), new SeatingConfig());
                var weather = new WeatherDay(config, 360, 1200, rng);
                if (weather.Rains) rainyDays++;
                var along = 0f;
                var dir = 1;
                for (var t = 0f; t < dayLength; t += 1f)
                {
                    var speed = Math.Min(40f, car.SpeedCapKmh);
                    var next = Math.Max(0f, Math.Min(6132f, along + dir * speed / 3.6f));
                    var wasFlat = car.FlatTyre;
                    field.Drive(along, next, speed, car, rng);
                    if (!wasFlat && car.FlatTyre) { punctures++; car.FitSpareWheel(); }
                    if (RoadIncidents.BreakdownWhileDriving(config, car, Math.Abs(next - along), rng))
                    {
                        car.BreakDown();
                        breakdowns++;
                        car.RepairAtGarage(shift);
                    }
                    if (rng.NextDouble() < 1.0 / 300.0) // un accrochage toutes les 5 min en moyenne (essai)
                    {
                        var c = RoadIncidents.Collision(config, 15f, rng.NextDouble() < 0.3, rng);
                        collisions++;
                        if (c.HitAndRun) hitAndRuns++;
                        car.AddDamage(c.Damage);
                    }
                    along = next;
                    if (along <= 0f || along >= 6132f) dir = -dir;
                }
                if (car.NeedsRepair) car.RepairAtGarage(shift); // le soir, au garage
                Assert.IsFalse(car.NeedsRepair);
            }
            TestContext.WriteLine($"Par semaine : {punctures / (float)weeks:F1} crevaisons, {breakdowns / (float)weeks:F1} pannes, {rainyDays / (float)weeks:F1} jours de pluie, {collisions / (float)weeks:F1} accrochages dont {hitAndRuns / (float)weeks:F1} délits de fuite");
            Assert.GreaterOrEqual(punctures, weeks);
            Assert.GreaterOrEqual(breakdowns, weeks);
            Assert.GreaterOrEqual(rainyDays, weeks);
            Assert.GreaterOrEqual(hitAndRuns, weeks);
        }
    }
}
