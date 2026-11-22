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

        private static (PoliceConfig, PaperSet, Career.DriverCareer) Renewal(int day, int savings, int insuranceCost, int technicalCost = 0, int licenceCost = 0)
        {
            var config = new PoliceConfig();
            config.papers[2].renewalCost = insuranceCost;   // assurance (montants d'essai)
            config.papers[3].renewalCost = technicalCost;   // visite technique
            config.papers[4].renewalCost = licenceCost;     // licence
            var career = Career.DriverCareer.Restore(new Career.OwnerConfig(), day, savings, null, false);
            return (config, new PaperSet(config), career);
        }

        [Test]
        public void AutoRenewal_AtDeadline_PaidFromSavings()
        {
            // Décision de Kevin (21/11) : renouvelés à l'échéance, payés sur l'épargne.
            var (_, papers, career) = Renewal(12, 20000, 12000);
            Assert.IsEmpty(PaperRenewal.AtDayStart(papers, career), "Jour 12 : l'assurance est encore valable, rien à faire.");
            Assert.AreEqual(20000, career.Savings);

            (_, papers, career) = Renewal(13, 20000, 12000);
            var done = PaperRenewal.AtDayStart(papers, career);
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual("assurance", done[0].Paper.id);
            Assert.IsTrue(done[0].Renewed);
            Assert.AreEqual(8000, career.Savings);
            Assert.AreEqual(13 + 30 - 1, papers.LastValidDay("assurance"), "Pas de trou : la nouvelle période part de l'échéance.");
            Assert.IsTrue(papers.IsValid("assurance", 13));
            Assert.AreEqual("Assurance renouvelée (12 000 FCFA pris sur l'épargne).", PoliceTexts.Renewal(done[0]));
            Assert.AreEqual(ControlFindingKind.Invented, PoliceControl.Find(new PoliceConfig { inventChanceWhenInOrder = 1f }, papers, 13, 1, 0, new Random(1)).Kind,
                "Papiers renouvelés : le contrôle n'a plus de vrai problème à trouver.");
        }

        [Test]
        public void AutoRenewal_NotEnoughSavings_StaysExpired_Announced_RetriedNextDay()
        {
            // Valeur par défaut de Claude (à confirmer par Kevin) : le papier reste expiré, un message l'annonce.
            var (config, papers, career) = Renewal(13, 5000, 12000);
            var done = PaperRenewal.AtDayStart(papers, career);
            Assert.AreEqual(1, done.Count);
            Assert.IsFalse(done[0].Renewed);
            Assert.AreEqual(5000, career.Savings, "Rien n'est pris.");
            Assert.IsFalse(papers.IsValid("assurance", 13));
            Assert.AreEqual(12, done[0].LastValidDay);
            var text = PoliceTexts.Renewal(done[0]);
            Assert.AreEqual("Assurance expirée : pas assez d'épargne pour la renouveler (12 000 FCFA). Gare aux contrôles.", text);
            Assert.IsFalse(PoliceTexts.IsCrude(text));
            Assert.AreEqual(ControlFindingKind.ExpiredPaper, PoliceControl.Find(config, papers, 13, 1, 0, new Random(1)).Kind);

            // Le lendemain, avec assez d'épargne : renouvelée à partir de ce jour.
            var next = Career.DriverCareer.Restore(new Career.OwnerConfig(), 14, 15000, null, false);
            done = PaperRenewal.AtDayStart(papers, next);
            Assert.IsTrue(done[0].Renewed);
            Assert.AreEqual(3000, next.Savings);
            Assert.AreEqual(14 + 30 - 1, papers.LastValidDay("assurance"));
        }

        [Test]
        public void AutoRenewal_SavingsShared_InDataOrder()
        {
            // Deux papiers à échéance le même jour : l'épargne paie le premier (ordre des données), pas le second.
            var (_, papers, career) = Renewal(30, 10000, 8000, 6000);
            var done = PaperRenewal.AtDayStart(papers, career);
            Assert.AreEqual(new[] { "assurance", "visite-technique" }, new[] { done[0].Paper.id, done[1].Paper.id });
            Assert.IsTrue(done[0].Renewed);
            Assert.IsFalse(done[1].Renewed);
            Assert.AreEqual(2000, career.Savings);
            Assert.AreEqual(8000, PaperRenewal.TotalPaid(done));
        }

        [Test]
        public void AutoRenewal_Free_AlwaysRenewed_EvenInDebt()
        {
            // Papiers gratuits (montants d'essai à 0) : une épargne négative n'empêche rien.
            var (_, papers, career) = Renewal(13, -4000, 0);
            var done = PaperRenewal.AtDayStart(papers, career);
            Assert.IsTrue(done[0].Renewed);
            Assert.AreEqual(-4000, career.Savings);
            Assert.AreEqual("Assurance renouvelée.", PoliceTexts.Renewal(done[0]));
            // Toute une carrière : les papiers ne sont jamais expirés au moment de rouler.
            for (var day = 14; day <= 400; day++)
            {
                PaperRenewal.AtDayStart(papers, Career.DriverCareer.Restore(new Career.OwnerConfig(), day, 0, null, false));
                Assert.IsEmpty(papers.Expired(day), $"jour {day}");
            }
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
            CollectionAssert.AreEqual(new[] { ControlChoice.Pay, ControlChoice.Negotiate, ControlChoice.Contest, ControlChoice.Call, ControlChoice.Flee }, control.AvailableChoices());
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

        /// <summary>Contester, option B (Kevin, 21/11) : gagne 1 fois sur 3 seulement si le problème est inventé, papiers en règle.</summary>
        [Test]
        public void Contest_WinsAboutOneInThree_OnlyWhenInventedAndPapersInOrder()
        {
            var config = Enabled();
            var real = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(1)); // assurance expirée
            Assert.IsTrue(real.Finding.Real);
            Assert.AreEqual(0.0, real.ChanceOf(config.contestOutcomes, "gagne"), "Problème réel : jamais.");
            Assert.AreEqual(0.25, real.ChanceOf(config.contestOutcomes, "fourriere"), 1e-9, "Sinon, fourrière possible.");
            config.inventChanceWhenInOrder = 1f;
            var invented = new PoliceControl(config, new PaperSet(config), 1, 0, 0, new Random(1));
            Assert.AreEqual(ControlFindingKind.Invented, invented.Finding.Kind);
            Assert.IsTrue(invented.PapersInOrder);
            Assert.AreEqual(1.0 / 3.0, invented.ChanceOf(config.contestOutcomes, "gagne"), 1e-9);

            int wins = 0, impounds = 0, n = 3000;
            for (var seed = 0; seed < n; seed++)
            {
                var c = new PoliceControl(config, new PaperSet(config), 1, 0, 0, new Random(seed));
                c.Choose(ControlChoice.Contest);
                if (c.OutcomeId == "gagne") { wins++; Assert.AreEqual(0, c.Paid); }
                if (c.Impounded) impounds++;
                var r = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(seed));
                r.Choose(ControlChoice.Contest);
                Assert.AreNotEqual("gagne", r.OutcomeId, "Problème réel : on ne gagne jamais.");
                if (r.OutcomeId == "perd") Assert.AreEqual(7500, r.Paid, "Perdre : on paie 1,5 fois.");
            }
            Assert.AreEqual(1.0 / 3.0, wins / (double)n, 0.03);
            Assert.Greater(impounds, 0, "Fourrière possible même quand le problème est inventé.");
        }

        /// <summary>Appeler quelqu'un, option B : ça marche avec un contact ; contacts = crochet du jalon 7, coupé.</summary>
        [Test]
        public void Call_WorksOnlyWithAContact_HookOffUntilMilestone7()
        {
            var config = Enabled();
            var contacts = new PoliceContacts(config);
            contacts.Add("cousin-brigade");
            Assert.IsFalse(contacts.HasContact, "Crochet du jalon 7 coupé : personne à appeler.");
            var noContact = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(4), contacts.HasContact);
            noContact.Choose(ControlChoice.Call);
            Assert.AreEqual("personne", noContact.OutcomeId);
            Assert.AreEqual(5000, noContact.Paid);

            config.contactsEnabled = true;
            var withHook = new PoliceContacts(config);
            Assert.IsFalse(withHook.HasContact, "Pas encore de contact.");
            withHook.Add("cousin-brigade");
            withHook.Add("cousin-brigade");
            Assert.AreEqual(1, withHook.Ids.Count);
            for (var seed = 0; seed < 20; seed++)
            {
                var c = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(seed), withHook.HasContact);
                c.Choose(ControlChoice.Call);
                Assert.AreEqual("arrange", c.OutcomeId, "Avec un contact, ça marche.");
                Assert.AreEqual(0, c.Paid);
            }
        }

        /// <summary>Fuir, option B : poursuite, fourrière si rattrapé ; sinon recherché, et au prochain poste fourrière ou arrestation possibles.</summary>
        [Test]
        public void Flee_ChaseThenImpoundOrWanted_NextPostSettlesIt()
        {
            var config = Enabled();
            int caught = 0, escaped = 0;
            for (var seed = 0; seed < 400; seed++)
            {
                var c = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(seed));
                Assert.IsTrue(c.Choose(ControlChoice.Flee));
                Assert.IsTrue(c.Closed);
                Assert.AreEqual(0, c.Paid);
                Assert.AreEqual(0, c.ReputationDelta);
                if (c.Impounded) { caught++; Assert.AreEqual("rattrape", c.OutcomeId); }
                else { escaped++; Assert.IsTrue(c.BecameWanted); }
            }
            Assert.AreEqual(0.4, caught / 400.0, 0.08);
            Assert.Greater(escaped, 0);

            var outcomes = new HashSet<string>();
            for (var seed = 0; seed < 200; seed++)
            {
                var w = new PoliceControl(config, new PaperSet(config), 1, 0, 0, new Random(seed), wanted: true);
                Assert.AreEqual(ControlFindingKind.Fled, w.Finding.Kind);
                outcomes.Add(w.OutcomeId);
                if (w.OutcomeId == "fourriere") Assert.IsTrue(w.Impounded && w.Closed);
                if (w.OutcomeId == "arrestation") Assert.IsTrue(w.Arrested && w.Closed);
                if (w.OutcomeId == "oublie") { Assert.IsFalse(w.Closed); Assert.AreEqual(config.demandFled, w.Demand); }
            }
            CollectionAssert.AreEquivalent(new[] { "fourriere", "arrestation", "oublie" }, outcomes);

            config.fleeEnabled = false;
            var off = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(1));
            Assert.IsFalse(off.Choose(ControlChoice.Flee));
            CollectionAssert.DoesNotContain(off.AvailableChoices(), ControlChoice.Flee);
        }

        [Test]
        public void PoliceDay_FleeThenNextPostStopsForSure()
        {
            var config = Enabled();
            config.flyingChecksPerDay = 0;
            config.checkpointStopChance = 1f;
            config.fleeOutcomes = new[] { new ChoiceOutcomeData { id = "echappe", weight = 1f, payFactor = 0f, wanted = true } };
            var day = new PoliceDay(config, null, null, 13, 6132f, 360, 1200, new Random(2));
            var c = day.Update(890f, 905f, 400, 0, 0);
            Assert.IsNotNull(c);
            Assert.IsNull(day.Update(4000f, 5300f, 401, 0, 0), "Un contrôle à la fois.");
            c.Choose(ControlChoice.Flee);
            Assert.AreSame(c, day.Close());
            Assert.IsTrue(day.Schedule.Wanted);
            config.checkpointStopChance = 0f; // même sans chance d'arrêt ordinaire…
            var next = day.Update(5100f, 5300f, 420, 0, 0);
            Assert.IsNotNull(next, "… recherché, le prochain poste l'arrête.");
            Assert.AreEqual(ControlFindingKind.Fled, next.Finding.Kind);
            Assert.IsFalse(day.Schedule.Wanted, "L'affaire se règle à ce contrôle.");
            Assert.AreEqual(2, day.Controls);
        }

        [Test]
        public void Street_Arrest_EndsTheDay()
        {
            var (street, shift) = NewStreet();
            shift.Cabin.TryBoard(new Client("p1", "etoudi", 3000, 400, Corpulence.Normal), out _);
            var config = Enabled();
            config.wantedOutcomes = new[] { new ChoiceOutcomeData { id = "arrestation", weight = 1f, arrest = true } };
            var c = new PoliceControl(config, new PaperSet(config), 1, 1, 0, new Random(1), wanted: true);
            Assert.IsTrue(c.Arrested);
            Assert.IsTrue(street.ApplyPoliceControl(c, config.impoundFee));
            Assert.IsTrue(street.DayFinished);
            Assert.IsTrue(street.Summary.Arrested);
            Assert.IsFalse(street.Summary.Impounded);
            Assert.AreEqual(1, street.Summary.UnpaidPassengers);
            StringAssert.Contains("poste", PoliceTexts.Closed(c));
        }

        /// <summary>Réputation (Kevin, 21/11) : payer, refuser ou contester ne la changent pas.</summary>
        [Test]
        public void Reputation_PayRefuseContest_DoNotChangeIt()
        {
            var config = new PoliceConfig();
            Assert.AreEqual(0, config.reputationPaid);
            foreach (var o in config.contestOutcomes) Assert.AreEqual(0, o.reputation, o.id);
            foreach (var o in config.callOutcomes) Assert.AreEqual(0, o.reputation, o.id);
            foreach (var choice in new[] { ControlChoice.Pay, ControlChoice.Contest, ControlChoice.Call })
                for (var seed = 0; seed < 30; seed++)
                {
                    var c = new PoliceControl(config, new PaperSet(config), seed % 2 == 0 ? 13 : 1, 0, 0, new Random(seed));
                    if (!c.Closed) c.Choose(choice);
                    Assert.AreEqual(0, c.ReputationDelta, $"{choice}");
                }
        }

        /// <summary>Effet de la réputation (Kevin, 21/11) : une bonne réputation fait moins arrêter.</summary>
        [Test]
        public void Schedule_StopFactor_FromReputation()
        {
            var config = Enabled();
            config.flyingChecksPerDay = 0;
            config.checkpointCooldownMinutes = 0;
            int Count(float factor)
            {
                var s = new PoliceSchedule(config, 6132f, 360, 1200, new Random(3));
                var n = 0;
                for (var i = 0; i < 2000; i++) if (s.Update(890f, 905f, 400 + i, factor) != null) n++;
                return n;
            }
            var good = Count(Career.ReputationEffects.PoliceStop(new Career.ReputationEffectsConfig(), 100));
            var bad = Count(Career.ReputationEffects.PoliceStop(new Career.ReputationEffectsConfig(), 0));
            Assert.AreEqual(0.35, good / 2000.0, 0.04);
            Assert.AreEqual(0.65, bad / 2000.0, 0.04);
        }

        /// <summary>Papiers, option A (Kevin, 21/11) : assurance 30 j, visite technique 180 j, licence 365 j de jeu.</summary>
        [Test]
        public void Papers_OptionA_RealisticDurations()
        {
            var config = new PoliceConfig();
            var byId = new Dictionary<string, int>();
            foreach (var p in config.papers) byId[p.id] = p.validityDays;
            Assert.AreEqual(30, byId["assurance"]);
            Assert.AreEqual(180, byId["visite-technique"]);
            Assert.AreEqual(365, byId["licence-taxi"]);
            Assert.IsTrue(config.enabled, "Jalon 4 activé le 21/11.");
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
            var s = new PoliceSchedule(new PoliceConfig { enabled = false }, 6000f, 360, 1200, new Random(1));
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
            Assert.IsFalse(street.Summary.VehicleSeized, "Fourrière ≠ saisie : le propriétaire renvoie seulement à la 2ᵉ fourrière (Kevin, 21/11).");
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

        [Test]
        public void Texts_RealisticTone_SuggestWithoutShowing()
        {
            // Ton réaliste (Kevin, 2026-11-21) : même phrase pour un problème réel et un prétexte inventé, sans mot cru.
            var config = Enabled();
            var real = new PoliceControl(config, new PaperSet(config), 13, 0, 0, new Random(2)); // assurance expirée
            Assert.IsTrue(real.Finding.Real);
            StringAssert.Contains("Le policier parle de", PoliceTexts.Demand(real));
            StringAssert.Contains("expirée", PoliceTexts.Demand(real));

            config.inventChanceWhenInOrder = 1f;
            var invented = new PoliceControl(config, new PaperSet(config), 1, 0, 0, new Random(2));
            Assert.AreEqual(ControlFindingKind.Invented, invented.Finding.Kind);
            StringAssert.Contains("Le policier parle de", PoliceTexts.Demand(invented), "Même phrase : au joueur de comprendre.");

            config.inventChanceWhenInOrder = 0f;
            var inOrder = new PoliceControl(config, new PaperSet(config), 1, 0, 0, new Random(2));
            Assert.AreEqual("Le policier regarde les papiers, longtemps.", PoliceTexts.Demand(inOrder));

            invented.Choose(ControlChoice.Pay);
            Assert.AreEqual($"{DaySummary.Money(invented.Paid)} laissés au contrôle. Bonne route.", PoliceTexts.Closed(invented));
            Assert.AreEqual("Appeler quelqu'un", PoliceTexts.Choice(ControlChoice.Call));

            foreach (var text in new[] { PoliceTexts.Demand(real), PoliceTexts.Demand(invented), PoliceTexts.Closed(invented),
                         PoliceTexts.Choice(ControlChoice.Pay), PoliceTexts.Choice(ControlChoice.Negotiate), PoliceTexts.Choice(ControlChoice.Contest),
                         Street.PoliceExpenseLabel, Street.ImpoundExpenseLabel })
                Assert.IsFalse(PoliceTexts.IsCrude(text), text);
            Assert.IsTrue(PoliceTexts.IsCrude("Un pot-de-vin"));
        }
        [Test]
        public void Markers_FixedAlwaysShown_FlyingOnlyWhileInPlace()
        {
            // Tâche 4.5 : postes dans le monde. Fixe : toujours là ; volant : seulement pendant qu'il est en place.
            var config = Enabled();
            var schedule = new PoliceSchedule(config, 6000f, 6 * 60, 20 * 60, new Random(3));
            ControlPost fixedPost = null, flying = null;
            foreach (var p in schedule.Posts) { if (p.Flying) flying ??= p; else fixedPost ??= p; }
            Assert.IsNotNull(fixedPost);
            Assert.IsNotNull(flying);
            Assert.IsTrue(PoliceMarkers.Visible(fixedPost, 0));
            Assert.IsTrue(PoliceMarkers.Visible(fixedPost, 23 * 60));
            Assert.IsFalse(PoliceMarkers.Visible(flying, flying.FromMinute - 1));
            Assert.IsTrue(PoliceMarkers.Visible(flying, flying.FromMinute));
            Assert.IsFalse(PoliceMarkers.Visible(flying, flying.ToMinute));
            Assert.IsFalse(PoliceMarkers.Visible(null, 0));
            Assert.AreEqual("Contrôle", PoliceMarkers.Label(fixedPost));
            Assert.AreEqual("Contrôle volant", PoliceMarkers.Label(flying));
            Assert.IsTrue(PoliceMarkers.Holding(fixedPost, fixedPost));
            Assert.IsFalse(PoliceMarkers.Holding(fixedPost, flying));
            Assert.IsFalse(PoliceMarkers.Holding(fixedPost, null));
            Assert.IsTrue(PoliceMarkers.LabelShown(fixedPost, fixedPost.Along - 200f));
            Assert.IsFalse(PoliceMarkers.LabelShown(fixedPost, fixedPost.Along + 300f));
        }
        [Test]
        public void Papers_Summary_ForPauseScreen_ExpiredFirst_SkipsNeverExpiring()
        {
            var papers = new PaperSet(new PoliceConfig());
            Assert.AreEqual("Assurance : 12 jours · Visite technique : 25 jours · Licence de taxi : 40 jours", papers.Summary(1));
            Assert.AreEqual("Assurance : expirée · Visite technique : 13 jours · Licence de taxi : 28 jours", papers.Summary(13));
            Assert.AreEqual("", new PaperSet(new PoliceConfig { papers = new[] { new PaperData { id = "permis", name = "Permis de conduire" } } }).Summary(1));
        }
    }
}
