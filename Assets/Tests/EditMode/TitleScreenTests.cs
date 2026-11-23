using NUnit.Framework;
using TaxiKmer.Core.Save;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class TitleScreenTests
    {
        [Test]
        public void FirstLaunch_StartsTheDay()
        {
            var data = new SaveData();
            Assert.IsFalse(TitleScreen.HasCareer(data));
            Assert.AreEqual("Commencer la journée", TitleScreen.PrimaryLabel(data));
            Assert.AreEqual("Une journée de taxi, de Messassi à Étoudi", TitleScreen.Subtitle(data));
        }

        [Test]
        public void ExistingCareer_OffersToContinue_WithDaySavingsAndReputation()
        {
            var data = new SaveData();
            data.career.dayNumber = 12;
            data.career.savings = 34500;
            data.career.reputation = 62;
            Assert.IsTrue(TitleScreen.HasCareer(data));
            Assert.AreEqual("Continuer", TitleScreen.PrimaryLabel(data));
            Assert.AreEqual("Jour 12 · épargne 34 500 FCFA · réputation 62", TitleScreen.Subtitle(data));
            data.career.reputation = -1; // réputation jamais enregistrée
            StringAssert.DoesNotContain("réputation", TitleScreen.Subtitle(data));
        }

        [Test]
        public void NewCareer_NeedsASecondTap()
        {
            var menu = new GameMenu(true);
            Assert.IsFalse(menu.RequestNewCareer());
            Assert.IsTrue(menu.ConfirmingNewCareer);
            StringAssert.StartsWith("Confirmer", TitleScreen.NewCareerLabel(menu.ConfirmingNewCareer));
            Assert.IsTrue(menu.RequestNewCareer());
            Assert.AreEqual(MenuScreen.None, menu.Screen, "la journée commence");
            Assert.IsFalse(menu.ConfirmingNewCareer);
        }

        [Test]
        public void NewCareer_ConfirmationIsCancelledBySettings_AndImpossibleInPause()
        {
            var menu = new GameMenu(true);
            menu.RequestNewCareer();
            menu.OpenSettings();
            Assert.IsFalse(menu.ConfirmingNewCareer);
            menu.CloseSettings();
            Assert.IsFalse(menu.RequestNewCareer(), "il faut de nouveau deux appuis");
            var playing = new GameMenu(false);
            playing.Pause();
            Assert.IsFalse(playing.RequestNewCareer());
            Assert.IsFalse(playing.ConfirmingNewCareer);
        }
    }
}
