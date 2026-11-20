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
    }
}
