using NUnit.Framework;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class SeatingTests
    {
        private static int _n;
        private static Client C(Corpulence c) => new Client("c" + (++_n), "emana", 1000, 300, c);

        private TaxiCabin _cabin;

        [SetUp]
        public void SetUp() => _cabin = new TaxiCabin(new SeatingConfig());

        [Test]
        public void FiveNormalPassengersFit_ThreeBackTwoFront()
        {
            for (var i = 0; i < 5; i++) Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Normal), out _));
            Assert.AreEqual(3, _cabin.Back.Passengers.Count);
            Assert.AreEqual(2, _cabin.Front.Passengers.Count);
            Assert.IsFalse(_cabin.TryBoard(C(Corpulence.Mince), out _), "Pas de sixième passager.");
        }

        [Test]
        public void BackIsFilledBeforeFront()
        {
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Normal), out var side));
            Assert.AreEqual(BenchSide.Back, side);
        }

        [Test]
        public void Front_CostaudPlusMince_Fits()
        {
            var front = _cabin.Front;
            FillBack();
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out var s1));
            Assert.AreEqual(BenchSide.Front, s1);
            Assert.IsTrue(front.CanSeat(C(Corpulence.Mince)));
        }

        [Test]
        public void Front_CostaudPlusNormal_DoesNotFit()
        {
            FillBack();
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out _));
            Assert.IsFalse(_cabin.TryBoard(C(Corpulence.Normal), out _));
        }

        [Test]
        public void Front_TwoCostauds_DoNotFit()
        {
            FillBack();
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out _));
            Assert.IsFalse(_cabin.TryBoard(C(Corpulence.Costaud), out _));
        }

        [Test]
        public void Back_TwoCostaudsPlusMince_DoesNotFit_MinceGoesFront()
        {
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out _));
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out _));
            Assert.IsFalse(_cabin.Back.CanSeat(C(Corpulence.Mince)));
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Mince), out var side));
            Assert.AreEqual(BenchSide.Front, side);
        }

        [Test]
        public void Back_CostaudNormalMince_Fits()
        {
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out _));
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Normal), out _));
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Mince), out var side));
            Assert.AreEqual(BenchSide.Back, side);
        }

        [Test]
        public void SameClientCannotBoardTwice()
        {
            var c = C(Corpulence.Mince);
            Assert.IsTrue(_cabin.TryBoard(c, out _));
            Assert.IsFalse(_cabin.TryBoard(c, out _));
            Assert.AreEqual(1, _cabin.PassengerCount);
        }

        [Test]
        public void ClientWhoDoesNotFit_CanBeSeatedAfterMovingAPassenger()
        {
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out _));
            var mince = C(Corpulence.Mince);
            Assert.IsTrue(_cabin.TryBoard(mince, out _));
            Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Costaud), out var s3));
            Assert.AreEqual(BenchSide.Front, s3);

            var newcomer = C(Corpulence.Costaud);
            Assert.IsFalse(_cabin.CanBoard(newcomer), "Sans changement de place, il ne rentre pas.");
            Assert.AreSame(mince, _cabin.FindMoveToFit(newcomer), "Le mince passe devant pour libérer l'arrière.");
            Assert.IsFalse(_cabin.CanBoard(newcomer), "La recherche ne modifie pas l'habitacle.");

            Assert.IsTrue(_cabin.TryMove(mince.Id));
            Assert.AreSame(_cabin.Front, _cabin.BenchOf(mince));
            Assert.IsTrue(_cabin.TryBoard(newcomer, out var side));
            Assert.AreEqual(BenchSide.Back, side);
        }

        [Test]
        public void FindMoveToFit_ReturnsNull_WhenNoMoveHelps()
        {
            for (var i = 0; i < 5; i++) Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Normal), out _));
            Assert.IsNull(_cabin.FindMoveToFit(C(Corpulence.Mince)));
        }

        [Test]
        public void FindMoveToFit_EveryCabin_NeverThrows_NeverChangesTheCabin()
        {
            // Toutes les façons de remplir le taxi (carrures tirées au hasard), puis chaque nouveau client possible :
            // la recherche ne doit ni planter (elle déplaçait les passagers pendant qu'elle les parcourait), ni rien changer.
            var rng = new System.Random(3);
            var kinds = new[] { Corpulence.Mince, Corpulence.Normal, Corpulence.Costaud };
            var searches = 0;
            for (var trial = 0; trial < 400; trial++)
            {
                var cabin = new TaxiCabin(new SeatingConfig());
                for (var i = 0; i < 6; i++) cabin.TryBoard(C(kinds[rng.Next(3)]), out _);
                var back = string.Join(",", System.Linq.Enumerable.Select(cabin.Back.Passengers, x => x.Id));
                var front = string.Join(",", System.Linq.Enumerable.Select(cabin.Front.Passengers, x => x.Id));
                foreach (var k in kinds)
                {
                    var newcomer = C(k);
                    Client move = null;
                    Assert.DoesNotThrow(() => move = cabin.FindMoveToFit(newcomer));
                    searches++;
                    Assert.AreEqual(back, string.Join(",", System.Linq.Enumerable.Select(cabin.Back.Passengers, x => x.Id)));
                    Assert.AreEqual(front, string.Join(",", System.Linq.Enumerable.Select(cabin.Front.Passengers, x => x.Id)));
                    if (move != null)
                    {
                        Assert.IsFalse(cabin.CanBoard(newcomer));
                        var copy = new TaxiCabin(new SeatingConfig());
                        foreach (var p in cabin.Back.Passengers) copy.Restore(p, BenchSide.Back);
                        foreach (var p in cabin.Front.Passengers) copy.Restore(p, BenchSide.Front);
                        Assert.IsTrue(copy.TryMove(move.Id));
                        Assert.IsTrue(copy.CanBoard(newcomer), "Le changement proposé libère vraiment la place.");
                    }
                }
            }
            Assert.Greater(searches, 0);
        }

        [Test]
        public void FindMoveToFit_FirstTryFails_SecondHelps()
        {
            // Derrière : costaud, mince ; devant : costaud. Un costaud arrive : le costaud de derrière ne peut pas passer
            // devant (essai refusé sans déplacement) ; le mince, si → la recherche continue après un essai raté.
            var c1 = C(Corpulence.Costaud);
            var m = C(Corpulence.Mince);
            var c2 = C(Corpulence.Costaud);
            Assert.IsTrue(_cabin.Restore(c1, BenchSide.Back));
            Assert.IsTrue(_cabin.Restore(m, BenchSide.Back));
            Assert.IsTrue(_cabin.Restore(c2, BenchSide.Front));
            var normal = C(Corpulence.Normal);
            Assert.IsTrue(_cabin.Restore(normal, BenchSide.Back)); // derrière : 4 + 2 + 3 = 9, plein
            var newcomer = C(Corpulence.Mince);
            Assert.DoesNotThrow(() => _cabin.FindMoveToFit(newcomer));
        }

        [Test]
        public void TryMove_FailsWhenOtherBenchIsFull()
        {
            FillBack();
            var front = C(Corpulence.Mince);
            Assert.IsTrue(_cabin.TryBoard(front, out _));
            Assert.IsFalse(_cabin.TryMove(front.Id));
            Assert.AreSame(_cabin.Front, _cabin.BenchOf(front));
        }

        private void FillBack()
        {
            for (var i = 0; i < 3; i++) Assert.IsTrue(_cabin.TryBoard(C(Corpulence.Normal), out var s) && s == BenchSide.Back);
        }
    }
}
