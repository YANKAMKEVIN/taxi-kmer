using System;
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
            Assert.AreEqual(0f, tank.SpeedCapKmh, "Panne sèche : le taxi s'arrête, le joueur choisit.");
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
        public void DryTank_StopsTheTaxi_UntilThePlayerChooses_ThenPushingIsSlow()
        {
            var street = NewStreet(Fuel(), 0.001f);
            var tank = street.Shift.Tank;
            tank.Consume(10f);
            Assert.IsTrue(tank.IsEmpty);
            Assert.IsTrue(tank.Stranded);
            Assert.AreEqual(0f, tank.SpeedCapKmh, "Plus de règle des 8 km/h : le taxi est arrêté.");
            Assert.IsTrue(street.DryTankChoice(At(300f)), "Hors de la station : on propose le choix.");
            Assert.IsFalse(street.DryTankChoice(At(700f)), "À la station : on achète du carburant.");
            Assert.AreEqual(StreetActionKind.Refuel, street.AvailableAction(At(700f)).Kind);

            Assert.IsTrue(street.PushTaxi());
            Assert.IsTrue(tank.Pushing);
            Assert.IsFalse(tank.Stranded);
            Assert.IsFalse(street.DryTankChoice(At(300f)));
            Assert.AreEqual(3f, tank.SpeedCapKmh, "On pousse au pas.");
            // 400 m jusqu'à la station au pas : 8 min réelles, plus de 3 h de jeu avec la journée de 35 min.
            var day = new DayConfig();
            var realSeconds = 400f / (tank.SpeedCapKmh / 3.6f);
            Assert.AreEqual(480f, realSeconds, 1e-2);
            Assert.Greater(realSeconds / day.dayLengthRealSeconds * (day.endHour - day.startHour) * 60f, 180f, "Très coûteux en temps.");

            street.Perform(At(700f)); // carburant à la station
            Assert.IsFalse(tank.IsEmpty);
            Assert.IsFalse(tank.Pushing, "Avec du carburant, on ne pousse plus.");
            Assert.IsTrue(float.IsPositiveInfinity(tank.SpeedCapKmh));
            Assert.IsFalse(street.PushTaxi(), "Rien à pousser avec du carburant.");
        }

        [Test]
        public void DryTank_Technician_ComesAfterADelay_ChargesFuelIncluded_ThenWeGo()
        {
            var street = NewStreet(Fuel(), 0f);
            var shift = street.Shift;
            var quote = street.TechnicianQuote();
            Assert.AreEqual(5f, quote.Litres, 1e-4);
            Assert.AreEqual(2000 + 4200, quote.Cost, "Déplacement 2 000 + 5 L à 840 FCFA.");
            Assert.AreEqual(45, quote.DelayMinutes);
            var before = shift.Clock.GameMinutes;

            Assert.IsTrue(street.CallTechnician(out var call));
            Assert.AreEqual(quote.Cost, call.Cost);
            Assert.AreEqual(5f, shift.Tank.Litres, 1e-4);
            Assert.IsFalse(shift.Tank.Stranded);
            Assert.IsTrue(float.IsPositiveInfinity(shift.Tank.SpeedCapKmh), "On repart.");
            Assert.AreEqual(before + 45, shift.Clock.GameMinutes, 1, "Le temps de son arrivée passe.");
            Assert.AreEqual(FuelGauge.TechnicianExpenseLabel, shift.Ledger.Expenses.Single().Label);
            Assert.AreEqual(6200, shift.Ledger.ExpensesTotal);
            Assert.AreEqual(0, shift.Ledger.FuelCost, "Une seule ligne au bilan : son prix, carburant compris.");
            Assert.IsFalse(street.CallTechnician(out _), "Plus de panne : pas de technicien.");
            Assert.AreEqual(6200, shift.Ledger.ExpensesTotal);
        }

        [Test]
        public void DryTank_TechnicianArrivesAfterTheEnd_EmptyTaxi_DayEnds()
        {
            var street = NewStreet(Fuel(), 0f);
            var day = new DayConfig();
            street.Shift.Clock.Advance(day.dayLengthRealSeconds * 0.98f);
            Assert.IsTrue(street.DryTankChoice(At(300f)));
            Assert.IsTrue(street.CallTechnician(out _));
            Assert.IsTrue(street.DayFinished);
            Assert.AreEqual(6200, street.Summary.ExpensesTotal);
            Assert.IsFalse(street.DryTankChoice(At(300f)), "Journée finie : plus de choix.");
        }

        [Test]
        public void DryTank_Texts()
        {
            var quote = new TechnicianCall { Litres = 5f, Cost = 6200, DelayMinutes = 45 };
            Assert.AreEqual("Appeler un technicien (6 200 FCFA, 45 min)", FuelGauge.TechnicianLabel(quote));
            Assert.AreEqual("Le technicien est arrivé après 45 min : +5,0 L, 6 200 FCFA. On repart !", FuelGauge.TechnicianCame(quote));
            StringAssert.Contains("technicien", FuelGauge.EmptyMessage);
            StringAssert.Contains("pousse", FuelGauge.EmptyMessage);
        }

        [Test]
        public void SpeedCap_AlsoHoldsInReverse()
        {
            var logic = new DriveLogic(new DriveConfig()) { SpeedCapKmh = 0f };
            var brake = new DriveInput { Brake = true };
            for (var i = 0; i < 200; i++) Assert.AreEqual(0f, Math.Max(0f, -logic.Step(brake, 0f, 0.02f).Throttle), 1e-6, "Panne sèche : pas de marche arrière non plus.");
            logic.SpeedCapKmh = float.PositiveInfinity;
            var reversed = false;
            for (var i = 0; i < 200; i++) reversed |= logic.Step(brake, 0f, 0.02f).Throttle < 0f;
            Assert.IsTrue(reversed);
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
