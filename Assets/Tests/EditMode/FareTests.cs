using NUnit.Framework;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    /// <summary>Prix ×2 des prix de Yaoundé (600 à 1 600 FCFA), tranché par Kevin le 21/11 après l'étude d'équilibrage.</summary>
    public class FareTests
    {
        [TestCase(0, 600)]
        [TestCase(2000, 600)]
        [TestCase(2001, 800)]
        [TestCase(7000, 1000)]
        [TestCase(10000, 1200)]
        [TestCase(15000, 1600)]
        public void DefaultBands(int meters, int expected)
        {
            Assert.AreEqual(expected, FareConfig.Default().FareFor(meters));
        }

        [Test]
        public void FaresStayBetween600And1600()
        {
            var fares = FareConfig.Default();
            for (var m = 0; m <= 30000; m += 250)
            {
                var f = fares.FareFor(m);
                Assert.That(f, Is.InRange(600, 1600));
            }
        }
    }
}
