using System;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class AccidentTests
    {
        [Test]
        public void SlowBump_NoAccident()
        {
            var o = PedestrianAccident.Resolve(new AccidentConfig(), 2f, new Random(1));
            Assert.AreEqual(InjurySeverity.None, o.Severity);
            Assert.AreEqual(0, o.CareCost);
        }

        [Test]
        public void Light_HospitalAndFirstAid()
        {
            var cfg = new AccidentConfig();
            for (var seed = 0; seed < 50; seed++)
            {
                var o = PedestrianAccident.Resolve(cfg, 15f, new Random(seed));
                Assert.AreEqual(InjurySeverity.Light, o.Severity);
                Assert.That(o.CareCost, Is.InRange(cfg.lightCareMin, cfg.lightCareMax));
                Assert.AreEqual(0, o.CareCost % 500, "Arrondi à 500 FCFA.");
                Assert.AreEqual(cfg.lightGameMinutes, o.LostGameMinutes);
                Assert.IsFalse(o.Arrested);
                Assert.IsTrue(o.PassengersLeave);
            }
        }

        [Test]
        public void Serious_SometimesArrested_SometimesSeized()
        {
            var cfg = new AccidentConfig();
            int arrested = 0, seized = 0, n = 2000;
            for (var seed = 0; seed < n; seed++)
            {
                var o = PedestrianAccident.Resolve(cfg, 50f, new Random(seed));
                Assert.AreEqual(InjurySeverity.Serious, o.Severity);
                if (o.Arrested)
                {
                    arrested++;
                    if (o.VehicleSeized) seized++;
                    Assert.AreEqual(0, o.CareCost);
                }
                else
                {
                    Assert.IsFalse(o.VehicleSeized);
                    Assert.That(o.CareCost, Is.InRange(cfg.seriousCareMin, cfg.seriousCareMax));
                    Assert.AreEqual(cfg.seriousGameMinutes, o.LostGameMinutes);
                }
            }
            Assert.AreEqual(cfg.seriousArrestChance, arrested / (float)n, 0.05f);
            Assert.AreEqual(cfg.seizureChance, seized / (float)arrested, 0.06f);
        }

        private static Street NewStreet(out Shift shift)
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
            shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), 3), shift, 1000f);
        }

        private static void BoardOne(Street street)
        {
            var c = street.Waiting.First(x => x.Pickup.id == "b" && x.Direction == 1);
            var at = new TaxiOnRoute { Along = c.Along - 30f, Direction = 1, Lateral = 2f };
            street.Update(0.1f, at);
            street.Honk(at, out _);
            var stop = new TaxiOnRoute { Along = c.Along, Direction = 1, Lateral = 2f };
            Assert.AreEqual(BoardResult.Boarded, street.Perform(stop).Board);
        }

        [Test]
        public void Street_LightAccident_PaysCare_LosesTime_PassengersLeaveUnpaid()
        {
            var street = NewStreet(out var shift);
            BoardOne(street);
            var fare = shift.Cabin.Passengers.Single().ProposedFare;
            var before = shift.Clock.GameMinutes;
            var o = street.PedestrianHit(new AccidentConfig(), 15f, new Random(2));
            Assert.AreEqual(InjurySeverity.Light, o.Severity);
            Assert.AreEqual(0, shift.Cabin.PassengerCount);
            Assert.AreEqual(o.CareCost, shift.Ledger.ExpensesTotal);
            Assert.That(shift.Clock.GameMinutes - before, Is.InRange(59, 61));
            Assert.AreEqual(-shift.Ledger.Versement - shift.Ledger.FuelCost - o.CareCost, shift.Ledger.Remaining);
            Assert.IsFalse(street.DayFinished);

            // Le bilan, en fin de journée, compte le passager descendu sans payer et les soins.
            shift.Clock.Advance(1e6f);
            street.Update(0.1f, new TaxiOnRoute { Along = 100f, Direction = 1 });
            Assert.IsTrue(street.DayFinished);
            Assert.AreEqual(1, street.Summary.UnpaidPassengers);
            Assert.AreEqual(fare, street.Summary.UnpaidFares);
            Assert.AreEqual(o.CareCost, street.Summary.ExpensesTotal);
            Assert.AreEqual(1, street.Summary.Expenses.Count);
        }

        [Test]
        public void Street_Arrest_EndsTheDayNow()
        {
            var street = NewStreet(out _);
            var cfg = new AccidentConfig { seriousArrestChance = 1f, seizureChance = 1f };
            var o = street.PedestrianHit(cfg, 60f, new Random(1));
            Assert.IsTrue(o.Arrested);
            Assert.IsTrue(street.DayFinished, "Garde à vue : la journée s'arrête.");
            Assert.IsTrue(street.Summary.Arrested);
            Assert.IsTrue(street.Summary.VehicleSeized);
            Assert.AreEqual(InjurySeverity.None, street.PedestrianHit(cfg, 60f, new Random(1)).Severity, "Plus rien après la fin.");
        }

        [Test]
        public void Clock_AdvanceGameMinutes()
        {
            var clock = new DayClock(new DayConfig());
            clock.AdvanceGameMinutes(90);
            Assert.AreEqual(6 * 60 + 90, clock.GameMinutes, 1);
        }
    }
}
