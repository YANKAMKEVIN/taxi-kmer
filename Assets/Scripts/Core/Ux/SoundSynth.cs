using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Sons provisoires du taxi synthétisés par le code (comme le klaxon), en attendant de vrais enregistrements :
    /// échantillons mono entre -1 et 1. Bruit pseudo-aléatoire à graine fixe : les sons sont identiques d'une partie à
    /// l'autre et testables. La présentation en fait des AudioClip (Driving/TaxiSounds).
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 22050;

        /// <summary>
        /// Ronflement de moteur bouclable d'une seconde : harmoniques entières de 40 Hz (la boucle se raccorde sans
        /// clic), avec des « explosions » marquées sur la fondamentale. La hauteur suit la vitesse via AudioSource.pitch.
        /// </summary>
        public static float[] EngineLoop()
        {
            var data = new float[Rate];
            for (var i = 0; i < data.Length; i++)
            {
                var t = i / (float)Rate;
                double s = 0;
                for (var k = 1; k <= 8; k++) s += Math.Sin(2 * Math.PI * 40 * k * t + k * 0.7) / (k * 0.9);
                var pulse = 0.6 + 0.4 * Math.Max(0, Math.Sin(2 * Math.PI * 40 * t));
                data[i] = (float)(0.32 * Math.Tanh(1.4 * s * pulse));
            }
            return data;
        }

        /// <summary>Crissement léger de pneus (0,6 s) : deux tons aigus qui vibrent, montée rapide, extinction douce.</summary>
        public static float[] BrakeSqueal()
        {
            var n = (int)(Rate * 0.6f);
            var data = new float[n];
            var rng = new Lcg(11);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var env = Math.Min(1.0, t / 0.04) * Math.Exp(-3.5 * t);
                var wobble = 1 + 0.012 * Math.Sin(2 * Math.PI * 23 * t);
                var s = Math.Sin(2 * Math.PI * 2300 * wobble * t) + 0.6 * Math.Sin(2 * Math.PI * 3150 * wobble * t);
                data[i] = (float)(0.22 * env * (s + 0.25 * rng.Next()));
            }
            return data;
        }

        /// <summary>Claquement de portière (0,3 s) : coup sourd grave qui s'éteint vite, et un clic métallique.</summary>
        public static float[] DoorSlam()
        {
            var n = (int)(Rate * 0.3f);
            var data = new float[n];
            var rng = new Lcg(5);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var thud = Math.Sin(2 * Math.PI * (70 + 60 * Math.Exp(-30 * t)) * t) * Math.Exp(-18 * t);
                var click = rng.Next() * Math.Exp(-120 * t);
                var latch = t > 0.035 ? Math.Sin(2 * Math.PI * 1800 * t) * Math.Exp(-90 * (t - 0.035)) * 0.25 : 0;
                data[i] = (float)Math.Max(-1, Math.Min(1, 0.75 * thud + 0.35 * click + latch));
            }
            return data;
        }

        /// <summary>Tintement de pièces (0,5 s) : trois petites notes métalliques décalées de 70 ms.</summary>
        public static float[] Coins()
        {
            var n = (int)(Rate * 0.5f);
            var data = new float[n];
            var notes = new[] { (0.0, 2637.0), (0.07, 3322.0), (0.14, 3951.0) };
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                double s = 0;
                foreach (var (start, f) in notes)
                {
                    if (t < start) continue;
                    var u = t - start;
                    s += (Math.Sin(2 * Math.PI * f * u) + 0.35 * Math.Sin(2 * Math.PI * f * 2.76 * u)) * Math.Exp(-14 * u);
                }
                data[i] = (float)(0.28 * s);
            }
            return data;
        }

        /// <summary>Hauteur du moteur (AudioSource.pitch) selon la vitesse : 0,8 au ralenti, 2 à la vitesse maximale.</summary>
        public static float EnginePitch(float speedKmh, float maxKmh)
        {
            var x = maxKmh > 0f ? Math.Max(0f, Math.Min(1f, Math.Abs(speedKmh) / maxKmh)) : 0f;
            return 0.8f + 1.2f * x;
        }

        /// <summary>Volume du moteur : plus fort quand on accélère.</summary>
        public static float EngineVolume(float throttle) => 0.35f + 0.3f * Math.Min(1f, Math.Abs(throttle));

        /// <summary>Freinage fort à bonne vitesse : de quoi faire crisser les pneus.</summary>
        public static bool HardBraking(float speedKmh, float brake) => brake >= 0.8f && Math.Abs(speedKmh) >= 25f;

        /// <summary>Générateur congruentiel : bruit blanc reproductible entre -1 et 1.</summary>
        private struct Lcg
        {
            private uint _state;
            public Lcg(uint seed) => _state = seed;

            public double Next()
            {
                _state = _state * 1664525u + 1013904223u;
                return (_state >> 8) / (double)(1 << 24) * 2 - 1;
            }
        }
    }
}
