using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class CabinPlanTests
    {
        private static Client C(string id, Corpulence c) => new Client(id, "etoudi", 1000, 300, c);

        [Test]
        public void TwoSeatsInFront_ThreeBehind_FilledInOrder()
        {
            var cabin = new TaxiCabin(new SeatingConfig());
            cabin.TryBoard(C("a", Corpulence.Normal), out _);
            var seats = CabinPlan.Seats(cabin);
            Assert.AreEqual(5, seats.Count);
            Assert.AreEqual(2, seats.Count(s => s.Bench == BenchSide.Front));
            Assert.AreEqual(3, seats.Count(s => s.Bench == BenchSide.Back));
            var occupied = seats.Single(s => s.Passenger != null);
            Assert.AreEqual(BenchSide.Back, occupied.Bench, "L'arrière se remplit d'abord.");
            Assert.AreEqual(0, occupied.Index);
            Assert.IsFalse(seats.Any(s => s.Blocking));
        }

        [Test]
        public void ClientWhoDoesNotFit_FlagsTheFreeSeatsThatAreTooNarrow()
        {
            var cabin = new TaxiCabin(new SeatingConfig());
            // Derrière : deux costauds (8/9), devant : un costaud (4/6). Il reste une place de chaque côté, trop étroite.
            cabin.TryBoard(C("b1", Corpulence.Costaud), out _);
            cabin.TryBoard(C("b2", Corpulence.Costaud), out _);
            cabin.TryBoard(C("f1", Corpulence.Costaud), out _);
            var normal = C("n", Corpulence.Normal);
            Assert.IsFalse(cabin.CanBoard(normal));
            var seats = CabinPlan.Seats(cabin, normal);
            Assert.AreEqual(2, seats.Count(s => s.Blocking), "La place libre avant et la place libre arrière coincent.");
            Assert.IsTrue(seats.Where(s => s.Blocking).All(s => s.Passenger == null));

            var mince = C("m", Corpulence.Mince);
            var forMince = CabinPlan.Seats(cabin, mince).Where(s => s.Blocking).ToList();
            Assert.AreEqual(1, forMince.Count, "Un mince rentre devant : seule la place arrière coince.");
            Assert.AreEqual(BenchSide.Back, forMince[0].Bench);
        }

        [Test]
        public void SilhouettesGrowWithCorpulence()
        {
            Assert.Less(CabinPlan.SilhouetteWidth(Corpulence.Mince), CabinPlan.SilhouetteWidth(Corpulence.Normal));
            Assert.Less(CabinPlan.SilhouetteWidth(Corpulence.Normal), CabinPlan.SilhouetteWidth(Corpulence.Costaud));
        }
    }
}
