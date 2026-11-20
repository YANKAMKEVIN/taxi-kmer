using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class FeedbackTests
    {
        [Test]
        public void Messages_StackNewestFirst_AndExpire()
        {
            var m = new MessageStack(2);
            m.Push("+500 FCFA", 3f, 0f);
            m.Push("Monté : Emana, 400 FCFA", 3f, 0.5f);
            CollectionAssert.AreEqual(new[] { "Monté : Emana, 400 FCFA", "+500 FCFA" }, m.Visible(1f), "Le premier message n'est plus écrasé.");
            m.Push("Il ne rentre pas", 3f, 1f);
            Assert.AreEqual(2, m.Visible(1f).Count, "Deux lignes au plus.");
            Assert.AreEqual("Il ne rentre pas", m.Visible(1f)[0]);
            CollectionAssert.AreEqual(new[] { "Il ne rentre pas" }, m.Visible(3.6f), "« Monté » a expiré à 3,5 s.");
            Assert.AreEqual(0, m.Visible(10f).Count);
        }

        [Test]
        public void Messages_SameTextIsRefreshedNotDuplicated()
        {
            var m = new MessageStack(2);
            m.Push("Pas de place", 3f, 0f);
            m.Push("Pas de place", 3f, 2f);
            Assert.AreEqual(1, m.Visible(2f).Count);
            Assert.AreEqual(1, m.Visible(4.5f).Count, "Prolongé jusqu'à 5 s.");
        }

        [Test]
        public void Counter_RollsToTheNewValue()
        {
            var c = new RollingCounter(3000);
            c.SetTarget(3500);
            Assert.IsTrue(c.Rolling);
            Assert.AreEqual(3000, c.Shown);
            c.Step(RollingCounter.RollSeconds / 2f);
            Assert.Greater(c.Shown, 3000);
            Assert.Less(c.Shown, 3500);
            c.Step(RollingCounter.RollSeconds);
            Assert.AreEqual(3500, c.Shown);
            Assert.IsFalse(c.Rolling);
        }

        [Test]
        public void PaymentPop_GrowsThenFliesToTheCounter()
        {
            PaymentPop.Sample(0f, out var s0, out var t0);
            PaymentPop.Sample(0.25f, out var s1, out var t1);
            PaymentPop.Sample(PaymentPop.Duration, out var s2, out var t2);
            Assert.Less(s0, s1, "Il grossit d'abord.");
            Assert.AreEqual(0f, t0);
            Assert.AreEqual(0f, t1, "Il reste au-dessus du taxi pendant qu'il grossit.");
            Assert.AreEqual(1f, t2, 1e-6f, "Il finit sur le compteur.");
            Assert.Less(s2, s1);
            Assert.IsFalse(PaymentPop.Arrived(0.5f));
            Assert.IsTrue(PaymentPop.Arrived(PaymentPop.Duration));
        }
    }
}
