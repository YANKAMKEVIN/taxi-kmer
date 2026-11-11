using NUnit.Framework;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Tests
{
    public class DayTests
    {
        [Test]
        public void Clock_RunsFrom6To20InTenMinutes()
        {
            var clock = new DayClock(new DayConfig());
            Assert.AreEqual("06:00", clock.Display);
            clock.Advance(300f);
            Assert.AreEqual("13:00", clock.Display);
            Assert.IsFalse(clock.IsOver);
            clock.Advance(300f);
            Assert.AreEqual("20:00", clock.Display);
            Assert.IsTrue(clock.IsOver);
            clock.Advance(100f);
            Assert.AreEqual("20:00", clock.Display, "L'horloge s'arrête à la fin de la journée.");
        }

        [Test]
        public void Ledger_RemainingAfterVersementAndFuel()
        {
            var ledger = new DayLedger(new DayConfig());
            for (var i = 0; i < 40; i++) ledger.AddFare(300);
            ledger.AddDistance(50000f); // 50 km → 4 L → 3 360 FCFA
            Assert.AreEqual(12000, ledger.Revenue);
            Assert.AreEqual(40, ledger.RideCount);
            Assert.AreEqual(3360, ledger.FuelCost);
            Assert.AreEqual(12000 - 10000 - 3360, ledger.Remaining);
        }

        [Test]
        public void Ledger_CanBeNegative()
        {
            var ledger = new DayLedger(new DayConfig());
            ledger.AddFare(500);
            Assert.Less(ledger.Remaining, 0);
        }
    }
}
