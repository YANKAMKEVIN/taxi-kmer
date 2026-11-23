using NUnit.Framework;
using TaxiKmer.Core.Road;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class RainLookTests
    {
        private static void Run(RainLook rain, float seconds, bool raining)
        {
            for (var t = 0f; t < seconds; t += 0.1f) rain.Update(0.1f, raining);
        }

        [Test]
        public void Shower_FadesIn_SoaksTheRoad_ThenDriesSlowly()
        {
            var rain = new RainLook(new RainLookConfig());
            Run(rain, 3f, true);
            Assert.That(rain.Intensity, Is.InRange(0.4f, 0.6f), "à mi-montée");
            Run(rain, 30f, true);
            Assert.AreEqual(1f, rain.Intensity, 1e-4f);
            Assert.AreEqual(1f, rain.Wetness, 1e-4f);
            Assert.AreEqual(0.75f, rain.RoadSmoothness, 1e-4f);
            Run(rain, 10f, false);
            Assert.AreEqual(0f, rain.Intensity);
            Assert.That(rain.Wetness, Is.InRange(0.85f, 1f), "la route reste mouillée après l'averse");
            Run(rain, 120f, false);
            Assert.AreEqual(0f, rain.Wetness);
            Assert.AreEqual(0.15f, rain.RoadSmoothness, 1e-4f);
        }

        [Test]
        public void Drops_CutAtLowQuality()
        {
            var rain = new RainLook(new RainLookConfig());
            Run(rain, 10f, true);
            Assert.AreEqual(0, rain.DropsPerSecond(0));
            Assert.AreEqual(600, rain.DropsPerSecond(1));
            Assert.AreEqual(1500, rain.DropsPerSecond(7), "niveau borné");
            Assert.AreEqual(0.5f, rain.RoofVolume, 1e-4f);
        }

        [Test]
        public void Wipers_Sweep_InRain_AndRestAfter()
        {
            var rain = new RainLook(new RainLookConfig());
            float min = 1f, max = 0f;
            for (var i = 0; i < 100; i++)
            {
                rain.Update(0.05f, true);
                min = System.Math.Min(min, rain.WiperPosition);
                max = System.Math.Max(max, rain.WiperPosition);
            }
            Assert.Less(min, 0.2f);
            Assert.Greater(max, 0.8f);
            Run(rain, 20f, false);
            Assert.AreEqual(0f, rain.WiperPosition);
        }

        [Test]
        public void Disabled_ShowsNothing()
        {
            var rain = new RainLook(new RainLookConfig { enabled = false });
            Run(rain, 30f, true);
            Assert.AreEqual(0f, rain.Intensity);
            Assert.AreEqual(0, rain.DropsPerSecond(2));
        }

        [Test]
        public void RoofSound_IsALoopInRange()
        {
            var data = SoundSynth.RainOnRoof();
            Assert.AreEqual(SoundSynth.Rate * 2, data.Length);
            foreach (var v in data) Assert.That(v, Is.InRange(-1f, 1f));
        }
    }
}
