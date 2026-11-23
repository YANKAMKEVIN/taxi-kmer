using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Réglages du joueur (volume général, vibrations), enregistrés par la présentation dans les préférences.</summary>
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

        /// <summary>Vibrations aux moments clés (klaxon, montée, paiement, refus).</summary>
        public bool Vibration { get; set; } = true;

        public string VibrationLabel => Vibration ? "Vibrations : oui" : "Vibrations : non";

        public string VolumeLabel => _volume <= 0f ? "Volume : coupé" : $"Volume : {(int)Math.Round(_volume * 100f)} %";

        // ---------- Accessibilité (idée n° 29) ----------

        /// <summary>Tailles de texte proposées (bulles des passagers, messages) : normale, grande, très grande.</summary>
        public static readonly float[] TextScales = { 1f, 1.25f, 1.5f };
        public int TextScaleIndex { get; set; }
        public float TextScale => TextScales[Math.Max(0, Math.Min(TextScales.Length - 1, TextScaleIndex))];
        public string TextScaleLabel => TextScaleIndex <= 0 ? "Texte : normal" : TextScaleIndex == 1 ? "Texte : grand" : "Texte : très grand";

        /// <summary>Couleurs lisibles pour les daltoniens (bleu / orange au lieu de vert / rouge).</summary>
        public bool ColorBlind { get; set; }
        public string ColorBlindLabel => ColorBlind ? "Couleurs daltoniens : oui" : "Couleurs daltoniens : non";

        // ---------- Commandes (idée n° 30) ----------

        /// <summary>Commandes pour gaucher : flèches à droite, pédales à gauche.</summary>
        public bool LeftHanded { get; set; }
        public string LeftHandedLabel => LeftHanded ? "Commandes : gaucher" : "Commandes : droitier";
        public static readonly float[] ButtonScales = { 1f, 1.15f, 0.85f };
        public int ButtonScaleIndex { get; set; }
        public float ButtonScale => ButtonScales[Math.Max(0, Math.Min(ButtonScales.Length - 1, ButtonScaleIndex))];
        public string ButtonScaleLabel => $"Boutons : {(int)Math.Round(ButtonScale * 100f)} %";
        public static readonly float[] ButtonOpacities = { 1f, 0.75f, 0.5f };
        public int ButtonOpacityIndex { get; set; }
        public float ButtonOpacity => ButtonOpacities[Math.Max(0, Math.Min(ButtonOpacities.Length - 1, ButtonOpacityIndex))];
        public string ButtonOpacityLabel => $"Opacité des boutons : {(int)Math.Round(ButtonOpacity * 100f)} %";

        /// <summary>Réglages qui défilent d'un appui, dans l'ordre de l'écran des réglages (sauf le volume et ses boutons – / +).</summary>
        public List<SettingRow> Rows()
        {
            var rows = new List<SettingRow>
            {
                new SettingRow(SettingIds.Vibration, VibrationLabel),
                new SettingRow(SettingIds.TextScale, TextScaleLabel),
                new SettingRow(SettingIds.ColorBlind, ColorBlindLabel),
                new SettingRow(SettingIds.LeftHanded, LeftHandedLabel),
                new SettingRow(SettingIds.ButtonScale, ButtonScaleLabel),
                new SettingRow(SettingIds.ButtonOpacity, ButtonOpacityLabel),
            };
            return rows;
        }

        /// <summary>Un appui sur la ligne : passe à la valeur suivante.</summary>
        public void Cycle(string id)
        {
            switch (id)
            {
                case SettingIds.Vibration: Vibration = !Vibration; break;
                case SettingIds.TextScale: TextScaleIndex = (TextScaleIndex + 1) % TextScales.Length; break;
                case SettingIds.ColorBlind: ColorBlind = !ColorBlind; break;
                case SettingIds.LeftHanded: LeftHanded = !LeftHanded; break;
                case SettingIds.ButtonScale: ButtonScaleIndex = (ButtonScaleIndex + 1) % ButtonScales.Length; break;
                case SettingIds.ButtonOpacity: ButtonOpacityIndex = (ButtonOpacityIndex + 1) % ButtonOpacities.Length; break;
            }
        }

        /// <summary>Lit les réglages enregistrés (préférences du téléphone).</summary>
        public void Load(ISettingsStore store)
        {
            if (store == null) return;
            Volume = store.GetFloat(SettingIds.Volume, DefaultVolume);
            Vibration = store.GetInt(SettingIds.Vibration, 1) == 1;
            TextScaleIndex = Math.Max(0, Math.Min(TextScales.Length - 1, store.GetInt(SettingIds.TextScale, 0)));
            ColorBlind = store.GetInt(SettingIds.ColorBlind, 0) == 1;
            LeftHanded = store.GetInt(SettingIds.LeftHanded, 0) == 1;
            ButtonScaleIndex = Math.Max(0, Math.Min(ButtonScales.Length - 1, store.GetInt(SettingIds.ButtonScale, 0)));
            ButtonOpacityIndex = Math.Max(0, Math.Min(ButtonOpacities.Length - 1, store.GetInt(SettingIds.ButtonOpacity, 0)));
        }

        public void Save(ISettingsStore store)
        {
            if (store == null) return;
            store.SetFloat(SettingIds.Volume, Volume);
            store.SetInt(SettingIds.Vibration, Vibration ? 1 : 0);
            store.SetInt(SettingIds.TextScale, TextScaleIndex);
            store.SetInt(SettingIds.ColorBlind, ColorBlind ? 1 : 0);
            store.SetInt(SettingIds.LeftHanded, LeftHanded ? 1 : 0);
            store.SetInt(SettingIds.ButtonScale, ButtonScaleIndex);
            store.SetInt(SettingIds.ButtonOpacity, ButtonOpacityIndex);
        }
    }

    /// <summary>Une ligne de l'écran des réglages : identifiant et libellé du moment.</summary>
    public struct SettingRow
    {
        public string Id;
        public string Label;
        public SettingRow(string id, string label) { Id = id; Label = label; }
    }

    /// <summary>Clés des réglages (aussi les clés des préférences du téléphone).</summary>
    public static class SettingIds
    {
        public const string Volume = "reglages.volume";
        public const string Vibration = "reglages.vibrations";
        public const string TextScale = "reglages.texte";
        public const string ColorBlind = "reglages.daltoniens";
        public const string LeftHanded = "reglages.gaucher";
        public const string ButtonScale = "reglages.taille-boutons";
        public const string ButtonOpacity = "reglages.opacite-boutons";
    }

    /// <summary>Où sont gardés les réglages (PlayerPrefs dans Unity ; un dictionnaire dans les tests).</summary>
    public interface ISettingsStore
    {
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
    }

    /// <summary>
    /// Couleurs des jauges et des bulles (idée n° 29) : vert / rouge habituels, ou bleu / orange vermillon lisibles par
    /// les daltoniens (palette d'Okabe et Ito). Couleurs r, g, b, a en 0..1.
    /// </summary>
    public static class HudPalette
    {
        public static float[] Good(bool colorBlind) => colorBlind ? new[] { 0f, 0.62f, 0.95f, 0.95f } : new[] { 0.3f, 0.85f, 0.4f, 0.95f };
        public static float[] Bad(bool colorBlind) => colorBlind ? new[] { 0.9f, 0.45f, 0f, 1f } : new[] { 1f, 0.3f, 0.25f, 1f };
        public static float[] Warning(bool colorBlind) => colorBlind ? new[] { 0.95f, 0.9f, 0.25f, 1f } : new[] { 1f, 0.6f, 0.15f, 1f };
    }
}
