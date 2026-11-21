using System;
using NUnit.Framework;
using TaxiKmer.Core.Home;

namespace TaxiKmer.Core.Tests
{
    public class HomeTests
    {
        /// <summary>Montants d'essai (les vrais sont à fixer par Kevin).</summary>
        private static HomeConfig Sample()
        {
            var c = new HomeConfig();
            foreach (var s in c.situations)
                foreach (var ch in s.charges)
                    ch.amount = ch.id == "loyer" ? 30000 : ch.id == "nourriture" ? 1500 : 10000;
            return c;
        }

        [Test]
        public void DefaultData_CostsNothing()
        {
            var h = new Household(new HomeConfig());
            Assert.IsEmpty(h.Evening(1, new Random(1)), "Montants à 0 tant que Kevin ne les a pas fixés.");
        }

        [Test]
        public void Charges_DueOnTheirDays_PerChild()
        {
            var single = new Household(Sample());
            Assert.AreEqual(-31500, Household.Total(single.ChargesDue(1)), "Le premier soir : loyer et nourriture.");
            Assert.AreEqual(-1500, Household.Total(single.ChargesDue(2)));
            Assert.AreEqual(-31500, Household.Total(single.ChargesDue(31)), "Loyer chaque mois.");

            var family = new Household(Sample(), "famille");
            Assert.AreEqual(-(30000 + 1500 + 2 * 10000), Household.Total(family.ChargesDue(1)));
            Assert.IsTrue(single.ChangeSituation("famille"));
            Assert.IsFalse(single.ChangeSituation("inconnue"));
            Assert.Throws<ArgumentException>(() => new Household(Sample(), "inconnue"));
        }

        [Test]
        public void LifeEvents_ChildrenOnly_Cooldown()
        {
            var c = Sample();
            c.lifeEvents[0].chancePerDay = 1f;
            c.lifeEvents[0].cost = 15000;
            Assert.IsEmpty(new Household(c).Events(1, new Random(1)), "Pas d'enfant : pas d'enfant malade.");
            var family = new Household(c, "famille");
            Assert.AreEqual(-15000, Household.Total(family.Events(1, new Random(1))));
            Assert.IsEmpty(family.Events(10, new Random(1)), "Pas deux fois en 30 jours.");
            Assert.IsNotEmpty(family.Events(31, new Random(1)));
        }

        [Test]
        public void Tontine_PaysEachRound_PotOnPlayerTurn_BalancesOverACycle()
        {
            var t = new TontineData { join = true, members = 4, contribution = 5000, everyDays = 7, playerTurn = 2 };
            Assert.IsEmpty(Household.Tontine(t, 6));
            Assert.AreEqual(-5000, Household.Total(Household.Tontine(t, 7)));
            Assert.AreEqual(-5000 + 20000, Household.Total(Household.Tontine(t, 14)), "Tour 2 : le pot.");
            var total = 0;
            for (var day = 1; day <= 28; day++) total += Household.Total(Household.Tontine(t, day));
            Assert.AreEqual(0, total, "Sur un cycle complet, on reçoit ce qu'on a cotisé.");
            Assert.IsEmpty(Household.Tontine(new TontineData(), 7), "Pas membre : rien.");
        }

        [Test]
        public void EveningScreen_EarnedSpentSaved()
        {
            var home = new[] { new HomeLine { Label = "Nourriture", Amount = -1500 } };
            var lines = Household.EveningSummary(12000, 1000, home);
            CollectionAssert.AreEqual(new[] { "Gagné : 12 000 FCFA", "Dépensé : 12 500 FCFA", "Mis de côté : -500 FCFA" }, lines);
        }
    }
}
