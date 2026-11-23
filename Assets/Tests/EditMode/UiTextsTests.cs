using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class UiTextsTests
    {
        [Test]
        public void Fill_ReplacesNamedValues_KeepsUnknown()
        {
            var t = new UiTextsConfig();
            Assert.AreEqual("OK pour Emana (600 FCFA) : arrête-toi", UiText.Fill(t.honkAccepted, "lieu", "Emana", "prix", "600 FCFA"));
            Assert.AreEqual("Faire descendre (2)", UiText.Fill(t.dropMany, "n", "2"));
            Assert.AreEqual("vers {lieu}", UiText.Fill(t.towards), "sans valeur, le nom reste visible");
            Assert.AreEqual("Nouvelle carrière. Jour 1, lundi : le patron te veut sur la route de 6 h à 20 h",
                UiText.Fill(t.dayStart, "carriere", t.newCareer, "jour", "1", "semaine", "lundi", "debut", "6", "fin", "20"));
        }
    }
}
