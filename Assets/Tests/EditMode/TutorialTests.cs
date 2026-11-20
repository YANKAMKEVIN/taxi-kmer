using NUnit.Framework;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class TutorialTests
    {
        private static TutorialObservation Obs(float speed = 0f, bool accepted = false, int passengers = 0, int paid = 0, bool action = false)
            => new TutorialObservation { SpeedKmh = speed, HasAcceptedClient = accepted, Passengers = passengers, RidesPaid = paid, ActionVisible = action };

        [Test]
        public void FollowsTheFirstRide_ThenEndsOnTheVersement()
        {
            var t = new Tutorial(true, 10000);
            Assert.AreEqual(TutorialStep.Accelerate, t.Step);
            Assert.AreEqual(DriveControl.Accelerate, t.Highlight);

            t.Update(0.1f, Obs(speed: 4f));
            Assert.AreEqual(TutorialStep.Accelerate, t.Step, "Pas encore assez vite.");
            t.Update(0.1f, Obs(speed: 12f));
            Assert.AreEqual(TutorialStep.Honk, t.Step);
            Assert.AreEqual(DriveControl.Horn, t.Highlight);

            t.Update(0.1f, Obs(speed: 0f));
            Assert.AreEqual(TutorialStep.Honk, t.Step, "Le départ reste acquis même arrêté.");

            t.Update(0.1f, Obs(speed: 20f, accepted: true));
            Assert.AreEqual(TutorialStep.Board, t.Step);
            Assert.AreEqual(DriveControl.Brake, t.Highlight, "D'abord s'arrêter.");
            t.Update(0.1f, Obs(accepted: true, action: true));
            Assert.AreEqual(DriveControl.Action, t.Highlight, "Puis « Faire monter ».");
            StringAssert.Contains("Faire monter", t.Instruction);

            t.Update(0.1f, Obs(speed: 30f, passengers: 1));
            Assert.AreEqual(TutorialStep.Drop, t.Step);
            Assert.AreEqual(DriveControl.None, t.Highlight);
            t.Update(0.1f, Obs(passengers: 1, action: true));
            Assert.AreEqual(DriveControl.Action, t.Highlight);

            t.Update(0.1f, Obs(paid: 1));
            Assert.AreEqual(TutorialStep.Versement, t.Step);
            StringAssert.Contains("10 000 FCFA", t.Instruction);
            Assert.IsTrue(t.Active);
            t.Update(Tutorial.VersementSeconds, Obs(paid: 1));
            Assert.AreEqual(TutorialStep.Done, t.Step);
            Assert.IsFalse(t.Active);
            Assert.AreEqual("", t.Instruction);
        }

        [Test]
        public void HonkingRightAway_SkipsTheAccelerateStep()
        {
            var t = new Tutorial(true, 10000);
            t.Update(0.1f, Obs(accepted: true));
            Assert.AreEqual(TutorialStep.Board, t.Step);
        }

        [Test]
        public void MissedClient_GoesBackToHonk()
        {
            var t = new Tutorial(true, 10000);
            t.Update(0.1f, Obs(speed: 20f, accepted: true));
            Assert.AreEqual(TutorialStep.Board, t.Step);
            t.Update(0.1f, Obs(speed: 40f));
            Assert.AreEqual(TutorialStep.Honk, t.Step, "Client dépassé : il faut en klaxonner un autre.");
        }

        [Test]
        public void AlreadyDone_ShowsNothing()
        {
            var t = new Tutorial(false, 10000);
            Assert.IsFalse(t.Active);
            Assert.AreEqual(DriveControl.None, t.Highlight);
            t.Update(1f, Obs(speed: 30f));
            Assert.AreEqual(TutorialStep.Done, t.Step);
        }
    }
}
