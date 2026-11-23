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

        [Test]
        public void LeftHanded_ArrowsGoRight_PedalsLeft_TopStays()
        {
            const float W = 2340f, H = 1080f;
            var layout = TaxiKmer.Core.Driving.ControlLayout.Adjusted(TaxiKmer.Core.Driving.ControlLayout.Default(), true, 1f);
            var left = layout.zones.First(z => z.control == TaxiKmer.Core.Driving.DriveControl.SteerLeft).Resolve(W, H);
            var right = layout.zones.First(z => z.control == TaxiKmer.Core.Driving.DriveControl.SteerRight).Resolve(W, H);
            var accel = layout.zones.First(z => z.control == TaxiKmer.Core.Driving.DriveControl.Accelerate).Resolve(W, H);
            var horn = layout.zones.First(z => z.control == TaxiKmer.Core.Driving.DriveControl.Horn).Resolve(W, H);
            Assert.Greater(left.X, W / 2f, "flèches à droite");
            Assert.Less(left.X, right.X, "◄ à gauche de ►");
            Assert.Less(accel.X, W / 2f, "pédales à gauche");
            Assert.Greater(horn.X, W / 2f, "le klaxon reste en haut à droite");
        }

        [Test]
        public void AdjustedLayouts_NeverOverlap_OnCommonScreens()
        {
            foreach (var lefty in new[] { false, true })
            foreach (var scale in GameSettings.ButtonScales)
            foreach (var (w, h) in new[] { (2340f, 1080f), (1920f, 1080f), (1280f, 800f) })
            {
                var zones = TaxiKmer.Core.Driving.ControlLayout.Adjusted(TaxiKmer.Core.Driving.ControlLayout.Default(), lefty, scale).zones;
                for (var i = 0; i < zones.Length; i++)
                {
                    var a = zones[i].Resolve(w, h);
                    Assert.GreaterOrEqual(a.X, 0f); Assert.LessOrEqual(a.X + a.Width, w);
                    Assert.GreaterOrEqual(a.Y, 0f); Assert.LessOrEqual(a.Y + a.Height, h);
                    for (var j = i + 1; j < zones.Length; j++)
                    {
                        var b = zones[j].Resolve(w, h);
                        var overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                        Assert.IsFalse(overlap, $"{zones[i].control} / {zones[j].control} à {w}×{h}, gaucher {lefty}, taille {scale}");
                    }
                }
            }
        }

        [Test]
        public void ButtonSizeAndOpacity_Cycle_AndPersist()
        {
            var s = new GameSettings();
            Assert.AreEqual("Boutons : 100 %", s.ButtonScaleLabel);
            s.Cycle(SettingIds.ButtonScale);
            Assert.AreEqual(1.15f, s.ButtonScale);
            s.Cycle(SettingIds.ButtonOpacity);
            s.Cycle(SettingIds.ButtonOpacity);
            Assert.AreEqual("Opacité des boutons : 50 %", s.ButtonOpacityLabel);
            s.Cycle(SettingIds.LeftHanded);
            var store = new MemoryStore();
            s.Save(store);
            var back = new GameSettings();
            back.Load(store);
            Assert.IsTrue(back.LeftHanded);
            Assert.AreEqual(1.15f, back.ButtonScale);
            Assert.AreEqual(0.5f, back.ButtonOpacity);
        }

        [Test]
        public void SeparateVolumes_CycleDown_ToMuted_AndPersist()
        {
            var s = new GameSettings();
            Assert.AreEqual(1f, s.RadioVolume);
            var labels = s.Rows().Select(r => r.Label).ToList();
            CollectionAssert.Contains(labels, "Radio : 100 %");
            CollectionAssert.Contains(labels, "Rue : 100 %");
            for (var i = 0; i < 4; i++) s.Cycle(SettingIds.RadioVolume);
            Assert.AreEqual(0f, s.RadioVolume);
            CollectionAssert.Contains(s.Rows().Select(r => r.Label).ToList(), "Radio : coupé");
            s.Cycle(SettingIds.VoiceVolume);
            s.Cycle(SettingIds.StreetVolume);
            s.Cycle(SettingIds.StreetVolume);
            var store = new MemoryStore();
            s.Save(store);
            var back = new GameSettings();
            back.Load(store);
            Assert.AreEqual(0f, back.RadioVolume);
            Assert.AreEqual(0.75f, back.VoiceVolume);
            Assert.AreEqual(1f, back.EffectsVolume);
            Assert.AreEqual(0.5f, back.StreetVolume);
            s.Cycle(SettingIds.RadioVolume);
            Assert.AreEqual(1f, s.RadioVolume, "après coupé, retour à 100 %");
        }
    }
}
