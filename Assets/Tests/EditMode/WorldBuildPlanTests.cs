using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class WorldBuildPlanTests
    {
        [Test]
        public void NearestFirst_SortsByDistanceToTheStart_Stably()
        {
            var centers = new[] { 1000f, 0f, 10f, 0f, -10f, 0f, 500f, 500f };
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, WorldBuildPlan.NearestFirst(centers, 0f, 0f));
            CollectionAssert.IsEmpty(WorldBuildPlan.NearestFirst(null, 0f, 0f));
        }

        [Test]
        public void Progress_NeverGoesBack_AndReachesOneAtTheEnd()
        {
            var p = new WorldBuildProgress(10);
            p.Done(5);
            Assert.AreEqual(0.5f, p.Fraction, 1e-6f);
            p.Add(10); // les cases de maillage s'ajoutent au total
            Assert.AreEqual(0.5f, p.Fraction, 1e-6f, "ne recule pas");
            p.Done(14);
            Assert.AreEqual(0.95f, p.Fraction, 1e-6f);
            p.Done(1);
            Assert.AreEqual(1f, p.Fraction);
            Assert.AreEqual(1f, new WorldBuildProgress(0).Fraction);
        }

        [Test]
        public void LoadingText_ShowsAPercentage()
        {
            Assert.AreEqual("Chargement de Yaoundé… 0 %", WorldBuildPlan.LoadingText(0f));
            Assert.AreEqual("Chargement de Yaoundé… 42 %", WorldBuildPlan.LoadingText(0.425f));
            Assert.AreEqual("Chargement de Yaoundé… 100 %", WorldBuildPlan.LoadingText(3f));
        }
    }
}
