using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Radio;

namespace TaxiKmer.Core.Tests
{
    public class RadioTests
    {
        private static RadioConfig Config(bool enabled = true) => new RadioConfig
        {
            enabled = enabled,
            defaultProgramSeconds = 100f,
            stations = new[]
            {
                new RadioStationData
                {
                    id = "s1", name = "Station 1", frequencyMHz = 0f,
                    programmes = new[]
                    {
                        new RadioProgramData { id = "a", title = "Piste A", clip = "" },
                        new RadioProgramData { id = "b", title = "Pub B", kind = "publicite", clip = "Radio/b", seconds = 30f },
                    },
                },
                new RadioStationData
                {
                    id = "s2", name = "Radio Essai", frequencyMHz = 94.5f,
                    programmes = new[] { new RadioProgramData { id = "c", title = "Émission C", kind = "emission", clip = "Radio/c" } },
                },
            },
        };

        [Test]
        public void Button_CyclesOff_Station1_Station2_Off()
        {
            var r = new CarRadio(Config());
            Assert.IsFalse(r.On);
            Assert.AreEqual("Radio coupée", r.ButtonLabel);
            Assert.AreEqual("s1", r.NextStation().id);
            Assert.AreEqual("Station 1 · FM ?", r.ButtonLabel, "Fréquence à fournir.");
            Assert.AreEqual("s2", r.NextStation().id);
            Assert.AreEqual("Radio Essai · 94,5 FM", r.ButtonLabel);
            Assert.IsNull(r.NextStation());
            Assert.IsFalse(r.On);
            r.NextStation();
            r.TurnOff();
            Assert.IsFalse(r.On);
            Assert.IsNull(new CarRadio(new RadioConfig { enabled = true }).NextStation(), "Sans station : reste coupée.");
        }

        [Test]
        public void StartsOn_FromTheData()
        {
            var c = Config();
            c.startsOn = true;
            Assert.AreEqual(0, new CarRadio(c).StationIndex);
        }

        [Test]
        public void Stations_AreLive_TheScheduleRunsEvenWhenNotListening()
        {
            var r = new CarRadio(Config());
            r.NextStation();
            Assert.AreEqual("a", r.CurrentProgramme.id);
            r.Update(60f);
            Assert.AreEqual(60f, r.ProgrammeOffset, 1e-3);
            r.TurnOff();
            r.Update(50f); // coupée : le direct continue
            r.NextStation();
            Assert.AreEqual("b", r.CurrentProgramme.id, "On retombe au milieu de la grille.");
            Assert.AreEqual(10f, r.ProgrammeOffset, 1e-3);
            Assert.AreEqual(30f, r.DurationOf(r.CurrentProgramme));
            r.Update(20f);
            Assert.AreEqual("a", r.CurrentProgramme.id, "La grille tourne en boucle.");
            Assert.AreEqual("Piste A", r.NowPlaying);
        }

        [Test]
        public void Silent_OutsideTheTaxi_OrWhenDisabled_LowerDuringDialogue()
        {
            var r = new CarRadio(Config());
            Assert.IsFalse(r.Audible, "Coupée.");
            r.NextStation();
            Assert.IsTrue(r.Audible);
            r.InTaxi = false;
            Assert.IsFalse(r.Audible, "Dehors (pause, bilan) : silence.");
            Assert.AreEqual(0f, r.Volume);
            r.InTaxi = true;
            Assert.AreEqual(0.6f, r.Volume, 1e-5);
            r.DialogueActive = true;
            Assert.IsTrue(r.Audible, "Un passager parle : le volume baisse au lieu du silence (Kevin, 21/11).");
            Assert.AreEqual(0.6f * 0.3f, r.Volume, 1e-5);
            r.DialogueActive = false;
            Assert.IsTrue(r.Audible);

            var off = new CarRadio(Config(enabled: false));
            off.NextStation();
            Assert.IsFalse(off.Audible, "Radio coupée dans les données (enabled: false).");
        }

        [Test]
        public void EmptySlots_StaySilent_ProvidedClipsPlay()
        {
            var r = new CarRadio(Config());
            r.NextStation();
            Assert.IsFalse(r.HasSound, "Piste A : emplacement vide, à fournir par Kevin.");
            r.Update(100f);
            Assert.IsTrue(r.HasSound, "Pub B : un son est donné.");
            r.DialogueActive = true;
            Assert.IsTrue(r.HasSound, "Plus bas, mais toujours là.");
            r.InTaxi = false;
            Assert.IsFalse(r.HasSound);
        }

