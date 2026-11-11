using NUnit.Framework;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class FareTests
    {
        [TestCase(0, 300)]
        [TestCase(2000, 300)]
        [TestCase(2001, 400)]
        [TestCase(7000, 500)]
        [TestCase(10000, 600)]
        [TestCase(15000, 800)]
        public void DefaultBands(int meters, int expected)
        {
            Assert.AreEqual(expected, FareConfig.Default().FareFor(meters));
        }

        [Test]
        public void FaresStayBetween300And800()
        {
            var fares = FareConfig.Default();
            for (var m = 0; m <= 30000; m += 250)
            {
                var f = fares.FareFor(m);
                Assert.That(f, Is.InRange(300, 800));
            }
        }
    }
}
