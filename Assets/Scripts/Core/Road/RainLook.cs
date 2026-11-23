using System;

namespace TaxiKmer.Core.Road
{
    /// <summary>
    /// Pluie visible (idée n° 12 ; données : Assets/Resources/Data/rain.json) : gouttes, chaussée mouillée,
    /// essuie-glaces et bruit sur la tôle. Valeurs par défaut de Claude, réglées à l'œil sur le Mac. Les gouttes sont
    /// coupées au niveau Bas (règle du projet : tout effet coûteux se coupe au niveau bas).
    /// </summary>
    [Serializable]
    public class RainLookConfig
    {
        public bool enabled = true;
        /// <summary>L'averse monte et retombe en douceur sur cette durée (s réelles).</summary>
        public float fadeSeconds = 6f;
        /// <summary>La chaussée est trempée après cette durée de pluie (s), et sèche en dryingSeconds.</summary>
        public float soakSeconds = 20f;
        public float dryingSeconds = 90f;
        /// <summary>Gouttes par seconde à pleine averse, par niveau de qualité (Bas, Moyen, Haut).</summary>
        public int[] dropsPerSecondByLevel = { 0, 600, 1500 };
        /// <summary>Brillance de la chaussée sèche et trempée (Smoothness du matériau URP).</summary>
        public float drySmoothness = 0.15f;
        public float wetSmoothness = 0.75f;
        /// <summary>Allers-retours d'essuie-glace par seconde à pleine averse.</summary>
        public float wiperCyclesPerSecond = 1.2f;
        /// <summary>Volume du bruit de pluie sur la tôle à pleine averse.</summary>
        public float roofVolume = 0.5f;
    }

    /// <summary>État visuel et sonore de la pluie, image par image (pur, testable).</summary>
    public sealed class RainLook
    {
        private readonly RainLookConfig _config;

        /// <summary>Force de l'averse affichée (0 à 1).</summary>
        public float Intensity { get; private set; }
        /// <summary>Chaussée mouillée (0 sèche, 1 trempée) : monte vite sous la pluie, sèche lentement après.</summary>
        public float Wetness { get; private set; }
        /// <summary>Position de l'essuie-glace (0 à 1 aller, 1 à 0 retour).</summary>
        public float WiperPosition { get; private set; }
        private float _wiperPhase;

        public RainLook(RainLookConfig config) => _config = config ?? new RainLookConfig();

        public bool Enabled => _config.enabled;

        public void Update(float dt, bool raining)
        {
            if (dt <= 0f) return;
            var target = raining && _config.enabled ? 1f : 0f;
            var step = dt / Math.Max(0.01f, _config.fadeSeconds);
            Intensity = target > Intensity ? Math.Min(target, Intensity + step) : Math.Max(target, Intensity - step);
            if (Intensity > 0f) Wetness = Math.Min(1f, Wetness + Intensity * dt / Math.Max(0.01f, _config.soakSeconds));
            else Wetness = Math.Max(0f, Wetness - dt / Math.Max(0.01f, _config.dryingSeconds));
            if (Intensity > 0.01f || WiperPosition > 0f)
            {
                _wiperPhase += dt * _config.wiperCyclesPerSecond * Math.Max(Intensity, 0.3f);
                _wiperPhase %= 1f;
                WiperPosition = _wiperPhase < 0.5f ? _wiperPhase * 2f : 2f - _wiperPhase * 2f;
                if (Intensity <= 0.01f && WiperPosition < 0.05f) { WiperPosition = 0f; _wiperPhase = 0f; } // se range au repos
            }
        }

        public int DropsPerSecond(int qualityLevel)
        {
            var byLevel = _config.dropsPerSecondByLevel;
            if (byLevel == null || byLevel.Length == 0) return 0;
            var i = Math.Max(0, Math.Min(byLevel.Length - 1, qualityLevel));
            return (int)Math.Round(byLevel[i] * Intensity);
        }

        public float RoadSmoothness => _config.drySmoothness + (_config.wetSmoothness - _config.drySmoothness) * Wetness;
        public float RoofVolume => _config.roofVolume * Intensity;
    }
}
