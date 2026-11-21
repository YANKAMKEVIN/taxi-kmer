using System;
using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Police;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class PoliceTests
    {
        private static PoliceConfig Enabled()
        {
            var c = new PoliceConfig { enabled = true };
            return c;
        }

        [Test]
        public void Papers_ExpireAndRenew()
        {
            var config = new PoliceConfig();
            config.papers[2].renewalCost = 12000; // assurance (montant d'essai)
            var papers = new PaperSet(config);
            Assert.AreEqual(int.MaxValue, papers.DaysLeft("permis", 500), "Le permis n'expire pas.");
            Assert.AreEqual(12, papers.DaysLeft("assurance", 1));
            Assert.IsTrue(papers.IsValid("assurance", 12));
            Assert.IsFalse(papers.IsValid("assurance", 13));
            Assert.AreEqual("Assurance : expirée", papers.Status("assurance", 13));
            Assert.AreEqual("Assurance : 1 jour", papers.Status("assurance", 12));
            Assert.AreEqual("Permis de conduire : valable", papers.Status("permis", 3));
            Assert.AreEqual("assurance", papers.Expired(13)[0].id);

            Assert.AreEqual(12000, papers.Renew("assurance", 15));
            Assert.AreEqual(15 + 30 - 1, papers.LastValidDay("assurance"), "Expirée : la nouvelle période part du jour du renouvellement.");
            papers.Renew("assurance", 40);
            Assert.AreEqual(44 + 30, papers.LastValidDay("assurance"), "Renouvelée à temps : la période suit l'ancienne.");
            Assert.AreEqual(0, papers.Renew("permis", 3));
            Assert.AreEqual(0, papers.Renew("inconnu", 3));
        }

        [Test]
        public void Finding_RealProblemsFirst_ThenMaybeInvented()
        {
            var config = Enabled();
            var papers = new PaperSet(config);
            var rng = new Random(1);
            Assert.AreEqual(ControlFindingKind.ExpiredPaper, PoliceControl.Find(config, papers, 13, 0, 0, rng).Kind);
            Assert.AreEqual(ControlFindingKind.Overload, PoliceControl.Find(config, papers, 1, 5, 2, rng).Kind);
            var front = PoliceControl.Find(config, papers, 1, 3, 2, rng);
            Assert.AreEqual(ControlFindingKind.FrontOverload, front.Kind, "« Deux devant » : prétexte de la feuille de route.");
            Assert.IsTrue(front.Real);

            config.inventChanceWhenInOrder = 1f;
            var invented = PoliceControl.Find(config, papers, 1, 2, 1, rng);
            Assert.AreEqual(ControlFindingKind.Invented, invented.Kind, "Même en règle, on peut être sommé de payer.");
            Assert.IsFalse(invented.Real);
            StringAssert.Contains("exemple à remplacer", invented.Label);

            config.inventChanceWhenInOrder = 0f;
            var control = new PoliceControl(config, papers, 1, 2, 1, rng);
            Assert.AreEqual(ControlFindingKind.None, control.Finding.Kind);
            Assert.IsTrue(control.Closed, "Rien à reprocher : on repart.");
            Assert.AreEqual(0, control.Demand);
            Assert.AreEqual(config.checkMinutes, control.LostMinutes, "Le contrôle prend quand même du temps.");
            Assert.IsEmpty(control.AvailableChoices());
        }

        [Test]
        public void Pay_ClosesAndCosts()
        {
            var config = Enabled();
            var control = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(2));
            Assert.AreEqual(5000, control.Demand);
            CollectionAssert.AreEqual(new[] { ControlChoice.Pay, ControlChoice.Negotiate, ControlChoice.Contest, ControlChoice.Call }, control.AvailableChoices());
            Assert.IsTrue(control.Choose(ControlChoice.Pay));
            Assert.IsTrue(control.Closed);
            Assert.AreEqual(5000, control.Paid);
            Assert.IsFalse(control.Choose(ControlChoice.Pay), "Une seule fois.");
        }

        [Test]
        public void Negotiate_LowersOrHardens_Limited()
        {
            var config = Enabled();
            config.negotiateFailChance = 0f;
            var c = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(3));
            Assert.IsTrue(c.Choose(ControlChoice.Negotiate));
            Assert.AreEqual(3500, c.Demand, "5 000 − 30 %, arrondi à 500.");
            Assert.IsTrue(c.Choose(ControlChoice.Negotiate));
            Assert.AreEqual(2500, c.Demand);
            Assert.IsFalse(c.CanNegotiate);
            Assert.IsFalse(c.Choose(ControlChoice.Negotiate), "Deux tentatives au plus.");
            Assert.AreEqual(config.checkMinutes + 2 * config.negotiateMinutes, c.LostMinutes);

            config.negotiateFailChance = 1f;
            var hard = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(3));
            hard.Choose(ControlChoice.Negotiate);
            Assert.AreEqual(6000, hard.Demand, "Le policier se braque : +20 %.");

            config.negotiateFailChance = 0f;
            config.demandExpiredPaper = 500;
            var floor = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(3));
            floor.Choose(ControlChoice.Negotiate);
            Assert.AreEqual(500, floor.Demand, "Jamais sous le plancher.");
        }

        [Test]
        public void Contest_DefaultData_NeverWins_ButCanIfKevinDecides()
        {
            var config = Enabled();
            for (var seed = 0; seed < 50; seed++)
            {
                var c = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(seed));
                c.Choose(ControlChoice.Contest);
                Assert.AreEqual("perd", c.OutcomeId, "« gagne » a un poids 0 tant que Kevin n'a pas décidé.");
                Assert.AreEqual(7500, c.Paid);
                Assert.AreEqual(config.checkMinutes + 60, c.LostMinutes);
            }
            config.contestOutcomes[0].weight = 0f;
            config.contestOutcomes[2].weight = 1f;
            var win = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(1));
            win.Choose(ControlChoice.Contest);
            Assert.AreEqual("gagne", win.OutcomeId);
            Assert.AreEqual(0, win.Paid);
        }

        [Test]
        public void Call_And_NoOutcomeData_Pays()
        {
            var config = Enabled();
            var c = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(4));
            c.Choose(ControlChoice.Call);
            Assert.AreEqual("personne", c.OutcomeId);
            Assert.AreEqual(5000, c.Paid);

            config.callOutcomes = Array.Empty<ChoiceOutcomeData>();
            var empty = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(4));
            empty.Choose(ControlChoice.Call);
            Assert.AreEqual(5000, empty.Paid, "Aucune issue dans les données : il paie la somme.");
        }

        [Test]
        public void Schedule_Disabled_NeverStops()
        {
            var s = new PoliceSchedule(new PoliceConfig(), 6000f, 360, 1200, new Random(1));
            Assert.IsEmpty(s.Posts);
            Assert.IsNull(s.Update(0f, 6000f, 600));
        }

        [Test]
        public void Schedule_FixedPost_StopsOnPassing_NotTwiceTooSoon()
        {
            var config = Enabled();
            config.checkpointStopChance = 1f;
            config.flyingChecksPerDay = 0;
            var s = new PoliceSchedule(config, 6132f, 360, 1200, new Random(1));
            Assert.AreEqual(2, s.Posts.Count);
            Assert.IsNull(s.Update(800f, 850f, 400), "Pas encore au poste.");
            Assert.AreEqual("poste-messassi", s.Update(890f, 905f, 400).Id);
            Assert.IsNull(s.Update(905f, 890f, 450), "Retour 50 min plus tard : pas de nouvel arrêt.");
            Assert.AreEqual("poste-messassi", s.Update(890f, 905f, 540)?.Id, "Deux heures plus tard, si.");
            Assert.IsNull(s.Update(905f, 905f, 600), "À l'arrêt, on ne passe devant rien.");
        }

        [Test]
        public void Schedule_FlyingChecks_InsideTheDayAndRoute()
        {
            var config = Enabled();
            config.flyingChecksPerDay = 5;
            var s = new PoliceSchedule(config, 6132f, 360, 1200, new Random(9));
            var flying = 0;
            foreach (var p in s.Posts)
            {
                if (!p.Flying) continue;
                flying++;
                Assert.That(p.Along, Is.InRange(0f, 6132f));
                Assert.GreaterOrEqual(p.FromMinute, 360);
                Assert.LessOrEqual(p.ToMinute, 1200);
                Assert.AreEqual(config.flyingDurationMinutes, p.ToMinute - p.FromMinute);
                Assert.IsFalse(p.ActiveAt(p.ToMinute));
            }
            Assert.AreEqual(5, flying);
        }

        [Test]
        public void Street_AppliesControl_ExpenseTimeReputation()
        {
            var (street, shift) = NewStreet();
            street.Reputation = new Career.DriverReputation(new Career.ReputationConfig());
            var config = Enabled();
            config.reputationPaid = -3;
            var c = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(1));
            Assert.IsFalse(street.ApplyPoliceControl(c, config.impoundFee), "Pas encore terminé.");
            c.Choose(ControlChoice.Pay);
            var before = shift.Clock.GameMinutes;
            Assert.IsTrue(street.ApplyPoliceControl(c, config.impoundFee));
            Assert.AreEqual(5000, shift.Ledger.ExpensesTotal);
            Assert.AreEqual(Street.PoliceExpenseLabel, shift.Ledger.Expenses[0].Label);
            Assert.AreEqual(before + config.checkMinutes, shift.Clock.GameMinutes, 1);
            Assert.AreEqual(47, street.Reputation.Score);
            Assert.IsFalse(street.DayFinished);
        }

        [Test]
        public void Street_Impound_EndsTheDay_PassengersLeaveUnpaid()
        {
            var (street, shift) = NewStreet();
            shift.Cabin.TryBoard(new Client("p1", "etoudi", 3000, 400, Corpulence.Normal), out _);
            var config = Enabled();
            config.contestOutcomes[0].weight = 0f;
            config.contestOutcomes[1].weight = 1f; // fourrière (essai)
            var c = new PoliceControl(config, new PaperSet(config), 13, 1, 0, new Random(1));
            c.Choose(ControlChoice.Contest);
            Assert.IsTrue(c.Impounded);
            street.ApplyPoliceControl(c, 15000);
            Assert.IsTrue(street.DayFinished);
            Assert.IsTrue(street.Summary.Impounded);
            Assert.AreEqual(1, street.Summary.UnpaidPassengers);
            Assert.AreEqual(400, street.Summary.UnpaidFares);
            Assert.AreEqual(15000, street.Summary.ExpensesTotal);
            Assert.IsFalse(street.Summary.VehicleSeized, "Fourrière ≠ saisie : le propriétaire ne renvoie pas (à décider par Kevin).");
        }

        [Test]
        public void TestDay_TenVariedControls()
        {
            // Critère du jalon 4 : une dizaine de contrôles variés dans une journée de test.
            var config = Enabled();
            config.flyingChecksPerDay = 12;
            config.flyingStopChance = 1f;
            config.checkpointStopChance = 1f;
            config.flyingDurationMinutes = 840;
            var schedule = new PoliceSchedule(config, 6132f, 360, 1200, new Random(5));
            var papers = new PaperSet(config);
            var rng = new Random(6);
            var kinds = new HashSet<ControlFindingKind>();
            var choices = new HashSet<string>();
            var controls = 0;
            float along = 0f;
            var dir = 1;
            var day = 1;
            for (var minute = 360; minute < 1200; minute++)
            {
                var next = Math.Max(0f, Math.Min(6132f, along + dir * 400f)); // 24 km/h de jeu
                var post = schedule.Update(along, next, minute);
                along = next;
                if (along <= 0f || along >= 6132f) dir = -dir;
                if (post == null) continue;
                controls++;
                // Journée de test : on change les papiers et la charge pour voir des contrôles variés.
                var passengers = controls % 4;
                var front = controls % 3 == 0 ? 2 : 0;
                var testDay = controls % 5 == 0 ? 13 : day;
                var c = new PoliceControl(config, papers, testDay, passengers + (controls % 6 == 0 ? 3 : 0), front, rng);
                kinds.Add(c.Finding.Kind);
                var choice = (ControlChoice)(controls % 4);
                if (!c.Closed && c.Choose(choice)) choices.Add(choice.ToString());
                if (!c.Closed) c.Choose(ControlChoice.Pay);
                Assert.IsTrue(c.Closed);
            }
            Assert.GreaterOrEqual(controls, 10);
            Assert.GreaterOrEqual(kinds.Count, 4, string.Join(",", kinds));
            Assert.GreaterOrEqual(choices.Count, 3);
        }

        private static (Street, Shift) NewStreet()
        {
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "dispensaire", name = "Dispensaire", distanceAlongRouteMeters = 0 },
                    new Stop { id = "etoudi", name = "Étoudi", distanceAlongRouteMeters = 3000 },
                },
            };
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            var street = new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), 1), shift, 3000f);
            return (street, shift);
        }
    }
}
