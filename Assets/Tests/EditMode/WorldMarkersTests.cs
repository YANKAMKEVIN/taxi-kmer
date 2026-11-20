using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class WorldMarkersTests
    {
        [Test]
        public void DestinationLabel_NamesTheStopAndCountsPassengers()
        {
            Assert.AreEqual("Emana · 1 passager", WorldMarkers.DestinationLabel("Emana", 1));
            Assert.AreEqual("Étoudi · 3 passagers", WorldMarkers.DestinationLabel("Étoudi (devant le palais de la présidence)", 3));
        }

        [Test]
        public void RingPulse_StaysAroundOne()
        {
            for (var t = 0f; t < 3f; t += 0.05f)
                Assert.That(WorldMarkers.RingPulse(t), Is.InRange(0.85f, 1.15f));
            Assert.AreNotEqual(WorldMarkers.RingPulse(0f), WorldMarkers.RingPulse(0.2f), "Elle bouge.");
        }
    }
}
