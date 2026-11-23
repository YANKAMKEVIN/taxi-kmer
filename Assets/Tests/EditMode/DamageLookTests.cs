using NUnit.Framework;
using TaxiKmer.Core.Road;

namespace TaxiKmer.Core.Tests
{
    public class DamageLookTests
    {
        [Test]
        public void NewTaxi_ShowsNothing()
        {
            var look = DamageLook.From(new RoadConfig(), 0f);
            Assert.IsFalse(look.Any);
            Assert.IsFalse(DamageLook.From(new RoadConfig(), 29f).Any);
        }

        [Test]
        public void Damage_ShowsBumperThenHeadlightThenSmoke()
        {
            var cfg = new RoadConfig();
            var bumper = DamageLook.From(cfg, 30f);
            Assert.AreEqual(8f, bumper.BumperHangDegrees, 1e-4f);
            Assert.IsFalse(bumper.HeadlightBroken);
            Assert.AreEqual(0f, bumper.Smoke);
            var light = DamageLook.From(cfg, 50f);
            Assert.IsTrue(light.HeadlightBroken);
            Assert.AreEqual(0f, light.Smoke);
            var smoke = DamageLook.From(cfg, 60f);
            Assert.AreEqual(0.3f, smoke.Smoke, 1e-4f, "la fumée arrive avec la limite de vitesse (damagedThreshold)");
            var wreck = DamageLook.From(cfg, 100f);
            Assert.AreEqual(DamageLook.MaxBumperDegrees, wreck.BumperHangDegrees, 1e-4f);
            Assert.AreEqual(1f, wreck.Smoke, 1e-4f);
        }
    }
}