        /// <summary>Kevin, 21/11 : avec des pistes vides, la radio diffuse seulement les annonces et les flashs, en texte.</summary>
        [Test]
        public void EmptyTracks_OnlyAnnouncements_AsTextOnScreen()
        {
            var c = new RadioConfig
            {
                enabled = true,
                skipSilentProgrammes = true,
                defaultProgramSeconds = 100f,
                stations = new[]
                {
                    new RadioStationData
                    {
                        id = "s", name = "Nkol FM", frequencyMHz = 94.1f,
                        programmes = new[]
                        {
                            new RadioProgramData { id = "m", title = "Musique", clip = "" },
                            new RadioProgramData { id = "a", kind = "emission", text = "Bonjour Yaoundé !", seconds = 20f },
                            new RadioProgramData { id = "m2", title = "Musique 2", clip = "" },
                            new RadioProgramData { id = "p", kind = "publicite", text = "Garage Bonne Route", seconds = 20f },
                        },
                    },
                },
            };
            var r = new CarRadio(c);
            r.NextStation();
            Assert.AreEqual("a", r.CurrentProgramme.id, "Les musiques sans son sont sautées.");
            Assert.AreEqual("Bonjour Yaoundé !", r.ScreenText);
            r.Update(25f);
            Assert.AreEqual("p", r.CurrentProgramme.id);
            Assert.AreEqual("Garage Bonne Route", r.ScreenText);
            Assert.IsFalse(r.HasSound, "Aucun son : texte seulement.");
            r.InTaxi = false;
            Assert.AreEqual("", r.ScreenText);
            c.skipSilentProgrammes = false;
            var all = new CarRadio(c);
            all.NextStation();
            Assert.AreEqual("m", all.CurrentProgramme.id, "Sans la règle, la grille garde les pistes vides.");
        }

        [Test]
        public void Flash_FromGameEvents_PassesBeforeTheProgramme_ThenGoes()
        {
            var c = Config();
            c.flashSeconds = 10f;
            c.flashes = new[]
            {
                new RadioFlashData { id = "p1", @event = RadioEvents.Rain, text = "La pluie arrive." },
                new RadioFlashData { id = "c1", @event = RadioEvents.Control, text = "Contrôle signalé {lieu}." },
                new RadioFlashData { id = "c2", @event = RadioEvents.Control, text = "Encore un contrôle {lieu}." },
            };
            var r = new CarRadio(c);
            r.NextStation();
            Assert.AreEqual("Contrôle signalé à Messassi.", r.PushEvent(RadioEvents.Control, "à Messassi"));
            Assert.AreEqual("Flash info : Contrôle signalé à Messassi.", r.ScreenText);
            Assert.AreEqual("Encore un contrôle sur la route.", r.PushEvent(RadioEvents.Control), "Les flashs tournent ; sans lieu : « sur la route ».");
            r.Update(11f);
            Assert.IsFalse(r.FlashActive);
            Assert.AreEqual("", r.ScreenText, "Piste A : musique sans texte.");
            Assert.AreEqual("", r.PushEvent(RadioEvents.TrafficJam), "Pas de flash pour cet événement dans les données.");
            Assert.AreEqual("", new CarRadio(Config(enabled: false)).PushEvent(RadioEvents.Rain));
        }

        [Test]
        public void NewsDesk_TurnsGameEventsIntoNews_Once()
        {
            var desk = new RadioNewsDesk();
            var controls = new List<(string, string)>();
            var n = desk.Update(6 * 60, false, true, "marché de Messassi", controls, "vers Étoudi");
            Assert.AreEqual(1, n.Count);
            Assert.AreEqual(RadioEvents.Market, n[0].Event);
            Assert.AreEqual(0, desk.Update(6 * 60 + 5, false, true, "marché de Messassi", controls, "vers Étoudi").Count, "Une seule fois.");
            n = desk.Update(7 * 60 + 10, false, true, "", controls, "vers Étoudi");
            Assert.AreEqual(RadioEvents.TrafficJam, n.Single().Event, "Heure de pointe : embouteillages.");
            Assert.AreEqual("vers Étoudi", n.Single().Place);
            controls.Add(("volant-1", "à Emana"));
            n = desk.Update(8 * 60, true, true, "", controls, "");
            CollectionAssert.AreEquivalent(new[] { RadioEvents.Rain, RadioEvents.Control }, n.Select(x => x.Event).ToArray());
            Assert.AreEqual("à Emana", n.Single(x => x.Event == RadioEvents.Control).Place);
            Assert.AreEqual(0, desk.Update(8 * 60 + 1, true, true, "", controls, "").Count, "Pluie et contrôle déjà annoncés.");
            Assert.AreEqual(RadioEvents.TrafficJam, desk.Update(17 * 60, false, true, "", controls, "").Single().Event, "Pointe du soir.");
            Assert.AreEqual(RadioEvents.Rain, desk.Update(18 * 60, true, true, "", controls, "").Single().Event, "Nouvelle averse.");
        }
    }
}
