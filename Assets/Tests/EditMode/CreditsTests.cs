using NUnit.Framework;
using TaxiKmer.Core.Radio;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class CreditsTests
    {
        [Test]
        public void Lines_ListDataCredits_ThenRadioTracks_Once()
        {
            var radio = new RadioConfig
            {
                stations = new[]
                {
                    new RadioStationData
                    {
                        id = "a",
                        programmes = new[]
                        {
                            new RadioProgramData { id = "m1", title = "Makossa du matin", clip = "m1", credit = "Groupe X", licence = "CC-BY 4.0", source = "freemusicarchive.org/x" },
                            new RadioProgramData { id = "t", title = "Annonce", text = "Bonjour", clip = "" },
                        },
                    },
                    new RadioStationData { id = "b", programmes = new[] { new RadioProgramData { id = "m1", title = "Makossa du matin", clip = "m1", credit = "Groupe X", licence = "CC-BY 4.0", source = "freemusicarchive.org/x" } } },
                },
            };
            var lines = Credits.Lines(new CreditsConfig(), radio);
            Assert.AreEqual("Taxi Kmer, un jeu de Kevin Yankam.", lines[0]);
            Assert.AreEqual("Routes et bâtiments de Yaoundé : © contributeurs OpenStreetMap (ODbL 1.0, openstreetmap.org/copyright)", lines[1]);
            CollectionAssert.Contains(lines, "Musiques de la radio");
            Assert.AreEqual("Makossa du matin : Groupe X (CC-BY 4.0, freemusicarchive.org/x)", lines[lines.Count - 1]);
            Assert.AreEqual(1, lines.FindAll(l => l.StartsWith("Makossa")).Count, "un morceau joué sur deux stations n'est crédité qu'une fois");
        }

        [Test]
        public void NoTracks_NoMusicTitle()
        {
            CollectionAssert.DoesNotContain(Credits.Lines(new CreditsConfig(), new RadioConfig()), "Musiques de la radio");
        }

        [Test]
        public void Menu_CreditsFromPauseOrTitle_BackWhereWeCameFrom()
        {
            var title = new GameMenu(true);
            Assert.IsTrue(title.OpenCredits());
            Assert.IsFalse(title.TimeStopped, "depuis l'écran titre, le décor vit");
            title.Back();
            Assert.AreEqual(MenuScreen.Title, title.Screen);
            var game = new GameMenu(false);
            Assert.IsFalse(game.OpenCredits(), "pas en conduisant");
            game.Pause();
            game.OpenCredits();
            Assert.IsTrue(game.TimeStopped);
            game.Back();
            Assert.AreEqual(MenuScreen.Pause, game.Screen);
        }
    }
}
