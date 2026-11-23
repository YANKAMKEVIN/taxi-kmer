using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Tests
{
    public class DailyGoalsTests
    {
        private static DailyGoals With(params GoalData[] goals) =>
            new DailyGoals(new DailyGoalsConfig { goals = goals, minPerDay = goals.Length, maxPerDay = goals.Length, rewardsEnabled = true }, 1, 1);

        [Test]
        public void Draw_OneToThree_Distinct_StablePerDay()
        {
            var cfg = new DailyGoalsConfig();
            for (var day = 1; day < 60; day++)
            {
                var a = new DailyGoals(cfg, day, 7);
                Assert.That(a.Goals.Count, Is.InRange(1, 3));
                Assert.AreEqual(a.Goals.Count, a.Goals.Select(g => g.Data.id).Distinct().Count());
                CollectionAssert.AreEqual(a.Goals.Select(g => g.Data.id), new DailyGoals(cfg, day, 7).Goals.Select(g => g.Data.id));
            }
            Assert.AreEqual(0, new DailyGoals(new DailyGoalsConfig { enabled = false }, 1, 1).Goals.Count);
        }

        [Test]
        public void DropsBefore_CountsOnlyEarlyRides()
        {
            var goals = With(new GoalData { id = "m", kind = GoalKinds.DropsBefore, label = "Déposer {n} passagers avant {heure}", count = 2, beforeTime = "08:00" });
            goals.RidePaid(7 * 60);
            Assert.AreEqual("1/2 Déposer 2 passagers avant 08:00", goals.Goals[0].Line);
            goals.RidePaid(8 * 60);
            Assert.IsFalse(goals.Goals[0].Done, "08:00 passé");
            goals.RidePaid(7 * 60 + 59);
            Assert.IsTrue(goals.Goals[0].Done);
            Assert.AreSame(goals.Goals[0], goals.TakeJustDone());
            Assert.IsNull(goals.TakeJustDone());
        }

        [Test]
        public void NoPoliceAndNoCrash_HoldUntilTheEnd_OrFail()
        {
            var goals = With(new GoalData { id = "h", kind = GoalKinds.NoPolicePaid, label = "Ne rien payer" },
                new GoalData { id = "p", kind = GoalKinds.NoCrash, label = "Aucun accrochage" });
            goals.PolicePaid(0);
            goals.Crash();
            goals.EndDay();
            Assert.IsTrue(goals.Goals[0].Done);
            Assert.IsTrue(goals.Goals[1].Failed);
            StringAssert.StartsWith("Objectifs du jour (1/2)", goals.Summary());
        }

        [Test]
        public void RevenueAndFullCabin_AndRewardsOnlyWhenEnabled()
        {
            var goals = With(new GoalData { id = "r", kind = GoalKinds.Revenue, label = "Faire {montant}", amount = 5000, reward = 300 },
                new GoalData { id = "f", kind = GoalKinds.FullCabin, label = "{n} à bord", count = 5, reward = 200 });
            goals.Revenue(4999);
            goals.Passengers(4);
            Assert.AreEqual(0, goals.DoneCount);
            goals.Revenue(5000);
            goals.Passengers(5);
            goals.EndDay();
            Assert.AreEqual(500, goals.Reward);
            Assert.AreEqual("Faire 5 000 FCFA", goals.Goals[0].Label);
            var noReward = new DailyGoals(new DailyGoalsConfig { goals = new[] { new GoalData { kind = GoalKinds.NoCrash, reward = 200 } }, minPerDay = 1, maxPerDay = 1, rewardsEnabled = false }, 1, 1);
            noReward.EndDay();
            Assert.AreEqual(0, noReward.Reward, "primes coupées");
            Assert.IsTrue(new DailyGoalsConfig().rewardsEnabled, "primes activées par Kevin le 23/11 au soir");
        }

        [Test]
        public void Bonus_GoesToSavings()
        {
            var career = new TaxiKmer.Core.Career.DriverCareer(new TaxiKmer.Core.Career.OwnerConfig());
            career.AddBonus(300);
            Assert.AreEqual(300, career.Savings);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => career.AddBonus(-1));
        }
    }
}
