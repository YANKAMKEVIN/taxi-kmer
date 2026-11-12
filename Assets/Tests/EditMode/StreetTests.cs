using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class StreetTests
    {
        private static StopsConfig Stops() => new StopsConfig
        {
            stops = new[]
            {
                new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
            },
        };

        private static Street NewStreet(out StreetConfig cfg, int seed = 7)
        {
            cfg = new StreetConfig();
            var stops = Stops();
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(cfg, stops, new ClientFactory(stops, FareConfig.Default(), seed), shift, 1000f);
        }

        private static TaxiOnRoute At(float along, int dir, float speedKmh = 0f, float lateral = 2f) =>
            new TaxiOnRoute { Along = along, Direction = dir, SpeedKmh = speedKmh, Lateral = lateral };

        [Test]
        public void Start_EachStopHasWaitingClients_FacingTheirDestination()
        {
            var street = NewStreet(out var cfg);
            Assert.AreEqual(3 * cfg.maxWaitingPerStop, street.Waiting.Count);
            foreach (var c in street.Waiting)
            {
                var dest = street.FindStop(c.Client.DestinationStopId);
                Assert.AreEqual(dest.distanceAlongRouteMeters > c.Pickup.distanceAlongRouteMeters ? 1 : -1, c.Direction);
                Assert.That(c.Along, Is.InRange(0f, 1000f));
                Assert.LessOrEqual(System.Math.Abs(c.Along - c.Pickup.distanceAlongRouteMeters), cfg.clientSpacingMeters * cfg.maxWaitingPerStop);
            }
            Assert.IsTrue(street.Waiting.Where(c => c.Pickup.id == "a").All(c => c.Direction == 1), "Au début du trajet, tous vont vers la fin.");
            var positions = street.Waiting.Select(c => (c.Along, c.Direction)).ToList();
            Assert.AreEqual(positions.Count, positions.Distinct().Count(), "Deux clients ne se tiennent pas au même endroit.");
        }

        [Test]
        public void Clients_CallOnlyToATaxiComingTheirWay()
        {
            var street = NewStreet(out var cfg);
            street.Update(0.1f, At(470f, 1));
            foreach (var c in street.Waiting)
            {
                var expected = c.Pickup.id == "b" && c.Direction == 1;
                Assert.AreEqual(expected, c.Calling, $"{c.Client.Id} à {c.Pickup.id}, sens {c.Direction}");
            }

            street.Update(0.1f, At(530f, -1));
            foreach (var c in street.Waiting)
                Assert.AreEqual(c.Pickup.id == "b" && c.Direction == -1, c.Calling);

            street.Update(0.1f, At(470f, 1, lateral: cfg.maxLateralMeters + 5f));
            Assert.IsFalse(street.Waiting.Any(c => c.Calling), "Taxi dans une rue voisine : personne ne l'appelle.");
        }

        [Test]
        public void Honk_WithNobodyCalling_DoesNothing()
        {
            var street = NewStreet(out _);
            street.Update(0.1f, At(250f, 1));
            Assert.AreEqual(HonkResult.NobodyCalling, street.Honk(At(250f, 1), out var c));
            Assert.IsNull(c);
        }

        [Test]
        public void Honk_Stop_Board_Drive_Drop_Pays()
        {
            var street = NewStreet(out _);
            var shift = street.Shift;
            // On arrive au lieu A dans le sens +1 : les clients de A appellent.
            var taxi = At(0f, 1, speedKmh: 20f);
            street.Update(0.1f, taxi);
            Assert.AreEqual(HonkResult.Accepted, street.Honk(taxi, out var client));
            Assert.AreEqual(RoadsideState.Accepted, client.State);

            Assert.AreEqual(StreetActionKind.None, street.AvailableAction(At(client.Along, 1, speedKmh: 20f)).Kind, "Pas en roulant.");
            var stopped = At(client.Along + 3f, 1);
            var action = street.AvailableAction(stopped);
            Assert.AreEqual(StreetActionKind.Board, action.Kind);
            Assert.AreSame(client, action.Client);
            Assert.IsNull(action.MoveFirst);

            var boarded = street.Perform(stopped);
            Assert.AreEqual(BoardResult.Boarded, boarded.Board);
            Assert.AreSame(client.Client, boarded.Boarded);
            Assert.AreEqual(1, shift.Cabin.PassengerCount);
            Assert.IsFalse(street.Waiting.Contains(client));

            var dest = street.FindStop(client.Client.DestinationStopId);
            var there = At(dest.distanceAlongRouteMeters - 10f, 1);
            street.Update(0.1f, there);
            action = street.AvailableAction(there);
            Assert.AreEqual(StreetActionKind.Drop, action.Kind);
            Assert.AreEqual(dest, action.Stop);
            Assert.AreEqual(1, action.Count);
            var dropped = street.Perform(there);
            Assert.AreEqual(client.Client.ProposedFare, dropped.Paid);
            Assert.AreEqual(1, dropped.Dropped.Count);
            Assert.AreEqual(client.Client.ProposedFare, shift.Ledger.Revenue);
            Assert.AreEqual(0, shift.Cabin.PassengerCount);
        }

        [Test]
        public void NoDrop_AtAStopNobodyGoesTo()
        {
            var street = NewStreet(out _);
            var taxi = At(0f, 1);
            street.Update(0.1f, taxi);
            street.Honk(taxi, out var client);
            street.Perform(At(client.Along, 1));
            var other = street.FindStop(client.Client.DestinationStopId).id == "b" ? 1000f : 500f;
            var action = street.AvailableAction(At(other, 1));
            Assert.AreNotEqual(StreetActionKind.Drop, action.Kind);
        }

        [Test]
        public void AcceptedClient_IsReleased_WhenTheTaxiDrivesPast()
        {
            var street = NewStreet(out var cfg);
            var taxi = At(0f, 1, speedKmh: 30f); // au lieu A, tous les clients vont dans le sens +1
            street.Update(0.1f, taxi);
            Assert.AreEqual(HonkResult.Accepted, street.Honk(taxi, out var client));
            street.Update(0.1f, At(client.Along + cfg.missedDistanceMeters - 1f, 1, speedKmh: 30f));
            Assert.AreEqual(RoadsideState.Accepted, client.State, "Pas encore trop loin.");
            street.Update(0.1f, At(client.Along + cfg.missedDistanceMeters + 1f, 1, speedKmh: 30f));
            Assert.AreEqual(RoadsideState.Waiting, client.State);
        }

        [Test]
        public void Honk_WhenTheCabinIsFull_NoRoom()
        {
            var street = NewStreet(out _);
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(BoardResult.Boarded, street.Shift.Board(new Client($"p{i}", "c", 1000, 400, Corpulence.Mince), out _));
            var taxi = At(0f, 1);
            street.Update(0.1f, taxi);
            Assert.AreEqual(HonkResult.NoRoom, street.Honk(taxi, out var client));
            Assert.AreEqual(RoadsideState.Waiting, client.State);
        }

        [Test]
        public void NewClients_ArriveAfterTheInterval()
        {
            var street = NewStreet(out var cfg);
            var taxi = At(0f, 1);
            street.Update(0.1f, taxi);
            street.Honk(taxi, out var client);
            street.Perform(At(client.Along, 1));
            var before = street.Waiting.Count;
            street.Update(cfg.spawnIntervalSeconds * 0.5f, At(200f, 1, 30f));
            Assert.AreEqual(before, street.Waiting.Count);
            street.Update(cfg.spawnIntervalSeconds * 0.6f, At(200f, 1, 30f));
            Assert.AreEqual(before + 1, street.Waiting.Count);
        }

        private static RoadsideClient BoardOne(Street street)
        {
            var taxi = At(0f, 1);
            street.Update(0.1f, taxi);
            street.Honk(taxi, out var client);
            Assert.AreEqual(BoardResult.Boarded, street.Perform(At(client.Along, 1)).Board);
            return client;
        }

        [Test]
        public void EndOfDay_EmptyCabin_FinishesAtOnce_WithSummary()
        {
            var street = NewStreet(out _);
            var shift = street.Shift;
            var client = BoardOne(street);
            var dest = street.FindStop(client.Client.DestinationStopId);
            street.Perform(At(dest.distanceAlongRouteMeters, 1));
            shift.Ledger.AddDistance(12500f);
            Assert.IsFalse(street.DayFinished);

            shift.Clock.Advance(10000f);
            street.Update(0.1f, At(200f, 1, 30f));
            Assert.IsTrue(street.DayFinished);
            var s = street.Summary;
            Assert.AreEqual(client.Client.ProposedFare, s.Revenue);
            Assert.AreEqual(1, s.RideCount);
            Assert.AreEqual(shift.Ledger.FuelCost, s.FuelCost);
            Assert.AreEqual(s.Revenue - s.Versement - s.FuelCost, s.Remaining);
            Assert.AreEqual(0, s.UnpaidPassengers);
            Assert.IsFalse(street.Waiting.Any(c => c.Calling), "Plus personne n'appelle.");
            Assert.AreEqual(StreetActionKind.None, street.AvailableAction(At(client.Pickup.distanceAlongRouteMeters, 1)).Kind);
        }

        [Test]
        public void EndOfDay_WithPassengers_CanStillDropThem_ThenFinishes()
        {
            var street = NewStreet(out _);
            var client = BoardOne(street);
            street.Shift.Clock.Advance(10000f);
            street.Update(0.1f, At(200f, 1, 30f));
            Assert.IsFalse(street.DayFinished, "On laisse le temps de déposer les passagers.");

            var dest = street.FindStop(client.Client.DestinationStopId);
            var there = At(dest.distanceAlongRouteMeters, 1);
            Assert.AreEqual(StreetActionKind.Drop, street.AvailableAction(there).Kind, "La dépose reste possible après l'heure.");
            Assert.AreEqual(client.Client.ProposedFare, street.Perform(there).Paid);
            Assert.IsTrue(street.DayFinished, "Taxi vide après l'heure : la journée se termine.");
            Assert.AreEqual(client.Client.ProposedFare, street.Summary.Revenue);
        }

        [Test]
        public void EndOfDay_PlayerEndsWithPassengersOnBoard_TheyDoNotPay()
        {
            var street = NewStreet(out _);
            var client = BoardOne(street);
            Assert.AreNotEqual(StreetActionKind.EndDay, street.AvailableAction(At(250f, 1)).Kind, "Pas avant l'heure.");
            street.Shift.Clock.Advance(10000f);
            Assert.AreEqual(StreetActionKind.None, street.AvailableAction(At(250f, 1, 20f)).Kind, "Seulement à l'arrêt.");
            var action = street.AvailableAction(At(250f, 1));
            Assert.AreEqual(StreetActionKind.EndDay, action.Kind);
            Assert.AreEqual(1, action.Count);

            street.Perform(At(250f, 1));
            Assert.IsTrue(street.DayFinished);
            Assert.AreEqual(0, street.Summary.Revenue);
            Assert.AreEqual(1, street.Summary.UnpaidPassengers);
            Assert.AreEqual(client.Client.ProposedFare, street.Summary.UnpaidFares);
        }

        [Test]
        public void EndOfDay_NoBoardingAfterTheHour()
        {
            var street = NewStreet(out _);
            var taxi = At(0f, 1);
            street.Update(0.1f, taxi);
            street.Honk(taxi, out var client);
            street.Shift.Clock.Advance(10000f);
            Assert.AreNotEqual(StreetActionKind.Board, street.AvailableAction(At(client.Along, 1)).Kind);
        }
    }
}
