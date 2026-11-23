using NUnit.Framework;
using TaxiKmer.Core.Quality;

namespace TaxiKmer.Core.Tests
{
    public class TrafficShadowsTests
    {
        [Test]
        public void Shadows_CutAtLowAndMedium_BodiesOnlyAtHigh()
        {
            var levels = QualityConfig.DefaultLevels();
            Assert.AreEqual(TrafficShadows.None, levels[0].trafficShadows, "Bas");
            Assert.AreEqual(TrafficShadows.None, levels[1].trafficShadows, "Moyen");
            Assert.AreEqual(TrafficShadows.Bodies, levels[2].trafficShadows, "Haut");
        }

        [Test]
        public void Casts_FollowsTheMode()
        {
            Assert.IsFalse(TrafficShadows.Casts(TrafficShadows.None, true));
            Assert.IsTrue(TrafficShadows.Casts(TrafficShadows.Bodies, true));
            Assert.IsFalse(TrafficShadows.Casts(TrafficShadows.Bodies, false), "plaques, roues, feux : jamais");
            Assert.IsTrue(TrafficShadows.Casts(TrafficShadows.All, false));
        }
    }
}
