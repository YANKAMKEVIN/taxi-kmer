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

        [Test]
        public void Summary_MatchesLedger_AndAddsUp()
        {
            var ledger = new DayLedger(new DayConfig());
            ledger.AddFare(500); ledger.AddFare(400); ledger.AddFare(300);
            ledger.AddDistance(37400f); // 37,4 km → 2,992 L → 2 513,28 → 2 513 FCFA
            var s = DaySummary.From(ledger, 2, 900);
            Assert.AreEqual(1200, s.Revenue);
            Assert.AreEqual(3, s.RideCount);
            Assert.AreEqual(10000, s.Versement);
            Assert.AreEqual(37.4f, s.Kilometres, 1e-3);
            Assert.AreEqual(2.992f, s.FuelLitres, 1e-4);
            Assert.AreEqual(2513, s.FuelCost);
            Assert.AreEqual(1200 - 10000 - 2513, s.Remaining);
            Assert.AreEqual(s.Revenue - s.Versement - s.FuelCost, s.Remaining, "Le bilan s'additionne.");
            Assert.IsFalse(s.VersementReached);
            Assert.AreEqual(8800, s.VersementShortfall);
            Assert.AreEqual(2, s.UnpaidPassengers);
            Assert.AreEqual(900, s.UnpaidFares);
        }

        [Test]
        public void Summary_IsASnapshot()
        {
            var ledger = new DayLedger(new DayConfig());
            for (var i = 0; i < 30; i++) ledger.AddFare(400);
            var s = DaySummary.From(ledger);
            ledger.AddFare(800);
            Assert.AreEqual(12000, s.Revenue);
            Assert.IsTrue(s.VersementReached);
            Assert.AreEqual(0, s.VersementShortfall);
            Assert.AreEqual(2000, s.Remaining);
        }

        [TestCase(0, "0 FCFA")]
        [TestCase(300, "300 FCFA")]
        [TestCase(1200, "1 200 FCFA")]
        [TestCase(10000, "10 000 FCFA")]
        [TestCase(1234567, "1 234 567 FCFA")]
        [TestCase(-8800, "-8 800 FCFA")]
        public void Money_FrenchFormat(int amount, string expected) => Assert.AreEqual(expected, DaySummary.Money(amount));

        [TestCase(2.992f, "3,0")]
        [TestCase(37.45f, "37,5")]
        [TestCase(0f, "0,0")]
        [TestCase(0.04f, "0,0")]
        public void OneDecimal_FrenchFormat(float v, string expected) => Assert.AreEqual(expected, DaySummary.OneDecimal(v));
    }
}
