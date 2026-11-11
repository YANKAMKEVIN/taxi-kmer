using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class ClientFactoryTests
    {
        private static StopsConfig Stops()
        {
            return new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 1500 },
                    new Stop { id = "c", name = "C", distanceAlongRouteMeters = 5000 },
                },
            };
        }

        [Test]
        public void DestinationIsNeverThePickupStop_AndFareMatchesDistance()
        {
            var factory = new ClientFactory(Stops(), FareConfig.Default(), 42);
            for (var i = 0; i < 200; i++)
            {
                var c = factory.CreateAt("b");
                Assert.AreNotEqual("b", c.DestinationStopId);
                var expected = c.DestinationStopId == "a" ? 1500 : 3500;
                Assert.AreEqual(expected, c.DistanceMeters);
                Assert.AreEqual(FareConfig.Default().FareFor(expected), c.ProposedFare);
            }
        }

        [Test]
        public void SameSeedSameClients()
        {
            var f1 = new ClientFactory(Stops(), FareConfig.Default(), 7);
            var f2 = new ClientFactory(Stops(), FareConfig.Default(), 7);
            for (var i = 0; i < 20; i++)
            {
                var c1 = f1.CreateAt("a");
                var c2 = f2.CreateAt("a");
                Assert.AreEqual(c1.DestinationStopId, c2.DestinationStopId);
                Assert.AreEqual(c1.Corpulence, c2.Corpulence);
            }
        }

        [Test]
        public void AllCorpulencesAppear_AndIdsAreUnique()
        {
            var factory = new ClientFactory(Stops(), FareConfig.Default(), 1);
            var seen = new HashSet<Corpulence>();
            var ids = new HashSet<string>();
            for (var i = 0; i < 300; i++)
            {
                var c = factory.CreateAt("a");
                seen.Add(c.Corpulence);
                Assert.IsTrue(ids.Add(c.Id));
            }
            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void UnknownStopThrows()
        {
            var factory = new ClientFactory(Stops(), FareConfig.Default(), 1);
            Assert.Throws<System.ArgumentException>(() => factory.CreateAt("zz"));
        }
    }
}
