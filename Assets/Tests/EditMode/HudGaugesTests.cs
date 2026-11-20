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
            var clock = new DayClock(new DayConfig());
            Assert.AreEqual(1f, HudGauges.DayLeft(clock), 1e-6f);
            clock.Advance(150f);
            Assert.AreEqual(0.75f, HudGauges.DayLeft(clock), 1e-6f);
            clock.Advance(1000f);
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
        public void LastHour_StartsAt19h_AndStopsAtTheEnd()
        {
            var clock = new DayClock(new DayConfig()); // 6 h → 20 h en 600 s : 1 h de jeu = 600 / 14 s
            Assert.AreEqual(14 * 60, clock.MinutesLeft);
            Assert.IsFalse(HudGauges.LastHour(clock));
            clock.Advance(600f * 12.9f / 14f);
            Assert.IsFalse(HudGauges.LastHour(clock), "18 h 54 : pas encore.");
            clock.Advance(600f * 0.2f / 14f);
            Assert.IsTrue(HudGauges.LastHour(clock), "19 h 06 : dernière heure.");
            clock.Advance(1000f);
            Assert.AreEqual(0, clock.MinutesLeft);
            Assert.IsFalse(HudGauges.LastHour(clock), "Après 20 h, c'est le rappel de fin qui prend le relais.");
        }

        [Test]
        public void EndOfDayReminder_StaysWhilePassengersAreOnBoard()
        {
            var clock = new DayClock(new DayConfig());
            Assert.AreEqual("", HudGauges.EndOfDayReminder(clock, 2), "Avant 20 h, pas de rappel.");
            clock.Advance(600f);
            Assert.AreEqual("Journée finie : dépose ton passager, ou arrête-toi pour terminer", HudGauges.EndOfDayReminder(clock, 1));
            StringAssert.Contains("tes 3 passagers", HudGauges.EndOfDayReminder(clock, 3));
            Assert.AreEqual("", HudGauges.EndOfDayReminder(clock, 0));
        }
    }
}
