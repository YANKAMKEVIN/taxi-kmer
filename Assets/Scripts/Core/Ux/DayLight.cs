using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Une teinte du ciel à une heure donnée (couleurs en 0..1).</summary>
    [Serializable]
    public class DayLightKey
    {
        /// <summary>Heure « HH:MM ».</summary>
        public string time = "12:00";
        public float[] sun = { 1f, 0.96f, 0.88f };
        public float[] ambient = { 0.75f, 0.82f, 0.95f };
        public float[] fog = { 0.8f, 0.85f, 0.9f };
    }

    /// <summary>
    /// Cycle jour / nuit (idée n° 3 ; données : Assets/Resources/Data/daylight.json). Valeurs par défaut de Claude pour
    /// Yaoundé (lever ≈ 06:00, coucher ≈ 18:15, presque sans variation dans l'année), à régler à l'œil sur le Mac.
    /// </summary>
    [Serializable]
    public class DayLightConfig
    {
        public bool enabled = true;
        public string sunrise = "06:00";
        public string sunset = "18:15";
        /// <summary>Hauteur du soleil à midi solaire (degrés) ; Yaoundé est presque sous l'équateur.</summary>
        public float noonElevationDegrees = 75f;
        /// <summary>Hauteur minimale de la lumière la nuit (degrés) : la lune éclaire encore un peu.</summary>
        public float nightElevationDegrees = 25f;
        /// <summary>Cap du soleil (degrés, 0 = nord) au lever ; il se couche à l'opposé.</summary>
        public float sunriseHeadingDegrees = 90f;
        public float dayIntensity = 1.1f;
        public float nightIntensity = 0.12f;
        /// <summary>Durée du crépuscule (min) de part et d'autre du lever et du coucher.</summary>
        public float twilightMinutes = 30f;
        /// <summary>Les lampadaires et les phares s'allument à cette heure… </summary>
        public string lampsOn = "18:00";
        /// <summary>… et s'éteignent à celle-ci le matin.</summary>
        public string lampsOff = "06:20";
        public DayLightKey[] keys = DefaultKeys();

        public static DayLightKey[] DefaultKeys() => new[]
        {
            new DayLightKey { time = "05:30", sun = new[] { 0.35f, 0.4f, 0.6f }, ambient = new[] { 0.12f, 0.14f, 0.22f }, fog = new[] { 0.15f, 0.17f, 0.25f } },
            new DayLightKey { time = "06:30", sun = new[] { 1f, 0.7f, 0.45f }, ambient = new[] { 0.55f, 0.5f, 0.5f }, fog = new[] { 0.85f, 0.7f, 0.6f } },
            new DayLightKey { time = "09:00", sun = new[] { 1f, 0.95f, 0.85f }, ambient = new[] { 0.75f, 0.82f, 0.95f }, fog = new[] { 0.8f, 0.85f, 0.9f } },
            new DayLightKey { time = "16:30", sun = new[] { 1f, 0.95f, 0.85f }, ambient = new[] { 0.75f, 0.82f, 0.95f }, fog = new[] { 0.8f, 0.85f, 0.9f } },
            new DayLightKey { time = "18:00", sun = new[] { 1f, 0.55f, 0.3f }, ambient = new[] { 0.6f, 0.45f, 0.4f }, fog = new[] { 0.9f, 0.6f, 0.45f } },
            new DayLightKey { time = "18:45", sun = new[] { 0.35f, 0.4f, 0.6f }, ambient = new[] { 0.12f, 0.14f, 0.22f }, fog = new[] { 0.15f, 0.17f, 0.25f } },
        };
    }

    /// <summary>État de la lumière à une heure : direction et force du soleil, teintes, éclairage public.</summary>
    public struct DayLightState
    {
        /// <summary>Hauteur de la lumière principale (degrés au-dessus de l'horizon).</summary>
        public float ElevationDegrees;
        /// <summary>Cap de la lumière principale (degrés, 0 = nord, 90 = est).</summary>
        public float HeadingDegrees;
        public float Intensity;
        public float[] Sun;
        public float[] Ambient;
        public float[] Fog;
        /// <summary>Lampadaires du muret et phares allumés.</summary>
        public bool LampsOn;
        /// <summary>0 en plein jour, 1 en pleine nuit (pour doser les effets).</summary>
        public float Night;
    }

    /// <summary>Calcule la lumière du jeu à partir de l'heure (minutes depuis minuit) : pur, testable sans scène.</summary>
    public sealed class DayLight
    {
        private readonly DayLightConfig _config;
        private readonly float _rise, _set, _lampsOn, _lampsOff;
        private readonly float[] _keyTimes;

        public DayLight(DayLightConfig config)
        {
            _config = config ?? new DayLightConfig();
            _rise = Parse(_config.sunrise, 360);
            _set = Parse(_config.sunset, 1095);
            if (_set <= _rise) throw new ArgumentException("Le coucher doit suivre le lever.");
            _lampsOn = Parse(_config.lampsOn, 1080);
            _lampsOff = Parse(_config.lampsOff, 380);
            var keys = _config.keys ?? new DayLightKey[0];
            _keyTimes = new float[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                _keyTimes[i] = Parse(keys[i].time, 720);
                if (i > 0 && _keyTimes[i] <= _keyTimes[i - 1]) throw new ArgumentException("Teintes dans l'ordre des heures.");
            }
        }

        public bool Enabled => _config.enabled;

        /// <summary>0 la nuit, 1 en plein jour, en douceur sur le crépuscule autour du lever et du coucher.</summary>
        public float Daylight(float minutes)
        {
            var tw = Math.Max(1f, _config.twilightMinutes);
            var up = Smooth((minutes - (_rise - tw)) / (2f * tw));
            var down = 1f - Smooth((minutes - (_set - tw)) / (2f * tw));
            return Math.Min(up, down);
        }

        public bool LampsOn(float minutes) => minutes >= _lampsOn || minutes < _lampsOff;

        public DayLightState At(float minutes)
        {
            var day = Daylight(minutes);
            // Arc du soleil : 0 au lever, 1 au coucher ; hauteur en sinus.
            var arc = Clamp01((minutes - _rise) / (_set - _rise));
            var sunElevation = (float)Math.Sin(arc * Math.PI) * _config.noonElevationDegrees;
            var state = new DayLightState
            {
                ElevationDegrees = Lerp(_config.nightElevationDegrees, Math.Max(sunElevation, 5f), day),
                HeadingDegrees = _config.sunriseHeadingDegrees + 180f * arc,
                Intensity = Lerp(_config.nightIntensity, _config.dayIntensity, day),
                LampsOn = LampsOn(minutes),
                Night = 1f - day,
            };
            Colors(minutes, out state.Sun, out state.Ambient, out state.Fog);
            return state;
        }

        private void Colors(float minutes, out float[] sun, out float[] ambient, out float[] fog)
        {
            var keys = _config.keys;
            if (keys == null || keys.Length == 0)
            {
                var d = new DayLightKey();
                sun = d.sun; ambient = d.ambient; fog = d.fog;
                return;
            }
            if (minutes <= _keyTimes[0]) { sun = keys[0].sun; ambient = keys[0].ambient; fog = keys[0].fog; return; }
            var last = keys.Length - 1;
            if (minutes >= _keyTimes[last]) { sun = keys[last].sun; ambient = keys[last].ambient; fog = keys[last].fog; return; }
            var i = 1;
            while (_keyTimes[i] < minutes) i++;
            var t = (minutes - _keyTimes[i - 1]) / (_keyTimes[i] - _keyTimes[i - 1]);
            sun = Mix(keys[i - 1].sun, keys[i].sun, t);
            ambient = Mix(keys[i - 1].ambient, keys[i].ambient, t);
            fog = Mix(keys[i - 1].fog, keys[i].fog, t);
        }

        private static float[] Mix(float[] a, float[] b, float t)
        {
            var r = new float[3];
            for (var i = 0; i < 3; i++) r[i] = Lerp(a[i], b[i], t);
            return r;
        }

        /// <summary>« HH:MM » en minutes depuis minuit ; <paramref name="fallback"/> si illisible.</summary>
        public static float Parse(string time, float fallback)
        {
            if (string.IsNullOrEmpty(time)) return fallback;
            var parts = time.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return fallback;
            if (h < 0 || h > 23 || m < 0 || m > 59) return fallback;
            return h * 60 + m;
        }

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
