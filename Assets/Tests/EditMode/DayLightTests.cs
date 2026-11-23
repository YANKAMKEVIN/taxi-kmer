using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class DayLightTests
    {
        private static readonly DayLight Light = new DayLight(new DayLightConfig());

        [Test]
        public void Noon_IsFullDay_HighSun_NoLamps()
        {
            var s = Light.At(12 * 60);
            Assert.AreEqual(0f, s.Night, 1e-4f);
            Assert.Greater(s.ElevationDegrees, 70f);
            Assert.AreEqual(1.1f, s.Intensity, 1e-4f);
            Assert.IsFalse(s.LampsOn);
        }

        [Test]
        public void Night_IsDim_WithLampsOn()
        {
            var s = Light.At(19 * 60 + 30);
            Assert.AreEqual(1f, s.Night, 1e-4f);
            Assert.AreEqual(0.12f, s.Intensity, 1e-4f);
            Assert.IsTrue(s.LampsOn);
            Assert.IsTrue(Light.LampsOn(6 * 60), "encore allumés à 06:00");
            Assert.IsFalse(Light.LampsOn(7 * 60));
        }

        [Test]
        public void Twilight_IsBetweenDayAndNight_AndMonotonous()
        {
            var previous = 1f;
            for (var m = 17 * 60; m <= 19 * 60; m += 5)
            {
                var d = Light.Daylight(m);
                Assert.LessOrEqual(d, previous + 1e-6f, $"{m} min");
                previous = d;
            }
            var dusk = Light.Daylight(18 * 60 + 15);
            Assert.That(dusk, Is.InRange(0.3f, 0.7f), "au coucher, à mi-chemin");
        }

        [Test]
        public void Sun_RisesInTheEast_AndSetsInTheWest()
        {
            Assert.AreEqual(90f, Light.At(6 * 60).HeadingDegrees, 1f);
            Assert.AreEqual(270f, Light.At(18 * 60 + 15).HeadingDegrees, 1f);
        }

        [Test]
        public void Colors_AreInterpolatedBetweenKeys()
        {
            var s = Light.At(17 * 60 + 15); // entre 16:30 et 18:00
            Assert.AreEqual((0.95f + 0.55f) / 2f, s.Sun[1], 1e-3f);
            Assert.AreEqual(new[] { 0.15f, 0.17f, 0.25f }, Light.At(20 * 60).Fog);
        }

        [Test]
        public void Parse_ReadsHoursAndRejectsGarbage()
        {
            Assert.AreEqual(1095f, DayLight.Parse("18:15", 0f));
            Assert.AreEqual(7f, DayLight.Parse("25:00", 7f));
            Assert.AreEqual(7f, DayLight.Parse("abc", 7f));
            Assert.Throws<System.ArgumentException>(() => new DayLight(new DayLightConfig { sunrise = "19:00" }));
        }
    }
}
