using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.AreEqual(18 * 40 * config.potholeDamagePerKmh, fast.Damage, 1e-3f);
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

        /// <summary>
        /// Décision de Kevin du 21/11 : incidents rares et marquants, ≈ 1 panne par mois de jeu et 1 crevaison par semaine,
        /// avec la journée de 35 min. 80 semaines simulées à 40 km/h (taxi réparé le soir, chauffeur qui paie) ;
        /// chaque incident peut être réglé (roue de secours, mécanicien de quartier).
        /// </summary>
        [Test]
        public void Frequencies_OneBreakdownAMonth_OnePunctureAWeek_AllSettled()
        {
            var config = new RoadConfig();
            var rng = new Random(11);
            var field = new PotholeField(config, 6132f, 1);
            var car = new VehicleCondition(config);
            int punctures = 0, breakdowns = 0;
            const int weeks = 80;
            var dayLength = new DayConfig().dayLengthRealSeconds; // la vraie durée d'une journée (day.json)
            var km = 0f;
            for (var day = 0; day < 7 * weeks; day++)
            {
                var shift = new Shift(new DayConfig(), new SeatingConfig());
                var along = 0f;
                var dir = 1;
                for (var t = 0f; t < dayLength; t += 1f)
                {
                    var speed = Math.Min(40f, car.SpeedCapKmh);
                    var next = Math.Max(0f, Math.Min(6132f, along + dir * speed / 3.6f));
                    km += Math.Abs(next - along) / 1000f;
                    var wasFlat = car.FlatTyre;
                    field.Drive(along, next, speed, car, rng);
                    if (!wasFlat && car.FlatTyre) { punctures++; car.FitSpareWheel(); }
                    if (RoadIncidents.BreakdownWhileDriving(config, car, Math.Abs(next - along), rng))
                    {
                        car.BreakDown();
                        breakdowns++;
                        car.RepairAtGarage(shift);
                    }
                    along = next;
                    if (along <= 0f || along >= 6132f) dir = -dir;
                }
                if (car.NeedsRepair) car.RepairAtGarage(shift); // le soir, au garage
                Assert.IsFalse(car.NeedsRepair);
            }
            var perWeek = punctures / (float)weeks;
            var perMonth = breakdowns / (7f * weeks / 30f);
            TestContext.WriteLine($"{km / (7 * weeks):F1} km par jour ; par semaine : {perWeek:F2} crevaisons ; par mois de 30 jours : {perMonth:F2} pannes");
            Assert.That(perWeek, Is.InRange(0.7f, 1.4f), "≈ 1 crevaison par semaine.");
            Assert.That(perMonth, Is.InRange(0.6f, 1.5f), "≈ 1 panne par mois de jeu.");
        }

        /// <summary>Pluie selon les saisons de Yaoundé (Kevin, 21/11) : mars-juin et septembre-novembre.</summary>
        [Test]
        public void Rain_FollowsYaoundeSeasons_WithTheCalendar()
        {
            var config = new RoadConfig();
            var calendar = new Calendar(new CalendarConfig());
            Assert.AreEqual(3, calendar.For(1).Month, "Jour 1 : lundi 2 mars (valeur par défaut).");
            Assert.AreEqual(2, calendar.For(1).DayOfMonth);
            Assert.AreEqual("lundi", calendar.For(1).Name);
            Assert.AreEqual(DayOfWeek.Monday, calendar.DateOf(1).DayOfWeek, "Le jour 1 tombe bien un lundi.");
            Assert.AreEqual("2 mars", Calendar.DateLabel(calendar.For(1)));
            Assert.AreEqual(4, calendar.For(31).Month);
            var rainyByMonth = new int[13];
            var daysByMonth = new int[13];
            var rng = new Random(5);
            for (var day = 1; day <= 365 * 4; day++)
            {
                var month = calendar.For(day).Month;
                daysByMonth[month]++;
                if (new WeatherDay(config, 360, 1200, rng, month).Rains) rainyByMonth[month]++;
            }
            float Share(params int[] months) => months.Sum(m => rainyByMonth[m]) / (float)months.Sum(m => daysByMonth[m]);
            var rainy = Share(3, 4, 5, 6, 9, 10, 11);
            var dry = Share(12, 1, 2, 7, 8);
            TestContext.WriteLine($"Jours de pluie : saisons des pluies {rainy:P0}, saisons sèches {dry:P0}");
            Assert.Greater(rainy, 0.4f);
            Assert.Less(dry, 0.15f);
            for (var m = 1; m <= 12; m++)
                Assert.AreEqual(WeatherDay.RainySeason(m), config.rainChanceByMonth[m - 1] >= 0.4f, $"mois {m}");
            Assert.AreEqual(config.rainChancePerDay, WeatherDay.RainChance(config, 0), "Mois inconnu : chance fixe.");
        }

        /// <summary>Fuite du joueur fautif, option B (Kevin, 21/11) : réputation perdue, retrouvé par la police selon une chance.</summary>
        [Test]
        public void PlayerFlees_LosesReputation_SometimesFoundByPolice()
        {
            var config = new RoadConfig();
            var crash = RoadIncidents.Collision(config, 20f, true, new Random(1));
            int found = 0;
            for (var seed = 0; seed < 1000; seed++)
            {
                var f = RoadIncidents.PlayerFlees(config, crash, new Random(seed));
                Assert.IsTrue(f.Fled);
                Assert.AreEqual(-10, f.ReputationDelta);
                if (f.Found) { found++; Assert.AreEqual(30000, f.Fine, "1,5 × 20 000."); Assert.AreEqual(90, f.LostMinutes); }
                else Assert.AreEqual(0, f.Fine);
            }
            Assert.AreEqual(0.5, found / 1000.0, 0.05);
            Assert.IsFalse(RoadIncidents.PlayerFlees(config, RoadIncidents.Collision(config, 20f, false, new Random(1)), new Random(1)).Fled,
                "Pas fautif : rien à fuir.");

            var shift = new Shift(new DayConfig(), new SeatingConfig());
            var street = NewStreet(shift);
            street.Reputation = new Career.DriverReputation(new Career.ReputationConfig());
            var before = shift.Clock.GameMinutes;
            street.ApplyAtFaultCollision(crash, true, new PlayerFlightOutcome { Fled = true, Found = true, ReputationDelta = -10, Fine = 30000, LostMinutes = 90 });
            Assert.AreEqual(40, street.Reputation.Score);
            Assert.AreEqual(30000, shift.Ledger.ExpensesTotal);
            Assert.AreEqual(before + 90, shift.Clock.GameMinutes, 1);
            street.ApplyAtFaultCollision(crash, false, default);
            Assert.AreEqual(50000, shift.Ledger.ExpensesTotal, "Sans fuir : il paie les dégâts de l'autre.");
        }

        /// <summary>Délit de fuite subi (Kevin, 21/11) : on peut poursuivre le fautif.</summary>
        [Test]
        public void HitAndRun_Chase_SometimesCaught_HePays()
        {
            var config = new RoadConfig();
            CollisionOutcome run = default;
            for (var seed = 0; !run.HitAndRun; seed++) run = RoadIncidents.Collision(config, 20f, false, new Random(seed));
            int caught = 0;
            for (var seed = 0; seed < 1000; seed++)
            {
                var c = RoadIncidents.Chase(config, run, new Random(seed));
                Assert.IsTrue(c.Chased);
                Assert.AreEqual(20, c.LostMinutes);
                if (c.Caught) { caught++; Assert.AreEqual(8000, c.Received, "16 points × 500 : il paie les réparations."); }
            }
            Assert.AreEqual(0.4, caught / 1000.0, 0.05);
            Assert.IsFalse(RoadIncidents.Chase(config, RoadIncidents.Collision(config, 20f, true, new Random(1)), new Random(1)).Chased);

            var shift = new Shift(new DayConfig(), new SeatingConfig());
            var street = NewStreet(shift);
            street.ApplyChase(new ChaseOutcome { Chased = true, Caught = true, Received = 8000, LostMinutes = 20 });
            Assert.AreEqual(8000, shift.Ledger.Revenue);
            Assert.AreEqual(8000, shift.Ledger.Compensations);
            Assert.AreEqual(0, shift.Ledger.RideCount);
        }

        /// <summary>RoadDay : crevaison → roue de secours ; panne → mécanicien de quartier, payé par le chauffeur.</summary>
        [Test]
        public void RoadDay_SpareWheel_Mechanic_DriverPays()
        {
            var config = new RoadConfig { potholeHitChance = 1f, potholePunctureChance = 1f };
            var road = new RoadDay(config, 6000f, 3, 10f, 360, 1200, 3, new Random(2));
            Assert.AreEqual(10f, road.Car.Damage, "Les dégâts passent d'un jour à l'autre.");
            var e = road.Drive(0f, 6000f, 50f);
            Assert.IsTrue(e.Punctured);
            Assert.AreEqual(config.flatMaxKmh, road.SpeedCapKmh);
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            var before = shift.Clock.GameMinutes;
            Assert.IsTrue(road.FitSpareWheel(shift));
            Assert.AreEqual(before + config.spareWheelMinutes, shift.Clock.GameMinutes, 1);
            Assert.IsFalse(road.FitSpareWheel(shift));
            // QA (22/11) : le bouton dit la durée des données (road.json), pas « 20 min » écrit en dur.
            Assert.AreEqual("Changer la roue (20 min)", road.SpareWheelButton);
            Assert.AreEqual("Changer la roue (1 h 15)", new RoadDay(new RoadConfig { spareWheelMinutes = 75 }, 6000f, 3, 0f, 360, 1200, 3, new Random(2)).SpareWheelButton);
            road.Car.BreakDown();
            Assert.AreEqual(0f, road.SpeedCapKmh);
            var quote = road.MechanicQuote();
            Assert.AreEqual(quote, road.CallMechanic(shift));
            Assert.AreEqual(quote, shift.Ledger.ExpensesTotal, "Le chauffeur paie tout (Kevin, 21/11).");
            Assert.IsFalse(road.Car.NeedsRepair);
            Assert.AreEqual(0, road.CallMechanic(shift));

            var off = new RoadDay(new RoadConfig { enabled = false }, 6000f, 3, 0f, 360, 1200, 3, new Random(2));
            Assert.IsFalse(off.Drive(0f, 6000f, 80f).Punctured);
            Assert.AreEqual(float.PositiveInfinity, off.SpeedCapKmh);
        }

        private static Street NewStreet(Shift shift)
        {
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 3000 },
                },
            };
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), 1), shift, 3000f);
        }
    }
}
