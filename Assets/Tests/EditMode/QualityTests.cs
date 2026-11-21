using NUnit.Framework;
using TaxiKmer.Core.Quality;

namespace TaxiKmer.Core.Tests
{
    public class QualityTests
    {
        [Test]
        public void Default_ThreeLevels_BasMoyenHaut_MoyenFirst()
        {
            var cfg = new QualityConfig();
            Assert.AreEqual(3, cfg.levels.Length);
            Assert.AreEqual("Bas", cfg.levels[0].name);
            Assert.AreEqual("Moyen", cfg.levels[1].name);
            Assert.AreEqual("Haut", cfg.levels[2].name);
            Assert.AreEqual("Moyen", cfg.Level(cfg.Resolve(-1)).name, "Rien d'enregistré : Moyen.");
        }

        [Test]
        public void Default_LevelsIncrease_AndFogStaysInsideDrawDistance()
        {
            var cfg = new QualityConfig();
            for (var i = 0; i < cfg.levels.Length; i++)
            {
                var l = cfg.levels[i];
                Assert.Less(l.fogStart, l.fogEnd, l.name);
                Assert.LessOrEqual(l.fogEnd, l.drawDistance, l.name + " : le brouillard cache le bord de la distance d'affichage.");
                if (i == 0) continue;
                Assert.Greater(l.drawDistance, cfg.levels[i - 1].drawDistance, l.name);
                Assert.GreaterOrEqual(l.targetFrameRate, cfg.levels[i - 1].targetFrameRate, l.name);
            }
            Assert.AreEqual(30, cfg.levels[1].targetFrameRate, "Critère : Moyen à 30 images par seconde.");
            Assert.AreEqual(60, cfg.levels[2].targetFrameRate, "Critère : Haut à 60 images par seconde.");
        }

        [Test]
        public void Resolve_KeepsValidSavedChoice_ElseDefault()
        {
            var cfg = new QualityConfig();
            Assert.AreEqual(0, cfg.Resolve(0));
            Assert.AreEqual(2, cfg.Resolve(2));
            Assert.AreEqual(1, cfg.Resolve(3), "Choix enregistré hors bornes (niveau supprimé) : défaut.");
            cfg.defaultLevel = 9;
            Assert.AreEqual(2, cfg.Resolve(-1), "Défaut hors bornes : ramené au dernier niveau.");
        }

        [Test]
        public void Next_Cycles()
        {
            var cfg = new QualityConfig();
            Assert.AreEqual(1, cfg.Next(0));
            Assert.AreEqual(2, cfg.Next(1));
            Assert.AreEqual(0, cfg.Next(2));
        }

        [Test]
        public void EmptyLevels_DoNotThrow()
        {
            var cfg = new QualityConfig { levels = new QualityLevel[0] };
            Assert.AreEqual(0, cfg.Resolve(1));
            Assert.AreEqual(0, cfg.Next(0));
        }

        [Test]
        public void FrameRateMeter_AveragesOverWindow()
        {
            var m = new FrameRateMeter(1f);
            for (var i = 0; i < 29; i++) m.Tick(1f / 30f);
            Assert.AreEqual(0f, m.FramesPerSecond, "Pas encore une seconde.");
            m.Tick(1f / 30f);
            Assert.AreEqual(30f, m.FramesPerSecond, 0.5f);
            for (var i = 0; i < 60; i++) m.Tick(1f / 60f);
            Assert.AreEqual(60f, m.FramesPerSecond, 0.5f);
            m.Tick(0f);
            m.Reset();
            Assert.AreEqual(0f, m.FramesPerSecond);
        }
    }
}
