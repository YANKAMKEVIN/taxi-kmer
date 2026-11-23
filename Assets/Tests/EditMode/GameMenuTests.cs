using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class GameMenuTests
    {
        [Test]
        public void Title_StartDay_Pause_Resume()
        {
            var m = new GameMenu(true);
            Assert.AreEqual(MenuScreen.Title, m.Screen);
            Assert.IsTrue(m.Frozen, "La journée attend sur l'écran titre.");
            Assert.IsFalse(m.Pause(), "Pas de pause depuis l'écran titre.");
            m.Back();
            Assert.AreEqual(MenuScreen.Title, m.Screen, "Retour sans effet sur l'écran titre.");
            m.StartDay();
            Assert.AreEqual(MenuScreen.None, m.Screen);
            Assert.IsFalse(m.Frozen);
            Assert.IsTrue(m.Pause());
            Assert.IsTrue(m.Frozen);
            m.Back();
            Assert.AreEqual(MenuScreen.None, m.Screen);
        }

        [Test]
        public void Settings_ReturnToWhereTheyWereOpened()
        {
            var m = new GameMenu(true);
            m.OpenSettings();
            Assert.AreEqual(MenuScreen.Settings, m.Screen);
            Assert.IsFalse(m.TimeStopped, "Réglages ouverts depuis l'écran titre : le décor continue.");
            m.Back();
            Assert.AreEqual(MenuScreen.Title, m.Screen);
            m.StartDay();
            m.Pause();
            Assert.IsTrue(m.TimeStopped);
            m.OpenSettings();
            Assert.IsTrue(m.TimeStopped, "Réglages ouverts depuis la pause : le temps reste arrêté.");
            m.CloseSettings();
            Assert.AreEqual(MenuScreen.Pause, m.Screen);
            m.OpenSettings();
            Assert.IsFalse(m.EndDay(), "Pas de fin de journée depuis les réglages.");
        }

        [Test]
        public void EndDay_OnlyFromPause_AndWarnsAboutUnpaidPassengers()
        {
            var m = new GameMenu(false);
            Assert.IsFalse(m.EndDay());
            m.Pause();
            Assert.IsTrue(m.EndDay());
            Assert.AreEqual(MenuScreen.None, m.Screen);
            Assert.AreEqual("Terminer la journée", GameMenu.EndDayLabel(0));
            Assert.AreEqual("Terminer la journée (1 passager ne paiera pas)", GameMenu.EndDayLabel(1));
            Assert.AreEqual("Terminer la journée (3 passagers ne paieront pas)", GameMenu.EndDayLabel(3));
        }

        [Test]
        public void EndDay_RefusedWhileAChoiceIsPending_WithAClearMessage()
        {
            // Décision de Kevin (23/11) : contrôle de police, accrochage, négociation… d'abord.
            var cases = new (PendingChoices pending, string what)[]
            {
                (new PendingChoices { PoliceControl = true }, "le contrôle de police"),
                (new PendingChoices { Crash = true }, "l'accrochage"),
                (new PendingChoices { Haggle = true }, "la négociation du client"),
                (new PendingChoices { Refusal = true }, "le passager qui refuse de payer"),
                (new PendingChoices { DryTank = true }, "la panne sèche"),
                (new PendingChoices { FlatTyre = true }, "la crevaison"),
                (new PendingChoices { BrokenDown = true }, "la panne"),
            };
            foreach (var (pending, what) in cases)
            {
                var m = new GameMenu(false);
                m.Pause();
                Assert.IsFalse(m.EndDay(pending), what);
                Assert.AreEqual(MenuScreen.Pause, m.Screen, $"{what} : la pause reste ouverte");
                Assert.AreEqual($"Règle d'abord {what} avant de terminer la journée", GameMenu.EndDayRefusal(pending));
                Assert.AreEqual(GameMenu.EndDayRefusal(pending), GameMenu.EndDayLabel(2, pending), "Le bouton dit quoi régler d'abord.");
            }
            Assert.AreEqual("", GameMenu.EndDayRefusal(default));
            var ok = new GameMenu(false);
            ok.Pause();
            Assert.IsTrue(ok.EndDay(new PendingChoices()), "Rien en attente : la journée se termine.");
            Assert.AreEqual("le contrôle de police", new PendingChoices { PoliceControl = true, Haggle = true }.What, "Le plus urgent d'abord.");
        }

        [Test]
        public void Volume_ClampsAndSteps()
        {
            var s = new GameSettings();
            Assert.AreEqual("Volume : 80 %", s.VolumeLabel);
            s.VolumeUp(); s.VolumeUp(); s.VolumeUp();
            Assert.AreEqual(1f, s.Volume, 1e-6f);
            for (var i = 0; i < 12; i++) s.VolumeDown();
            Assert.AreEqual(0f, s.Volume, 1e-6f);
            Assert.AreEqual("Volume : coupé", s.VolumeLabel);
            s.Volume = 0.47f;
            Assert.AreEqual(0.5f, s.Volume, 1e-6f);
        }

        [Test]
        public void Street_EndDayNow_FreezesTheSummary_PassengersDoNotPay()
        {
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 3000 },
                },
            };
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            var street = new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), 7), shift, 3000f);
            shift.Board(new Client("x", "b", 3000, 400, Corpulence.Normal), out _);
            shift.Ledger.AddFare(500);
            Assert.IsFalse(street.DayFinished);
            street.EndDayNow();
            Assert.IsTrue(street.DayFinished);
            Assert.AreEqual(500, street.Summary.Revenue);
            Assert.AreEqual(1, street.Summary.UnpaidPassengers);
            Assert.AreEqual(400, street.Summary.UnpaidFares);
            var summary = street.Summary;
            street.EndDayNow();
            Assert.AreSame(summary, street.Summary, "Le bilan reste celui du premier arrêt.");
        }
    }
}
