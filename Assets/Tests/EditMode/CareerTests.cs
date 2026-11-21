using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Tests
{
    public class CareerTests
    {
        /// <summary>Bilan d'une journée : recette, distance (carburant), frais imprévus.</summary>
        private static DaySummary Day(int revenue, float meters = 0f, int expenses = 0, bool seized = false)
        {
            var ledger = new DayLedger(new DayConfig());
            if (revenue > 0) ledger.AddFare(revenue);
            ledger.AddDistance(meters);
            ledger.AddExpense("Soins", expenses);
            return DaySummary.From(ledger, 0, 0, seized, seized);
        }

        [Test]
        public void GoodDay_PaysVersement_RestGoesToSavings_NextDay()
        {
            var c = new DriverCareer(new OwnerConfig());
            var close = c.CloseDay(Day(14000, meters: 25000f)); // carburant : 2 L = 1 680 FCFA
            Assert.AreEqual(OwnerMood.Satisfied, close.Mood);
            Assert.AreEqual(10000, close.VersementPaid);
            Assert.AreEqual(0, close.VersementMissing);
            Assert.AreEqual(14000 - 1680 - 10000, c.Savings);
            Assert.AreEqual(2, c.DayNumber);
            c.CloseDay(Day(12000));
            Assert.AreEqual(14000 - 1680 - 10000 + 2000, c.Savings, "L'argent passe d'un jour à l'autre.");
        }

        [TestCase(14000, 25000f, 0)]
        [TestCase(10500, 25000f, 0)]
        [TestCase(10000, 0f, 0)]
        [TestCase(9000, 0f, 0)]
        [TestCase(12000, 10000f, 15000)]
        [TestCase(0, 5000f, 0)]
        public void WithoutSavings_SummaryVerdict_MatchesTheOwner(int revenue, float meters, int expenses)
        {
            // Le bilan (tampon, jauge du HUD) et le propriétaire comptent de la même façon : carburant et frais d'abord.
            var s = Day(revenue, meters, expenses);
            var close = new DriverCareer(new OwnerConfig()).CloseDay(s);
            Assert.AreEqual(close.VersementMissing, s.VersementShortfall);
            Assert.AreEqual(close.VersementMissing == 0, s.VersementReached);
        }

        [Test]
        public void ShortDay_CompletedFromSavings_NoThreat()
        {
            var c = DriverCareer.Restore(new OwnerConfig(), 5, 6000, null, false);
            var close = c.CloseDay(Day(7000));
            Assert.AreEqual(3000, close.TakenFromSavings);
            Assert.AreEqual(10000, close.VersementPaid);
            Assert.AreEqual(OwnerMood.Satisfied, close.Mood);
            Assert.AreEqual(3000, c.Savings);
        }

        [Test]
        public void NotEnoughEvenWithSavings_Threat_ThenFired_AfterThreeMissesInAWeek()
        {
            var c = new DriverCareer(new OwnerConfig());
            var d1 = c.CloseDay(Day(4000));
            Assert.AreEqual(OwnerMood.Threatens, d1.Mood);
            Assert.AreEqual(4000, d1.VersementPaid);
            Assert.AreEqual(6000, d1.VersementMissing);
            Assert.AreEqual(0, c.Savings, "Tout part au propriétaire.");
            Assert.AreEqual(1, d1.RecentMisses);
            c.CloseDay(Day(15000));                       // jour 2 : payé
            var d3 = c.CloseDay(Day(3000));
            Assert.AreEqual(OwnerMood.Threatens, d3.Mood);
            Assert.AreEqual(2, d3.RecentMisses);
            var d4 = c.CloseDay(Day(2000));
            Assert.AreEqual(OwnerMood.Fired, d4.Mood);
            Assert.IsTrue(c.Fired);
            Assert.Throws<System.InvalidOperationException>(() => c.CloseDay(Day(20000)));
        }

        [Test]
        public void OldMisses_AreForgotten_AfterTheWindow()
        {
            var c = new DriverCareer(new OwnerConfig { missesBeforeFired = 3, windowDays = 7, payShortfallFromSavings = false });
            c.CloseDay(Day(0));                                         // jour 1 : manqué
            c.CloseDay(Day(0));                                         // jour 2 : manqué
            for (var i = 0; i < 6; i++) c.CloseDay(Day(30000));          // jours 3 à 8 : payés
            var d9 = c.CloseDay(Day(0));                                // jour 9 : jours 1 et 2 hors de la fenêtre
            Assert.AreEqual(OwnerMood.Threatens, d9.Mood);
            Assert.AreEqual(1, d9.RecentMisses);
            Assert.IsFalse(c.Fired);
        }

        [Test]
        public void CostsBeyondRevenue_ComeOutOfSavings_CanGoNegative()
        {
            var c = DriverCareer.Restore(new OwnerConfig(), 3, 5000, null, false);
            var close = c.CloseDay(Day(10000, expenses: 18000));
            // Après les soins, rien pour le versement depuis la recette ; l'épargne paie d'abord les soins (8 000 de trop),
            // puis il ne reste rien pour compléter le versement.
            Assert.AreEqual(0, close.TakenFromSavings);
            Assert.AreEqual(10000, close.VersementMissing);
            Assert.AreEqual(5000 + 10000 - 18000, c.Savings);
            Assert.AreEqual(OwnerMood.Threatens, close.Mood);
        }

        [Test]
        public void SavingsNotUsed_WhenRuleOff()
        {
            var c = DriverCareer.Restore(new OwnerConfig { payShortfallFromSavings = false }, 1, 50000, null, false);
            var close = c.CloseDay(Day(7000));
            Assert.AreEqual(7000, close.VersementPaid);
            Assert.AreEqual(OwnerMood.Threatens, close.Mood);
            Assert.AreEqual(50000, c.Savings);
        }

        [Test]
        public void SeizedTaxi_OwnerFiresTheDriver()
        {
            var c = new DriverCareer(new OwnerConfig());
            Assert.AreEqual(OwnerMood.Fired, c.CloseDay(Day(20000, seized: true)).Mood);
        }

        [Test]
        public void Restore_KeepsMissedDays()
        {
            var c = DriverCareer.Restore(new OwnerConfig(), 6, 0, new[] { 4, 5 }, false);
            Assert.AreEqual(2, c.MissesInWindow(5));
            Assert.AreEqual(OwnerMood.Fired, c.CloseDay(Day(0)).Mood);
            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, c.MissedDays.ToArray());
        }
    }
}
