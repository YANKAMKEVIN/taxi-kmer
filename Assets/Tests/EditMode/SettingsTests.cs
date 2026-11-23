using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    /// <summary>Réglages enregistrés dans un dictionnaire (à la place des PlayerPrefs).</summary>
    public sealed class MemoryStore : ISettingsStore
    {
        public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
        public float GetFloat(string key, float fallback) => Values.TryGetValue(key, out var v) ? v : fallback;
        public int GetInt(string key, int fallback) => Values.TryGetValue(key, out var v) ? (int)v : fallback;
        public void SetFloat(string key, float value) => Values[key] = value;
        public void SetInt(string key, int value) => Values[key] = value;
    }

    public class SettingsTests
    {
        [Test]
        public void TextSize_CyclesNormalLargeVeryLarge()
        {
            var s = new GameSettings();
            Assert.AreEqual(1f, s.TextScale);
            Assert.AreEqual("Texte : normal", s.TextScaleLabel);
            s.Cycle(SettingIds.TextScale);
            Assert.AreEqual(1.25f, s.TextScale);
            s.Cycle(SettingIds.TextScale);
            Assert.AreEqual("Texte : très grand", s.TextScaleLabel);
            s.Cycle(SettingIds.TextScale);
            Assert.AreEqual(1f, s.TextScale);
        }

        [Test]
        public void ColorBlindPalette_AvoidsRedGreen()
        {
            var s = new GameSettings();
            s.Cycle(SettingIds.ColorBlind);
            Assert.IsTrue(s.ColorBlind);
            var good = HudPalette.Good(true);
            var bad = HudPalette.Bad(true);
            Assert.Greater(good[2], good[1], "« bon » est bleu");
            Assert.Less(bad[2], 0.1f, "« mauvais » est orange");
            Assert.Greater(HudPalette.Good(false)[1], HudPalette.Good(false)[0], "vert par défaut");
        }

        [Test]
        public void Settings_RoundTripThroughTheStore_WithTheOldKeys()
        {
            var store = new MemoryStore();
            var s = new GameSettings { Volume = 0.5f, Vibration = false, TextScaleIndex = 2, ColorBlind = true };
            s.Save(store);
            Assert.AreEqual(0.5f, store.Values["reglages.volume"], "même clé qu'avant (StreetView.VolumeKey)");
            Assert.AreEqual(0f, store.Values["reglages.vibrations"]);
            var back = new GameSettings();
            back.Load(store);
            Assert.AreEqual(0.5f, back.Volume, 1e-4f);
            Assert.IsFalse(back.Vibration);
            Assert.AreEqual(1.5f, back.TextScale);
            Assert.IsTrue(back.ColorBlind);
            var fresh = new GameSettings();
            fresh.Load(new MemoryStore());
            Assert.AreEqual(GameSettings.DefaultVolume, fresh.Volume, 1e-4f);
            Assert.IsTrue(fresh.Vibration);
        }

        [Test]
        public void Rows_ListEveryCyclingSetting_Once()
        {
            var rows = new GameSettings().Rows();
            CollectionAssert.IsSubsetOf(new[] { SettingIds.Vibration, SettingIds.TextScale, SettingIds.ColorBlind }, rows.Select(r => r.Id).ToList());
            Assert.AreEqual(rows.Count, rows.Select(r => r.Id).Distinct().Count());
        }
    }
}
