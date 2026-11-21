using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class HudGaugesTests
    {
        [Test]
        public void DayLeft_DrainsFromOneToZero()
        {
            var day = new DayConfig();
            var clock = new DayClock(day);
            Assert.AreEqual(1f, HudGauges.DayLeft(clock), 1e-6f);
            clock.Advance(day.dayLengthRealSeconds * 0.25f);
            Assert.AreEqual(0.75f, HudGauges.DayLeft(clock), 1e-6f);
            clock.Advance(day.dayLengthRealSeconds);
            Assert.AreEqual(0f, HudGauges.DayLeft(clock), 1e-6f);
        }

        [Test]
        public void VersementGauge_FillsThenTurnsGreenWithTheExtra()
        {
            Assert.AreEqual(0f, HudGauges.VersementFill(0, 10000));
            Assert.AreEqual(0.35f, HudGauges.VersementFill(3500, 10000), 1e-6f);
            Assert.AreEqual(1f, HudGauges.VersementFill(11500, 10000));
            Assert.AreEqual("Versement 10 000", HudGauges.VersementCaption(3500, 10000));
            Assert.IsFalse(HudGauges.VersementCovered(9999, 10000));
            Assert.IsTrue(HudGauges.VersementCovered(10000, 10000));
            Assert.AreEqual("Versement couvert", HudGauges.VersementCaption(10000, 10000));
            Assert.AreEqual("Versement couvert · +1 500 au-delà", HudGauges.VersementCaption(11500, 10000));
        }
    
        [Test]
        public void VersementGauge_DeductsFuelAndExpenses_LikeTheSummary()
        {
            // 11 000 de recette, 4 000 de carburant acheté et 0 de frais : 7 000 comptent pour le versement.
            var toward = HudGauges.TowardVersement(11000, 4000, 0);
            Assert.AreEqual(7000, toward);
            Assert.IsFalse(HudGauges.VersementCovered(toward, 10000));
            Assert.AreEqual(0.7f, HudGauges.VersementFill(toward, 10000), 1e-6f);
            Assert.AreEqual("Versement 10 000 · -4 000 dépensés", HudGauges.VersementCaption(toward, 10000, 4000));
            Assert.AreEqual("Versement couvert · +500 au-delà", HudGauges.VersementCaption(HudGauges.TowardVersement(14500, 4000, 0), 10000, 4000));
            Assert.AreEqual(0f, HudGauges.VersementFill(HudGauges.TowardVersement(1000, 2000, 15000), 10000), "Jamais négative.");

            // Même verdict que le bilan de fin de journée.
            var ledger = new DayLedger(new DayConfig()) { UsesFuelPurchases = true };
            ledger.AddFare(11000);
            ledger.BuyFuel(4.76f, 4000);
            var s = DaySummary.From(ledger);
            Assert.AreEqual(toward, s.TowardVersement);
            Assert.IsFalse(s.VersementReached);
            Assert.AreEqual(3000, s.VersementShortfall);
            Assert.AreEqual("IL MANQUE 3 000 FCFA", SummaryReveal.StampText(s));
        }

        [Test]
        public void LastHour_StartsAt19h_AndStopsAtTheEnd()
        {
            var day = new DayConfig(); // 6 h → 20 h en dayLengthRealSeconds : 1 h de jeu = durée / 14
            var len = day.dayLengthRealSeconds;
            var clock = new DayClock(day);
            Assert.AreEqual(14 * 60, clock.MinutesLeft);
            Assert.IsFalse(HudGauges.LastHour(clock));
            clock.Advance(len * 12.9f / 14f);
            Assert.IsFalse(HudGauges.LastHour(clock), "18 h 54 : pas encore.");
            clock.Advance(len * 0.2f / 14f);
            Assert.IsTrue(HudGauges.LastHour(clock), "19 h 06 : dernière heure.");
            clock.Advance(len);
            Assert.AreEqual(0, clock.MinutesLeft);
            Assert.IsFalse(HudGauges.LastHour(clock), "Après 20 h, c'est le rappel de fin qui prend le relais.");
        }

        [Test]
        public void EndOfDayReminder_StaysWhilePassengersAreOnBoard()
        {
            var day = new DayConfig();
            var clock = new DayClock(day);
            Assert.AreEqual("", HudGauges.EndOfDayReminder(clock, 2), "Avant 20 h, pas de rappel.");
            clock.Advance(day.dayLengthRealSeconds);
            Assert.AreEqual("Journée finie : dépose ton passager, ou arrête-toi pour terminer", HudGauges.EndOfDayReminder(clock, 1));
            StringAssert.Contains("tes 3 passagers", HudGauges.EndOfDayReminder(clock, 3));
            Assert.AreEqual("", HudGauges.EndOfDayReminder(clock, 0));
        }
    }
}
