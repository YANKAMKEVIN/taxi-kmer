using System;
using NUnit.Framework;
using TaxiKmer.Core.Police;

namespace TaxiKmer.Core.Tests
{
    public class PoliceChaseTests
    {
        [Test]
        public void StoppedTaxi_IsCaught()
        {
            var chase = new PoliceChase(new ChaseData(), 1000f, 1);
            Assert.AreEqual(965f, chase.PoliceAlong, 1e-3f, "part derrière le taxi");
            var state = ChaseState.Running;
            for (var i = 0; i < 600 && state == ChaseState.Running; i++) state = chase.Update(0.05f, 1000f);
            Assert.AreEqual(ChaseState.Caught, state);
            Assert.Less(chase.Elapsed, 10f);
        }

        [Test]
        public void FastTaxi_Escapes_ByDistance()
        {
            var chase = new PoliceChase(new ChaseData(), 1000f, 1);
            float along = 1000f;
            var state = ChaseState.Running;
            for (var i = 0; i < 2000 && state == ChaseState.Running; i++)
            {
                along += 90f / 3.6f * 0.05f; // 90 km/h contre 60
                state = chase.Update(0.05f, along);
            }
            Assert.AreEqual(ChaseState.Escaped, state);
            Assert.GreaterOrEqual(chase.Gap, 220f);
        }

        [Test]
        public void HoldingOn_UntilTheEnd_Escapes()
        {
            var data = new ChaseData { maxSeconds = 10f };
            var chase = new PoliceChase(data, 0f, -1);
            Assert.AreEqual(35f, chase.PoliceAlong, 1e-3f, "vers Messassi : la police est du côté d'Étoudi");
            float along = 0f;
            var state = ChaseState.Running;
            for (var i = 0; i < 400 && state == ChaseState.Running; i++)
            {
                along -= 60f / 3.6f * 0.05f; // même vitesse : l'écart ne se referme pas
                state = chase.Update(0.05f, along);
            }
            Assert.AreEqual(ChaseState.Escaped, state);
            Assert.AreEqual(10f, chase.Elapsed, 0.06f);
        }

        private static PoliceControl Invented(PoliceConfig cfg)
        {
            cfg.inventChanceWhenInOrder = 1f;
            var papers = new PaperSet(cfg);
            return new PoliceControl(cfg, papers, 1, 1, 0, new Random(3));
        }

        [Test]
        public void Flee_StartsAChase_ThenTheChaseDecides()
        {
            var cfg = new PoliceConfig();
            var caught = Invented(cfg);
            Assert.IsTrue(caught.Choose(ControlChoice.Flee));
            Assert.IsTrue(caught.Chasing);
            Assert.IsFalse(caught.Closed);
            CollectionAssert.IsEmpty(caught.AvailableChoices(), "plus de boutons pendant la poursuite");
            caught.EndChase(true);
            Assert.IsTrue(caught.Closed);
            Assert.IsTrue(caught.Impounded);
            Assert.AreEqual("rattrape", caught.OutcomeId);

            var escaped = Invented(new PoliceConfig());
            escaped.Choose(ControlChoice.Flee);
            escaped.EndChase(false);
            Assert.IsTrue(escaped.BecameWanted);
            Assert.IsFalse(escaped.Impounded);
        }

        [Test]
        public void ChaseDisabled_KeepsTheOldDraw()
        {
            var cfg = new PoliceConfig();
            cfg.chase.enabled = false;
            var c = Invented(cfg);
            c.Choose(ControlChoice.Flee);
            Assert.IsFalse(c.Chasing);
            Assert.IsTrue(c.Closed);
        }
    }
}
