using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class DayLightTests
    {
        // Les données réelles (daylight.json : Yaoundé, soleil à l'est le matin, presque vertical à midi…) sont vérifiées
        // par tools/dotnet/DataFilesTests.cs : ce fichier est aussi compilé hors Unity, sans UnityEngine.

        [Test]
        public void Interpolates_AndClampsOutsideTheKeys()
        {
            var c = new DayLightConfig
            {
                keys = new[]
                {
                    new DayLightKey { minute = 100, elevation = 10, intensity = 0f },
                    new DayLightKey { minute = 200, elevation = 30, intensity = 1f },
                },
            };
            Assert.AreEqual(20f, DayLight.At(c, 150).Elevation, 1e-4f);
            Assert.AreEqual(0.5f, DayLight.At(c, 150).SunIntensity, 1e-4f);
            Assert.AreEqual(10f, DayLight.At(c, 0).Elevation, 1e-4f, "Avant la première clé : la première");
            Assert.AreEqual(30f, DayLight.At(c, 999).Elevation, 1e-4f, "Après la dernière : la dernière");
            Assert.AreEqual(60f, DayLight.At(null, 0).Elevation, 1e-4f, "Sans données : plein jour");
        }

        [Test]
        public void Night_MoonLights_ButTheSkyStaysDark()
        {
            var c = new DayLightConfig
            {
                moonIntensity = 0.2f,
                keys = new[] { new DayLightKey { minute = 0, elevation = -10, intensity = 0f } },
            };
            var s = DayLight.At(c, 0);
            Assert.IsTrue(s.Night);
            Assert.AreEqual(0.2f, s.SunIntensity, 1e-4f, "La lune éclaire");
            Assert.Greater(s.Elevation, 40f, "… de haut");
            Assert.AreEqual(0f, s.SkyBrightness, 1e-4f, "… mais le ciel reste noir (avant : ciel bleu clair à 20 h)");
        }
    }
}
