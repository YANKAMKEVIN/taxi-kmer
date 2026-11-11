using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class ShiftTests
    {
        private Shift _shift;

        [SetUp]
        public void SetUp() => _shift = new Shift(new DayConfig(), new SeatingConfig());

        [Test]
        public void BoardThenDropAtDestination_Pays()
        {
            var c = new Client("a", "emana", 3000, 400, Corpulence.Normal);
            Assert.AreEqual(BoardResult.Boarded, _shift.Board(c, out _));
            Assert.AreEqual(DropResult.WrongStop, _shift.Drop("a", "messassi-carrefour"));
            Assert.AreEqual(0, _shift.Ledger.Revenue);
            Assert.AreEqual(DropResult.Paid, _shift.Drop("a", "emana"));
            Assert.AreEqual(400, _shift.Ledger.Revenue);
            Assert.AreEqual(0, _shift.Cabin.PassengerCount);
            Assert.AreEqual(DropResult.NotOnBoard, _shift.Drop("a", "emana"));
        }

        [Test]
        public void DropAllAt_PaysOnlyThoseArrived()
        {
            _shift.Board(new Client("a", "emana", 3000, 400, Corpulence.Normal), out _);
            _shift.Board(new Client("b", "emana", 3000, 300, Corpulence.Mince), out _);
            _shift.Board(new Client("c", "dispensaire", 1000, 300, Corpulence.Costaud), out _);
            Assert.AreEqual(700, _shift.DropAllAt("emana"));
            Assert.AreEqual(1, _shift.Cabin.PassengerCount);
            Assert.AreEqual(2, _shift.Ledger.RideCount);
        }

        [Test]
        public void SeatFreedAfterDrop()
        {
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(BoardResult.Boarded, _shift.Board(new Client("p" + i, "emana", 1000, 300, Corpulence.Normal), out _));
            var extra = new Client("x", "emana", 1000, 300, Corpulence.Normal);
            Assert.AreEqual(BoardResult.NoRoom, _shift.Board(extra, out _));
            _shift.Drop("p0", "emana");
            Assert.AreEqual(BoardResult.Boarded, _shift.Board(extra, out _));
        }

        [Test]
        public void NoBoardingAfterDayEnds()
        {
            _shift.Clock.Advance(600f);
            Assert.AreEqual(BoardResult.DayOver, _shift.Board(new Client("a", "emana", 1000, 300, Corpulence.Normal), out _));
        }
    }
}
