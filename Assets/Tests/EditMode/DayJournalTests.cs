using NUnit.Framework;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Tests
{
    public class DayJournalTests
    {
        private static JournalDayData Day(int n, bool reached) => new JournalDayData { dayNumber = n, versementReached = reached };

        [Test]
        public void Entry_CopiesTheSummaryAndCounters()
        {
            var ledger = new DayLedger(new DayConfig { versement = 7000 });
            ledger.AddFare(1000);
            ledger.AddFare(800);
            var s = DaySummary.From(ledger);
            var e = DayJournal.Entry(s, 4, "jeudi", policeControls: 2, policePaid: 1500, crashes: 1, tips: 150, savingsAfter: 3200, gameVersion: "0.1.0");
            Assert.AreEqual(4, e.dayNumber);
            Assert.AreEqual(1800, e.revenue);
            Assert.AreEqual(2, e.rideCount);
            Assert.IsFalse(e.versementReached);
            Assert.AreEqual(2, e.policeControls);
            Assert.AreEqual(1500, e.policePaid);
            Assert.AreEqual(150, e.tips);
            Assert.AreEqual("jeudi", e.weekday);
        }

        [Test]
        public void Append_ReplacesTheSameDay_KeepsOrder_AndTheLastDaysOnly()
        {
            var j = new JournalData();
            j = DayJournal.Append(j, Day(2, false));
            j = DayJournal.Append(j, Day(1, true));
            j = DayJournal.Append(j, Day(2, true)); // journée rejouée : remplacée
            Assert.AreEqual(2, j.days.Length);
            Assert.AreEqual(1, j.days[0].dayNumber);
            Assert.IsTrue(j.days[1].versementReached);
            for (var n = 3; n < 200; n++) j = DayJournal.Append(j, Day(n, n % 3 != 0));
            Assert.AreEqual(DayJournal.MaxDays, j.days.Length);
            Assert.AreEqual(199, j.days[j.days.Length - 1].dayNumber);
        }

        [Test]
        public void Verdict_ComparesWithTheBalanceTarget()
        {
            Assert.AreEqual("Aucune journée dans le journal.", DayJournal.Verdict(new JournalData()));
            var j = new JournalData { days = new[] { Day(1, true), Day(2, true), Day(3, false), Day(4, true) } };
            Assert.AreEqual(0.75f, DayJournal.VersementRate(j));
            StringAssert.Contains("dans la cible", DayJournal.Verdict(j));
            j.days[3].versementReached = false;
            StringAssert.Contains("trop dur", DayJournal.Verdict(j));
        }
    }
}
