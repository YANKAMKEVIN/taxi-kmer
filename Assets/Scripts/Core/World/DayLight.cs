using System;

namespace TaxiKmer.Core.World
{
    /// <summary>Une clé de lumière (daylight.json) : l'état de la lumière à une minute de la journée.</summary>
    [Serializable]
    public class DayLightKey
    {
        public float minute;
        public float elevation;
        public float azimuth;
        public float[] sun = { 1f, 1f, 1f };
        public float intensity = 1f;
        public float[] sky = { 0.5f, 0.6f, 0.8f };
        public float[] equator = { 0.6f, 0.6f, 0.6f };
        public float[] ground = { 0.3f, 0.2f, 0.15f };
        public float[] fog = { 0.8f, 0.85f, 0.9f };
        public float exposure;
        public bool lamps;
    }

    [Serializable]
    public class DayLightConfig
    {
        public DayLightKey[] keys = Array.Empty<DayLightKey>();
        /// <summary>La nuit, une lumière froide et faible remplace le soleil couché (sinon tout serait noir).</summary>
        public float moonIntensity = 0.15f;
        public float[] moonColor = { 0.55f, 0.65f, 0.9f };
    }

    /// <summary>Couleur RVB linéaire simple (la logique pure ne dépend pas d'Unity).</summary>
    public struct Rgb
    {
        public float R, G, B;

        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static Rgb From(float[] c) => c != null && c.Length >= 3 ? new Rgb(c[0], c[1], c[2]) : new Rgb(1f, 1f, 1f);
        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    /// <summary>État de la lumière à un instant (résultat de <see cref="DayLight.At"/>).</summary>
    public struct DayLightState
    {
        /// <summary>Hauteur du soleil au-dessus de l'horizon (degrés ; négatif : sous l'horizon).</summary>
        public float Elevation;
        /// <summary>Direction du soleil (degrés depuis le nord, 90 = est).</summary>
        public float Azimuth;
        public Rgb SunColor;
        public float SunIntensity;
        public Rgb Sky, Equator, Ground, Fog;
        /// <summary>Correction d'exposition (EV) de l'étalonnage.</summary>
        public float Exposure;
        public bool Lamps;
        /// <summary>La lumière principale vient de la lune (soleil couché).</summary>
        public bool Night;
        /// <summary>Clarté du ciel, de 0 (nuit noire) à 1 (plein jour), d'après la vraie hauteur du soleil (pas celle de la lune).</summary>
        public float SkyBrightness;
    }

    /// <summary>
    /// Lumière du jour à Yaoundé, minute par minute (logique pure, testée) : interpolation linéaire entre les clés de
    /// daylight.json, bornée par la première et la dernière clé. Soleil couché : une lumière de lune prend le relais, haute
    /// et froide, pour que la nuit reste lisible.
    /// </summary>
    public static class DayLight
    {
        public static DayLightState At(DayLightConfig config, float minute)
        {
            var keys = config?.keys;
            if (keys == null || keys.Length == 0)
                return new DayLightState { Elevation = 60f, Azimuth = 180f, SunColor = new Rgb(1f, 1f, 1f), SunIntensity = 1f, Sky = new Rgb(0.6f, 0.7f, 0.9f), Equator = new Rgb(0.7f, 0.7f, 0.7f), Ground = new Rgb(0.3f, 0.2f, 0.15f), Fog = new Rgb(0.8f, 0.85f, 0.9f) };

            var i = 0;
            while (i < keys.Length - 1 && keys[i + 1].minute <= minute) i++;
            var a = keys[i];
            var b = i < keys.Length - 1 ? keys[i + 1] : a;
            var span = b.minute - a.minute;
            var t = span > 0f ? Clamp01((minute - a.minute) / span) : 0f;
            if (minute < keys[0].minute) t = 0f;

            var state = new DayLightState
            {
                Elevation = Lerp(a.elevation, b.elevation, t),
                Azimuth = Lerp(a.azimuth, b.azimuth, t),
                SunColor = Rgb.Lerp(Rgb.From(a.sun), Rgb.From(b.sun), t),
                SunIntensity = Lerp(a.intensity, b.intensity, t),
                Sky = Rgb.Lerp(Rgb.From(a.sky), Rgb.From(b.sky), t),
                Equator = Rgb.Lerp(Rgb.From(a.equator), Rgb.From(b.equator), t),
                Ground = Rgb.Lerp(Rgb.From(a.ground), Rgb.From(b.ground), t),
                Fog = Rgb.Lerp(Rgb.From(a.fog), Rgb.From(b.fog), t),
                Exposure = Lerp(a.exposure, b.exposure, t),
                Lamps = t < 0.5f ? a.lamps : b.lamps,
            };
            state.SkyBrightness = Clamp01((state.Elevation + 6f) / 30f);
            if (state.Elevation <= 0f)
            {
                // Lune : lumière froide et haute ; elle monte à mesure que le soleil descend sous l'horizon.
                var night = Clamp01(-state.Elevation / 6f);
                state.Night = night > 0f;
                state.SunColor = Rgb.Lerp(state.SunColor, Rgb.From(config.moonColor), night);
                state.SunIntensity = Math.Max(state.SunIntensity, config.moonIntensity * night);
                state.Elevation = Lerp(state.Elevation, 55f, night);
                state.Azimuth = Lerp(state.Azimuth, 200f, night);
            }
            return state;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
