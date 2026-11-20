using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Réglages du joueur (volume général), enregistrés par la présentation dans les préférences.</summary>
    public sealed class GameSettings
    {
        public const float VolumeStep = 0.1f;
        public const float DefaultVolume = 0.8f;

        private float _volume = DefaultVolume;

        /// <summary>Volume général, de 0 à 1, par pas de 10 %.</summary>
        public float Volume
        {
            get => _volume;
            set => _volume = (float)Math.Round(Math.Max(0f, Math.Min(1f, value)) / VolumeStep) * VolumeStep;
        }

        public void VolumeUp() => Volume = _volume + VolumeStep;
        public void VolumeDown() => Volume = _volume - VolumeStep;

        public string VolumeLabel => _volume <= 0f ? "Volume : coupé" : $"Volume : {(int)Math.Round(_volume * 100f)} %";
    }
}
