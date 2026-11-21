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
        public void Silent_OutsideTheTaxi_DuringDialogue_OrWhenDisabled()
        {
            var r = new CarRadio(Config());
            Assert.IsFalse(r.Audible, "Coupée.");
            r.NextStation();
            Assert.IsTrue(r.Audible);
            r.InTaxi = false;
            Assert.IsFalse(r.Audible, "Dehors (pause, bilan) : silence.");
            r.InTaxi = true;
            r.DialogueActive = true;
            Assert.IsFalse(r.Audible, "Un passager parle : silence.");
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
            Assert.IsFalse(r.HasSound);
        }
    }
}
