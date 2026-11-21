using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Tests
{
    public class FuelTests
    {
        private static FuelConfig Fuel() => new FuelConfig
        {
            tankLitres = 40f,
            startLitres = 10f,
            consumptionScale = 10f,
            refuelStepFcfa = 2000,
            stations = new[] { new StationData { id = "s", name = "Station", distanceAlongRouteMeters = 700f } },
        };

        private static StopsConfig Stops() => new StopsConfig
        {
            stops = new[]
            {
                new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
            },
        };

        private static Street NewStreet(FuelConfig fuel, float? litres = null)
        {
            var stops = Stops();
            var shift = new Shift(new DayConfig(), new SeatingConfig(), fuel, litres);
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), 5), shift, 1000f);
        }

        private static TaxiOnRoute At(float along, float speedKmh = 0f) =>
            new TaxiOnRoute { Along = along, Direction = 1, Lateral = 2f, SpeedKmh = speedKmh };

        [Test]
        public void Tank_EmptiesWhileDriving_ScaledConsumption()
        {
            var tank = new FuelTank(Fuel(), new DayConfig(), 10f); // 8 L/100 km × 10 = 0,8 L/km
            Assert.AreEqual(0.8f, tank.Consume(1000f), 1e-4);
            Assert.AreEqual(9.2f, tank.Litres, 1e-4);
            Assert.AreEqual(9.2f, tank.Consume(100000f), 1e-4, "On ne brûle pas plus que ce qu'il y a.");
            Assert.IsTrue(tank.IsEmpty);
            Assert.AreEqual(Fuel().emptyCrawlKmh, tank.SpeedCapKmh);
            tank.Add(1f);
            Assert.IsTrue(float.IsPositiveInfinity(tank.SpeedCapKmh));
            Assert.IsTrue(tank.IsLow);
        }

        [Test]
        public void Tank_IsBounded_AndKnowsItsStations()
        {
            var tank = new FuelTank(Fuel(), new DayConfig(), 99f);
            Assert.AreEqual(40f, tank.Litres);
            Assert.IsTrue(tank.IsFull);
            Assert.AreEqual(0f, tank.LitresFor(2000, 840));
            Assert.AreEqual("s", tank.StationAt(720f).id);
            Assert.IsNull(tank.StationAt(760f));
        }

        [Test]
        public void Shift_DriveConsumes_BuyFuelPaysForWhatWasBought()
        {
            var shift = new Shift(new DayConfig(), new SeatingConfig(), Fuel());
            shift.Drive(5000f); // 4 L brûlés
            Assert.AreEqual(6f, shift.Tank.Litres, 1e-4);
            Assert.AreEqual(0, shift.Ledger.FuelCost, "Rien d'acheté : le bilan ne compte pas ce qui est brûlé.");
            var litres = shift.BuyFuel(2000, out var cost);
            Assert.AreEqual(2000f / 840f, litres, 1e-4);
            Assert.AreEqual(2000, cost);
            Assert.AreEqual(2000, shift.Ledger.FuelCost);
            Assert.AreEqual(litres, shift.Ledger.FuelLitres, 1e-4);
            Assert.AreEqual(shift.Ledger.Revenue - shift.Ledger.Versement - 2000, shift.Ledger.Remaining);
        }

        [Test]
        public void BuyFuel_StopsAtFullTank()
        {
            var shift = new Shift(new DayConfig(), new SeatingConfig(), Fuel(), 39f);
            var litres = shift.BuyFuel(2000, out var cost);
            Assert.AreEqual(1f, litres, 1e-4);
            Assert.AreEqual(840, cost);
            Assert.AreEqual(0f, shift.BuyFuel(2000, out cost));
            Assert.AreEqual(0, cost);
        }

        [Test]
        public void WithoutTank_FuelStillCountedByDistance()
        {
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            Assert.IsNull(shift.Tank);
            shift.Drive(50000f);
            Assert.AreEqual(3360, shift.Ledger.FuelCost);
        }

        [Test]
        public void Station_OffersRefuel_WhenStoppedAndNotFull()
        {
            var street = NewStreet(Fuel());
            var action = street.AvailableAction(At(705f));
            Assert.AreEqual(StreetActionKind.Refuel, action.Kind);
            Assert.AreEqual("s", action.Station.id);
            Assert.AreEqual(2000, action.FuelCost);
            Assert.AreEqual(StreetActionKind.None, street.AvailableAction(At(705f, 20f)).Kind, "Il faut s'arrêter.");
            Assert.AreEqual(StreetActionKind.None, street.AvailableAction(At(800f)).Kind, "Loin de la station.");

            var outcome = street.Perform(At(705f));
            Assert.AreEqual(StreetActionKind.Refuel, outcome.Kind);
            Assert.AreEqual(2000, outcome.FuelCost);
            Assert.AreEqual(10f + 2000f / 840f, street.Shift.Tank.Litres, 1e-3);

            var full = NewStreet(Fuel(), 40f);
            Assert.AreEqual(StreetActionKind.None, full.AvailableAction(At(705f)).Kind, "Plein : rien à acheter.");
        }

        [Test]
        public void Summary_KeepsTheTankLevel_AndTheSaveCarriesIt()
        {
            var street = NewStreet(Fuel());
            street.Shift.Drive(2500f); // 2 L brûlés → 8 L
            street.Perform(At(705f));
            var data = new SaveData();
            SaveSnapshot.CaptureDay(data, street, 705f, 1, 5);
            var fresh = NewStreet(Fuel());
            SaveSnapshot.RestoreDay(data.day, fresh);
            Assert.AreEqual(street.Shift.Tank.Litres, fresh.Shift.Tank.Litres, 1e-4);
            Assert.AreEqual(2000, fresh.Shift.Ledger.FuelCost, "Le carburant acheté reste compté.");

            street.EndDayNow();
            Assert.AreEqual(street.Shift.Tank.Litres, street.Summary.TankLitres, 1e-4);
            var career = SaveSnapshot.LoadCareer(data, new TaxiKmer.Core.Career.OwnerConfig());
            var closure = career.CloseDay(street.Summary);
            SaveSnapshot.RecordDay(data, street.Summary, closure, career);
            Assert.AreEqual(street.Shift.Tank.Litres, SaveSnapshot.TankAtDayStart(data, Fuel()), 1e-4, "Le réservoir passe d'un jour à l'autre.");
            Assert.AreEqual(2000, data.career.history.Single().fuelCost);
        }

        [Test]
        public void SpeedCap_LimitsThrottle()
        {
            var logic = new DriveLogic(new DriveConfig()) { SpeedCapKmh = 8f };
            var input = new DriveInput { Accelerate = true };
            Assert.Greater(logic.Step(input, 0f, 0.02f).Throttle, 0.9f);
            Assert.AreEqual(0f, logic.Step(input, 8f / 3.6f, 0.02f).Throttle, 1e-4, "Réservoir vide : on se traîne.");
            logic.SpeedCapKmh = float.PositiveInfinity;
            Assert.Greater(logic.Step(input, 8f / 3.6f, 0.02f).Throttle, 0.9f);
        }
    }
}

namespace TaxiKmer.Core.Tests
{
    public class FuelGaugeTests
    {
        [Test]
        public void Texts()
        {
            var tank = new FuelTank(new FuelConfig { tankLitres = 40f }, new DayConfig(), 12.7f);
            Assert.AreEqual("12 L", FuelGauge.Label(tank));
            Assert.AreEqual("Carburant +2,4 L (2 000 FCFA)", FuelGauge.ActionLabel(2000f / 840f, 2000));
            Assert.AreEqual("+2,4 L pour 2 000 FCFA · réservoir 12 / 40 L", FuelGauge.Bought(2000f / 840f, 2000, tank));
        }
    }
}
