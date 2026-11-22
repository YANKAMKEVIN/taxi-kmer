using System;
using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class SoundSynthTests
    {
        private static float Peak(float[] d)
        {
            var p = 0f;
            foreach (var v in d) p = Math.Max(p, Math.Abs(v));
            return p;
        }

        [Test]
        public void AllSounds_AudibleAndNeverClip()
        {
            foreach (var d in new[] { SoundSynth.EngineLoop(), SoundSynth.BrakeSqueal(), SoundSynth.DoorSlam(), SoundSynth.Coins(),
                SoundSynth.DistantHorn(), SoundSynth.StreetMurmur(), SoundSynth.MarketChatter(), SoundSynth.ClientCall() })
            {
                Assert.Greater(d.Length, 0);
                Assert.Greater(Peak(d), 0.05f, "Audible.");
                Assert.LessOrEqual(Peak(d), 1f, "Pas de saturation.");
            }
        }

        [Test]
        public void EngineLoop_LastsOneSecond_AndLoopsWithoutAClick()
        {
            var d = SoundSynth.EngineLoop();
            Assert.AreEqual(SoundSynth.Rate, d.Length);
            var typicalStep = 0f;
            for (var i = 1; i < d.Length; i++) typicalStep = Math.Max(typicalStep, Math.Abs(d[i] - d[i - 1]));
            Assert.LessOrEqual(Math.Abs(d[0] - d[d.Length - 1]), typicalStep, "Le raccord fin → début est aussi doux que le reste.");
        }

        [Test]
        public void OneShots_FadeOut()
        {
            foreach (var d in new[] { SoundSynth.BrakeSqueal(), SoundSynth.DoorSlam(), SoundSynth.Coins(), SoundSynth.DistantHorn(), SoundSynth.ClientCall() })
            {
                var tail = 0f;
                for (var i = d.Length - 200; i < d.Length; i++) tail = Math.Max(tail, Math.Abs(d[i]));
                Assert.Less(tail, Peak(d) * 0.35f, "Le son s'éteint avant la fin.");
            }
        }

        [Test]
        public void Synthesis_IsReproducible()
        {
            CollectionAssert.AreEqual(SoundSynth.DoorSlam(), SoundSynth.DoorSlam());
        }

        [Test]
        public void EnginePitch_RisesWithSpeed_AndBrakeSquealNeedsSpeed()
        {
            Assert.AreEqual(0.8f, SoundSynth.EnginePitch(0f, 80f), 1e-6f);
            Assert.AreEqual(2f, SoundSynth.EnginePitch(80f, 80f), 1e-6f);
            Assert.AreEqual(2f, SoundSynth.EnginePitch(120f, 80f), 1e-6f);
            Assert.Less(SoundSynth.EnginePitch(20f, 80f), SoundSynth.EnginePitch(50f, 80f));
            Assert.Less(SoundSynth.EngineVolume(0f), SoundSynth.EngineVolume(1f));
            Assert.IsTrue(SoundSynth.HardBraking(50f, 1f));
            Assert.IsFalse(SoundSynth.HardBraking(10f, 1f), "Trop lent pour crisser.");
            Assert.IsFalse(SoundSynth.HardBraking(50f, 0.3f), "Freinage doux.");
        }
        [Test]
        public void AmbienceLoops_LoopWithoutAClick_AndAreReproducible()
        {
            foreach (var d in new[] { SoundSynth.StreetMurmur(), SoundSynth.MarketChatter() })
            {
                var typicalStep = 0f;
                for (var i = 1; i < d.Length; i++) typicalStep = Math.Max(typicalStep, Math.Abs(d[i] - d[i - 1]));
                Assert.LessOrEqual(Math.Abs(d[0] - d[d.Length - 1]), typicalStep, "Raccord fin → début aussi doux que le reste.");
            }
            Assert.AreEqual(SoundSynth.Rate * 3, SoundSynth.StreetMurmur().Length);
            CollectionAssert.AreEqual(SoundSynth.MarketChatter(), SoundSynth.MarketChatter());
        }
    }
}
