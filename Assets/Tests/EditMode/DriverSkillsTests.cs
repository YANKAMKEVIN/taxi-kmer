using NUnit.Framework;
using TaxiKmer.Core.Career;

namespace TaxiKmer.Core.Tests
{
    public class DriverSkillsTests
    {
        private static SkillsConfig On() => new SkillsConfig { enabled = true };

        [Test]
        public void Off_ByDefault_NoEffectNoProgress()
        {
            var s = new DriverSkills(new SkillsConfig());
            CollectionAssert.IsEmpty(s.EndDay(new SkillDay { Negotiations = 50, FullDay = true, Kilometres = 500f }));
            Assert.AreEqual(0.3f, s.HaggleDiscount(0.3f));
            Assert.AreEqual(1f, s.PatienceFactor);
            Assert.AreEqual("", s.Summary());
        }

        [Test]
        public void Skills_GrowWithTheDays_AndSoftenDisputes()
        {
            var s = new DriverSkills(On());
            var up = s.EndDay(new SkillDay { Negotiations = 6, FullDay = true, Kilometres = 23f });
            CollectionAssert.AreEqual(new[] { "Négociation" }, up);
            Assert.AreEqual(1, s.Level(DriverSkills.Negotiation));
            Assert.AreEqual(0.3f * 0.96f, s.HaggleDiscount(0.3f), 1e-5f);
            Assert.AreEqual(0.5f * 0.96f, s.PoliceNegotiateFail(0.5f), 1e-5f);
            s.EndDay(new SkillDay { FullDay = true, Kilometres = 23f });
            s.EndDay(new SkillDay { FullDay = true, Kilometres = 23f });
            Assert.AreEqual(1, s.Level(DriverSkills.Endurance));
            Assert.AreEqual(1.02f, s.PatienceFactor, 1e-5f);
            Assert.AreEqual(1, s.Level(DriverSkills.Shortcuts), "69 km");
            Assert.AreEqual("Négociation niv. 1 · Endurance niv. 1 · Raccourcis niv. 1", s.Summary());
        }

        [Test]
        public void Levels_AreCapped_AndSurviveTheSave()
        {
            var s = new DriverSkills(On());
            for (var d = 0; d < 100; d++) s.EndDay(new SkillDay { Negotiations = 5, FullDay = true, Kilometres = 30f });
            Assert.AreEqual(10, s.Level(DriverSkills.Negotiation));
            Assert.AreEqual(0.6f * 0.6f, s.HaggleDiscount(0.6f), 1e-5f, "au plus 40 % de rabais en moins");
            var back = new DriverSkills(On(), s.Save());
            Assert.AreEqual(s.Points(DriverSkills.Shortcuts), back.Points(DriverSkills.Shortcuts));
            Assert.AreEqual(0, new DriverSkills(On(), new[] { "inconnu:50", "abîmé" }).Points("inconnu"));
        }
    }
}
