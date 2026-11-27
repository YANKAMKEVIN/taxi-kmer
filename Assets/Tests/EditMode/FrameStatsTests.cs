using NUnit.Framework;
using TaxiKmer.Core.Quality;

namespace TaxiKmer.Core.Tests
{
    public class FrameStatsTests
    {
        [Test]
        public void Percentiles_Average_AndLateFrames()
        {
            var stats = new FrameStats();
            for (var i = 0; i < 95; i++) stats.Add(1f / 30f);
            for (var i = 0; i < 5; i++) stats.Add(0.1f);
            stats.Add(0f); // ignorée
            Assert.AreEqual(100, stats.Count);
            Assert.AreEqual(33.33f, stats.PercentileMs(50), 0.01f);
            Assert.AreEqual(33.33f, stats.PercentileMs(95), 0.01f);
            Assert.AreEqual(100f, stats.PercentileMs(99), 0.01f);
            Assert.AreEqual(0.05f, stats.LateShare(30), 1e-4f, "Les 5 images de 100 ms dépassent le budget de 33 ms (+10 %)");
            Assert.AreEqual(100f * 1000f / (95f * 33.333f + 500f), stats.AverageFps, 0.05f);
        }

        [Test]
        public void Empty_IsZero()
        {
            var stats = new FrameStats();
            Assert.AreEqual(0f, stats.PercentileMs(50));
            Assert.AreEqual(0f, stats.AverageFps);
            Assert.AreEqual(0f, stats.LateShare(30));
        }
    }
}
