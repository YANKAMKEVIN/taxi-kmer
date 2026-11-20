using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class HapticPatternsTests
    {
        private static int Pulses(HapticEvent e)
        {
            HapticPatterns.For(e, out var timings, out var amps);
            var n = 0;
            for (var i = 0; i < timings.Length; i++) if (amps[i] > 0 && timings[i] > 0) n++;
            return n;
        }

        private static int MaxAmp(HapticEvent e)
        {
            HapticPatterns.For(e, out _, out var amps);
            var m = 0;
            foreach (var a in amps) m = System.Math.Max(m, a);
            return m;
        }

        [Test]
        public void Patterns_AreValidWaveforms()
        {
            foreach (HapticEvent e in System.Enum.GetValues(typeof(HapticEvent)))
            {
                HapticPatterns.For(e, out var timings, out var amps);
                Assert.AreEqual(timings.Length, amps.Length, e.ToString());
                Assert.AreEqual(0, amps[0], "Le motif commence par une pause (Android).");
                foreach (var a in amps) Assert.That(a, Is.InRange(0, HapticPatterns.MaxAmplitude));
                Assert.Less(HapticPatterns.TotalMilliseconds(e), 300, "Retour bref, jamais une longue vibration.");
            }
        }

        [Test]
        public void Payment_IsDouble_RefusalIsTheStrongest()
        {
            Assert.AreEqual(1, Pulses(HapticEvent.Honk));
            Assert.AreEqual(1, Pulses(HapticEvent.Board));
            Assert.AreEqual(2, Pulses(HapticEvent.Payment));
            Assert.AreEqual(HapticPatterns.MaxAmplitude, MaxAmp(HapticEvent.Refused));
            Assert.Less(MaxAmp(HapticEvent.Honk), MaxAmp(HapticEvent.Refused));
            Assert.Less(HapticPatterns.TotalMilliseconds(HapticEvent.Honk), HapticPatterns.TotalMilliseconds(HapticEvent.Refused));
        }

        [Test]
        public void VibrationSetting_DefaultsOn()
        {
            var s = new GameSettings();
            Assert.IsTrue(s.Vibration);
            Assert.AreEqual("Vibrations : oui", s.VibrationLabel);
            s.Vibration = false;
            Assert.AreEqual("Vibrations : non", s.VibrationLabel);
        }
    }
}
