using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class SummaryLinesTests
    {
        [Test]
        public void NoRide_ButCompensation_IsNotShownAsRideRevenue()
        {
            // Vu par le parcours automatique (27/11) : « Recettes (0 courses) + 41 500 FCFA » après deux accrochages.
            var ledger = new DayLedger(new DayConfig { versement = 6000 });
            ledger.AddCompensation(22000);
            ledger.AddTip(500);
            var lines = SummaryLines.Build(DaySummary.From(ledger), new DayClosure());
            Assert.AreEqual("Courses (0 courses)", lines[0].Label);
            Assert.AreEqual(0, lines[0].Amount);
            Assert.AreEqual(500, lines.Single(l => l.Label == "Pourboires").Amount);
            Assert.AreEqual(22000, lines.Single(l => l.Label == "Dédommagements reçus").Amount);
            Assert.AreEqual(ledger.Revenue, lines.Where(l => l.Income).Sum(l => l.Amount), "Les lignes de recette font la recette");
        }

        [Test]
        public void Rides_Only_NoExtraLines_AndExpensesAreCosts()
        {
            var ledger = new DayLedger(new DayConfig { versement = 6000 });
            ledger.AddFare(800);
            ledger.AddExpense("Garage", 1500);
            var lines = SummaryLines.Build(DaySummary.From(ledger), new DayClosure { VersementPaid = 800, VersementMissing = 5200 });
            CollectionAssert.AreEqual(new[] { "Courses (1 course)", "Versement (incomplet)", "Carburant (0,0 L · 0,0 km)", "Garage" },
                lines.Select(l => l.Label).ToArray());
            CollectionAssert.AreEqual(new[] { "+ ", "- ", "- ", "- " }, lines.Select(l => l.Sign).ToArray());
        }

        [Test]
        public void Savings_DoesNotRepeatTheTotal()
        {
            Assert.AreEqual("Épargne : -3 500 FCFA", SummaryLines.Savings(-3500, -3500));
            Assert.AreEqual("Épargne : 12 000 FCFA (+ 3 000 FCFA)", SummaryLines.Savings(12000, 3000));
            Assert.AreEqual("Épargne : 2 000 FCFA (- 1 000 FCFA)", SummaryLines.Savings(2000, -1000));
        }

        [Test]
        public void Ledger_RestoreExtras_KeepsTheSplit()
        {
            var ledger = new DayLedger(new DayConfig());
            ledger.Restore(3000, 2, 0f, null);
            ledger.RestoreExtras(500, 9999);
            Assert.AreEqual(500, ledger.Tips);
            Assert.AreEqual(2500, ledger.Compensations, "Bornés par la recette");
            Assert.AreEqual(0, ledger.FaresRevenue);
        }
    }
}
