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

        /// <summary>
        /// Klaxon d'une autre voiture, au loin (0,45 s ; ambiance du jalon 5) : deux tons rauques (≈ 400 et 500 Hz), un peu
        /// étouffés par la distance. La hauteur varie par AudioSource.pitch pour ne pas entendre toujours la même voiture.
        /// </summary>
        public static float[] DistantHorn()
        {
            var n = (int)(Rate * 0.45f);
            var data = new float[n];
            var lp = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var env = Math.Min(1.0, t / 0.02) * Math.Min(1.0, (0.45 - t) / 0.08);
                var raw = Math.Tanh(2.5 * (Math.Sin(2 * Math.PI * 405 * t) + Math.Sin(2 * Math.PI * 508 * t)));
                lp += 0.25 * (raw - lp); // étouffé par la distance
                data[i] = (float)(0.3 * env * lp);
            }
            return data;
        }

        /// <summary>
        /// Rumeur de la ville bouclable (3 s) : bruit grave filtré qui ondule lentement (moteurs lointains, vent). Raccord
        /// en fondu enchaîné : la fin rejoint le début sans clic.
        /// </summary>
        public static float[] StreetMurmur() => Loop(3f, 17, (t, noise, state) =>
        {
            state[0] += 0.02 * (noise - state[0]);
            var swell = 0.75 + 0.25 * Math.Sin(2 * Math.PI * t / 3.0);
            return 1.8 * state[0] * swell;
        });

        /// <summary>
        /// Pluie sur la tôle du taxi bouclable (2 s, idée n° 12) : bruit aigu filtré, crépitement de gouttes au hasard.
        /// Raccord sans clic.
        /// </summary>
        public static float[] RainOnRoof() => Loop(2f, 41, (t, noise, state) =>
        {
            state[0] += 0.6 * (noise - state[0]);
            state[1] += 0.05 * (state[0] - state[1]);
            var hiss = state[0] - state[1];
            state[2] = noise > 0.985 ? 1.0 : state[2] * 0.9; // une goutte qui claque, puis s'éteint
            return 0.35 * hiss + 0.4 * state[2] * Math.Sin(2 * Math.PI * 2400 * t);
        });

        /// <summary>
        /// Rumeur du marché bouclable (4 s) : bruit dans la bande de la voix, haché en syllabes par plusieurs « voix » qui
        /// ne parlent pas au même rythme, avec quelques appels plus hauts. Raccord sans clic.
        /// </summary>
        public static float[] MarketChatter() => Loop(4f, 29, (t, noise, state) =>
        {
            state[0] += 0.35 * (noise - state[0]);   // passe-bas
            state[1] += 0.04 * (state[0] - state[1]); // retire le grave : bande de la voix
            var band = state[0] - state[1];
            double syll = 0;
            var rates = new[] { 3.1, 4.3, 5.2, 3.7 };
            for (var k = 0; k < rates.Length; k++) syll += Math.Max(0, Math.Sin(2 * Math.PI * rates[k] * t + k * 1.9));
            var call = Math.Max(0, Math.Sin(2 * Math.PI * 0.5 * t)) * 0.3 * Math.Sin(2 * Math.PI * (300 + 40 * Math.Sin(2 * Math.PI * 2 * t)) * t);
            return 0.9 * band * (0.3 + syll / rates.Length) + 0.25 * call * syll / rates.Length;
        });

        /// <summary>
        /// Appel d'un client au bord de la route (0,5 s, provisoire) : une voix synthétique « éé ! » qui monte puis descend
        /// (deux formants sur une fondamentale de 220 Hz), en attendant de vrais enregistrements (« Emana, 600 ! »).
        /// </summary>
        public static float[] ClientCall()
        {
            var n = (int)(Rate * 0.5f);
            var data = new float[n];
            var phase = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var f0 = 220 + 70 * Math.Sin(Math.PI * t / 0.5);
                phase += 2 * Math.PI * f0 / Rate;
                double s = 0;
                for (var k = 1; k <= 12; k++)
                {
                    var f = f0 * k;
                    var formant = Math.Exp(-Math.Pow((f - 550) / 180, 2)) + 0.7 * Math.Exp(-Math.Pow((f - 1900) / 300, 2));
                    s += formant * Math.Sin(k * phase);
                }
                var env = Math.Min(1.0, t / 0.03) * Math.Min(1.0, (0.5 - t) / 0.12);
                data[i] = (float)(0.35 * env * Math.Tanh(s));
            }
            return data;
        }

        /// <summary>
        /// Petit son de voix provisoire d'un passager (jalon 5, texte + petits sons, option B de Kevin) pour un id de
        /// dialogues.json → voiceSounds : « hmm », « eh », « tchip », « aie », « rire » ; null pour un id inconnu. Joué
        /// tant que le vrai son reste « à fournir ». Voix synthétique à deux formants : reconnaissable, pas réaliste.
        /// </summary>
        public static float[] Voice(string id)
        {
            switch (id)
            {
                case "hmm": // bouche fermée, qui doute : grave, nasal, qui descend
                    return Vowel(0.45, t => 150 - 25 * t / 0.45, t => 260, t => 2100, 0.5);
                case "eh": // surprise, appel : « é » bref qui monte
                    return Vowel(0.25, t => 230 + 120 * t / 0.25, t => 480, t => 1900, 1);
                case "aie": // « a » qui glisse vers « ï »
                    return Vowel(0.4, t => 260 - 40 * t / 0.4, t => 800 - 500 * t / 0.4, t => 1250 + 1050 * t / 0.4, 1);
                case "rire": // quatre petits « ha »
                    return Vowel(0.75, t => 210 - 30 * t / 0.75, t => 750, t => 1250, 1, t => Math.Max(0, Math.Sin(2 * Math.PI * 5.5 * t)));
                case "tchip": // succion entre les dents : souffle aigu qui monte, sans voix
                {
                    var n = (int)(Rate * 0.3f);
                    var data = new float[n];
                    var rng = new Lcg(41);
                    double lp = 0, prev = 0;
                    for (var i = 0; i < n; i++)
                    {
                        var t = i / (float)Rate;
                        lp += 0.5 * (rng.Next() - lp);
                        var hp = lp - prev; // aigu seulement
                        prev = lp;
                        var tone = Math.Sin(2 * Math.PI * (2200 + 2600 * t / 0.3) * t);
                        var env = Math.Min(1.0, t / 0.03) * Math.Exp(-6 * t) * Math.Min(1.0, (0.3 - t) / 0.05);
                        data[i] = (float)Math.Max(-1, Math.Min(1, env * (1.4 * hp + 0.12 * tone)));
                    }
                    return data;
                }
                default:
                    return null;
            }
        }

        /// <summary>Voyelle synthétique : fondamentale f0(t), deux formants F1(t) et F2(t), enveloppe d'attaque et de fin, hachage optionnel.</summary>
        private static float[] Vowel(double seconds, Func<double, double> f0, Func<double, double> f1, Func<double, double> f2, double brightness,
            Func<double, double> chop = null)
        {
            var n = (int)(Rate * seconds);
            var data = new float[n];
            var phase = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = i / (double)Rate;
                var f = f0(t);
                phase += 2 * Math.PI * f / Rate;
                double s = 0;
                for (var k = 1; k * f < 4000; k++)
                {
                    var h = k * f;
                    var amp = Math.Exp(-Math.Pow((h - f1(t)) / 160, 2)) + brightness * 0.6 * Math.Exp(-Math.Pow((h - f2(t)) / 260, 2));
                    s += amp * Math.Sin(k * phase);
                }
                var env = Math.Min(1.0, t / 0.025) * Math.Min(1.0, (seconds - t) / Math.Min(0.1, seconds / 3));
                if (chop != null) env *= chop(t);
                data[i] = (float)(0.35 * env * Math.Tanh(s));
            }
            return data;
        }

        /// <summary>Son bouclable : <paramref name="sample"/>(t, bruit, état) sur la durée, plus un fondu enchaîné de 0,25 s fin → début.</summary>
        private static float[] Loop(float seconds, uint seed, Func<double, double, double[], double> sample)
        {
            var n = (int)(Rate * seconds);
            var fade = Rate / 4;
            var raw = new double[n + fade];
            var rng = new Lcg(seed);
            var state = new double[4];
            for (var i = 0; i < raw.Length; i++) raw[i] = sample(i / (double)Rate, rng.Next(), state);
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var v = raw[i];
                if (i < fade)
                {
                    var k = i / (double)fade;
                    v = raw[i] * k + raw[n + i] * (1 - k); // le début reprend là où la fin s'arrête
                }
                data[i] = (float)Math.Max(-1, Math.Min(1, v));
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
