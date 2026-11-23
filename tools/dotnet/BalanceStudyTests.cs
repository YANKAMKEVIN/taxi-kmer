// Tests de l'outil d'étude d'équilibrage (BalanceStudy.cs) et grille complète (à la demande, catégorie Equilibrage).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.DotnetTests
{
    public class BalanceStudyTests
    {
        private static BalanceSetting Current(GameData data)
        {
            // Valeurs actuelles du jeu (fuel.json, fares.json ×1, day.json).
            return new BalanceSetting
            {
                ConsumptionScale = BalanceBot.Load<Fuel.FuelConfig>("fuel.json").consumptionScale,
                PriceMultiplier = 1f,
                Versement = BalanceBot.Load<DayConfig>("day.json").versement,
            };
        }

        [Test]
        public void ScaleFares_RoundsTo50_AndKeepsDistances()
        {
            // Anciens prix de Yaoundé (300 à 800), sur lesquels la grille de l'étude a été calculée.
            var yaounde = BalanceStudy.ScaleFares(FareConfig.Default(), 0.5f);
            Assert.AreEqual(new[] { 300, 400, 500, 600 }, yaounde.bands.Select(b => b.fare).ToArray());
            Assert.AreEqual(800, yaounde.longDistanceFare);
            var f = BalanceStudy.ScaleFares(yaounde, 1.25f);
            Assert.AreEqual(new[] { 400, 500, 650, 750 }, f.bands.Select(b => b.fare).ToArray()); // 375 → 400, 625 → 650
            Assert.AreEqual(1000, f.longDistanceFare);
            Assert.AreEqual(2000, f.bands[0].maxDistanceMeters);
            var same = BalanceStudy.ScaleFares(FareConfig.Default(), 1f);
            Assert.AreEqual(new[] { 600, 800, 1000, 1200 }, same.bands.Select(b => b.fare).ToArray());
            Assert.AreEqual(1600, same.longDistanceFare);
        }

        [Test]
        public void Settings_AreAppliedToCopies_GameDataUnchanged()
        {
            var data = GameData.Load();
            var s = new BalanceSetting { ConsumptionScale = 1.5f, PriceMultiplier = 2f, Versement = 5000 };
            Assert.AreEqual(1.5f, data.FuelFor(s).consumptionScale);
            Assert.AreEqual(5000, data.DayFor(s).versement);
            var first = BalanceBot.Load<FareConfig>("fares.json").bands[0].fare;
            Assert.AreEqual(2 * first, data.FaresFor(s).bands[0].fare);
            // Les fichiers du jeu, relus, n'ont pas bougé.
            Assert.AreEqual(Current(data).ConsumptionScale, BalanceBot.Load<Fuel.FuelConfig>("fuel.json").consumptionScale);
            Assert.AreEqual(first, BalanceBot.Load<FareConfig>("fares.json").bands[0].fare);
        }

        [Test]
        public void SameSeed_SameCareer()
        {
            var data = GameData.Load();
            var s = Current(data);
            var a = BalanceStudy.PlayCareer(data, s, new AveragePlayer(), 2, 5);
            var b = BalanceStudy.PlayCareer(data, s, new AveragePlayer(), 2, 5);
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Summary.Revenue, b[i].Summary.Revenue);
                Assert.AreEqual(a[i].Summary.FuelCost, b[i].Summary.FuelCost);
                Assert.AreEqual(a[i].Summary.ExpensesTotal, b[i].Summary.ExpensesTotal);
                Assert.AreEqual(a[i].Reputation, b[i].Reputation);
            }
        }

        [Test]
        public void Career_UsesActiveSystems_AndCarriesStateOver()
        {
            var data = GameData.Load();
            Assume.That(data.Police.enabled && data.Road.enabled, "Contrôles et route actifs dans les données.");
            var days = BalanceStudy.PlayCareer(data, Current(data), new AveragePlayer(), 4, 11);
            Assert.That(days.All(d => d.Summary.RideCount > 0), "Le joueur moyen fait des courses chaque jour.");
            Assert.Greater(days.Sum(d => d.Controls), 0, "Des contrôles de police en 4 jours.");
            Assert.Greater(days.Sum(d => d.PolicePaid), 0);
            Assert.Greater(days.Sum(d => d.Summary.FuelCost), 0, "Du carburant acheté en 4 jours.");
            Assert.That(days.Any(d => d.Reputation != data.Reputation.start), "La réputation bouge et passe d'un jour à l'autre.");
        }

        /// <summary>
        /// Garde-fou de l'équilibrage (QA du 22/11) : avec les données du jeu (décisions de Kevin du 21-22/11 : carburant ×1,
        /// prix 600 à 1 600, versement 6 000, papiers 500 / 1 000 / 2 000), le versement reste atteint dans 60 à 80 % des
        /// journées sans trafic (étude : 70 %, equilibrage.md) et peu de chauffeurs sont renvoyés en 14 jours (étude : 10 %).
        /// Une donnée changée sans relancer l'étude fait échouer ce test au lieu de casser l'équilibre en silence.
        /// 40 carrières de 14 jours, graines fixes.
        /// </summary>
        [Test]
        public void GameData_KeepsTheChosenBalance()
        {
            var data = GameData.Load();
            var s = Current(data);
            var r = BalanceStudy.RunCareers(data, s.ConsumptionScale, 1f, new[] { s.Versement }, 40, Horizon, new AveragePlayer())[0];
            TestContext.WriteLine($"Versement atteint {BalanceStudy.Pct(r.ReachedShare)}, renvoyés en {Horizon} j {BalanceStudy.Pct(r.FiredByHorizon)}");
            Assert.That(r.ReachedShare, Is.InRange(0.6, 0.8), "Versement atteint hors de la cible de Kevin (60 à 70 %, equilibrage.md).");
            Assert.That(r.FiredByHorizon, Is.LessThanOrEqualTo(0.3));
        }

        /// <summary>
        /// Pourboires rares et petits (décision de Kevin du 23/11, 100 à 200 FCFA sur quelques réponses) : le joueur moyen
        /// qui répond à une bulle par passager en reçoit un peu, sans changer l'équilibre (une petite part des recettes).
        /// </summary>
        [Test]
        public void Tips_AreRareAndSmall()
        {
            var data = GameData.Load();
            var days = BalanceStudy.PlayCareer(data, Current(data), new AveragePlayer(), 3, 14);
            var tips = days.Sum(d => d.TipsPromised);
            var revenue = days.Sum(d => d.Summary.Revenue);
            var rides = days.Sum(d => d.Summary.RideCount);
            TestContext.WriteLine($"{days.Length} jours : {rides} courses, pourboires {tips} FCFA ({tips / (double)days.Length:0} par jour), recettes {revenue} FCFA");
            Assert.Greater(tips, 0, "Quelques pourboires en 14 jours.");
            Assert.LessOrEqual(tips * 100, revenue * 3, "Les pourboires restent sous 3 % des recettes.");
            var off = BalanceStudy.PlayCareer(data, Current(data), new AveragePlayer { AnswersPassengers = false }, 3, 14);
            Assert.AreEqual(0, off.Sum(d => d.TipsPromised), "Sans réponse, pas de pourboire.");
        }

        [Test]
        public void LessFuel_HigherPrices_LowerVersement_AreEasier()
        {
            var data = GameData.Load();
            var player = new AveragePlayer();
            var v = new[] { 5000, 7000 };
            var hard = BalanceStudy.RunCareers(data, 8f, 1f, v, 4, 3, player);
            var easy = BalanceStudy.RunCareers(data, 1f, 1.5f, v, 4, 3, player);
            Assert.Greater(easy[1].MeanLeftover, hard[1].MeanLeftover, "Moins de carburant et des prix plus hauts laissent plus au chauffeur.");
            Assert.Less(easy[1].MeanFuel, hard[1].MeanFuel);
            Assert.Greater(easy[1].MeanRevenue, hard[1].MeanRevenue);
            // Mêmes journées closes avec deux versements : 2 000 de moins au versement = 2 000 de plus au reste.
            Assert.AreEqual(easy[0].MeanLeftover - easy[1].MeanLeftover, 2000.0, 1e-6);
            Assert.GreaterOrEqual(easy[0].ReachedShare, easy[1].ReachedShare);
            Assert.LessOrEqual(easy[0].FiredByHorizon, easy[1].FiredByHorizon);
        }

        [Test]
        public void Papers_RenewedAtDeadline_NeverExpiredAtControls()
        {
            // Décision de Kevin (21/11) : renouvellement automatique. Sans lui, l'assurance expire le 13ᵉ jour pour toujours.
            var data = GameData.Load();
            Assume.That(data.Police.enabled && data.Police.autoRenewPapers);
            var days = BalanceStudy.PlayCareer(data, Current(data), new AveragePlayer(), 16, 7);
            Assert.AreEqual(0, days.Sum(d => d.ExpiredPaperControls), "Papiers renouvelés : jamais expirés au contrôle.");
            Assert.AreEqual(1, days[12].PapersRenewed, "Assurance renouvelée au début du 13ᵉ jour.");
            Assert.AreEqual(500, days[12].RenewalCost, "Assurance : 500 FCFA (décision de Kevin du 22/11, equilibrage.md).");
            Assert.AreEqual(500, days.Sum(d => d.RenewalCost), "Seule l'assurance arrive à échéance en 16 jours.");

            data.Police.autoRenewPapers = false;
            var before = BalanceStudy.PlayCareer(data, Current(data), new AveragePlayer(), 16, 7);
            Assert.AreEqual(0, before.Take(12).Sum(d => d.ExpiredPaperControls), "Avant le 13ᵉ jour, l'assurance est valable.");
            Assert.Greater(before.Skip(12).Sum(d => d.ExpiredPaperControls), 0, "Sans renouvellement : assurance expirée aux contrôles dès le 13ᵉ jour.");
        }

        private static readonly Dictionary<string, int> Expensive = new Dictionary<string, int>
        {
            { "assurance", 1000000 }, { "visite-technique", 1000000 }, { "licence-taxi", 1000000 },
        };

        [Test]
        public void WithRenewalCosts_ChangesACopy_GameDataUnchanged()
        {
            var data = GameData.Load();
            var before = data.Police.papers.Select(p => p.renewalCost).ToArray();
            var copy = BalanceStudy.WithRenewalCosts(data, new Dictionary<string, int> { { "assurance", 1234 } });
            Assert.AreEqual(1234, copy.Police.papers.First(p => p.id == "assurance").renewalCost);
            Assert.AreEqual(before, data.Police.papers.Select(p => p.renewalCost).ToArray(), "Les données chargées n'ont pas bougé.");
            Assert.AreEqual(before, BalanceBot.Load<Police.PoliceConfig>("police.json").papers.Select(p => p.renewalCost).ToArray());
            Assert.AreSame(data.Owner, copy.Owner);
        }

        [Test]
        public void Renewal_NotAffordable_PaperStaysExpired_AndIsTriedAgainNextMorning()
        {
            // Cas validé par Kevin : épargne insuffisante → le papier reste expiré, nouvel essai chaque matin.
            var data = BalanceStudy.WithRenewalCosts(GameData.Load(), Expensive);
            Assume.That(data.Police.enabled && data.Police.autoRenewPapers);
            var carry = new CareerCarry { Savings = 5000 };
            var days = new List<StudyDay>();
            for (var d = 0; d < 16; d++)
                days.Add(StudyDayPlayer.Play(data, Current(data), new AveragePlayer(), carry, 7 + d));
            Assert.AreEqual(0, days.Take(12).Sum(d => d.PapersNotRenewed), "Avant le 13ᵉ jour, rien n'arrive à échéance.");
            Assert.That(days.Skip(12).All(d => d.PapersNotRenewed == 1 && d.RenewalCost == 0), "Assurance impayée : retentée chaque matin, rien payé.");
            Assert.Greater(days.Skip(12).Sum(d => d.ExpiredPaperControls), 0, "Assurance expirée trouvée aux contrôles.");

            carry.Savings = 2000000; // assez d'épargne : renouvelée le matin suivant
            var paid = StudyDayPlayer.Play(data, Current(data), new AveragePlayer(), carry, 99);
            Assert.AreEqual(1, paid.PapersRenewed);
            Assert.AreEqual(1000000, paid.RenewalCost);
            Assert.AreEqual(0, paid.PapersNotRenewed);
        }

        [Test]
        public void RunCareers_PaidRenewals_UseRealSavings()
        {
            var data = GameData.Load();
            var free = BalanceStudy.WithRenewalCosts(data, new Dictionary<string, int> { { "assurance", 0 }, { "visite-technique", 0 }, { "licence-taxi", 0 } });
            Assert.IsFalse(BalanceStudy.RenewalsCostMoney(free.Police));
            var expensive = BalanceStudy.WithRenewalCosts(data, Expensive);
            Assert.IsTrue(BalanceStudy.RenewalsCostMoney(expensive.Police));
            var s = Current(data);
            var a = BalanceStudy.RunCareers(free, s.ConsumptionScale, 1f, new[] { 6000 }, 3, 16, new AveragePlayer())[0];
            var b = BalanceStudy.RunCareers(expensive, s.ConsumptionScale, 1f, new[] { 6000 }, 3, 16, new AveragePlayer())[0];
            Assert.AreEqual(0, a.DaysWithUnpaidPaper);
            Assert.AreEqual(0, a.ExpiredControlsPerDay);
            Assert.AreEqual(4.0 / 16, b.DaysWithUnpaidPaper, 1e-9, "Jours 13 à 16 : assurance impayable.");
            Assert.Greater(b.ExpiredControlsPerDay, 0);
            Assert.AreEqual(0, b.MeanRenewalCost);
        }

        [Test]
        public void WithVersement_ChangesOnlyTheVersement()
        {
            var data = GameData.Load();
            var d = BalanceStudy.PlayCareer(data, Current(data), new AveragePlayer(), 1, 3)[0].Summary;
            var toward = d.TowardVersement;
            BalanceStudy.WithVersement(d, 1234);
            Assert.AreEqual(1234, d.Versement);
            Assert.AreEqual(toward, d.TowardVersement);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Grille complète (≈ 5 min sur 4 cœurs) : tools/test_core.sh "Category=Equilibrage". Rapport dans
        // tools/dotnet/bin/Debug/net8.0/equilibrage/grille.md, recopié dans docs/tech/equilibrage.md.

        // Multiplicateurs de la grille : consommation absolue (fuel.json → consumptionScale remplacé), prix relatifs à
        // fares.json. Depuis la décision de Kevin du 21/11, fares.json vaut déjà ×2 des prix de Yaoundé : la grille de
        // docs/tech/equilibrage.md a été calculée sur les anciens prix (300 à 800).
        public static readonly float[] Scales = { 1f, 1.5f, 2f, 3f, 4f, 8f };
        public static readonly float[] Prices = { 1f, 1.25f, 1.5f, 2f };
        public static readonly int[] Versements = { 5000, 6000, 7000 };
        public const int Horizon = 14;

        /// <summary>Grille avec le trafic simulé (concurrence des taxis et mototaxis) : la référence (≈ 45 min).</summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void Grid_Report() => Grid(true, 30, "grille.md");

        /// <summary>Même grille sans trafic (plus de carrières, mais recette plus haute : borne optimiste) (≈ 5 min).</summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void GridWithoutTraffic_Report() => Grid(false, 200, "grille-sans-trafic.md");

        private static void Grid(bool withCity, int careers, string file)
        {
            var data = GameData.Load(withWorld: withCity);
            var player = new AveragePlayer { WithCity = withCity };
            var watch = Stopwatch.StartNew();
            var all = new List<SettingResult>();
            foreach (var scale in Scales)
            foreach (var price in Prices)
                all.AddRange(BalanceStudy.RunCareers(data, scale, price, Versements, careers, Horizon, player));
            var sb = new StringBuilder();
            sb.AppendLine($"{careers} carrières de {Horizon} jours par réglage ({careers * Horizon} journées), joueur moyen par défaut, {(withCity ? "avec" : "sans")} trafic, {watch.Elapsed.TotalSeconds:F0} s.");
            sb.AppendLine();
            sb.AppendLine(BalanceStudy.TableHeader.Replace("{H}", Horizon.ToString()));
            foreach (var r in all) sb.AppendLine(BalanceStudy.Row(r));
            Write(file, sb.ToString());
            TestContext.WriteLine(sb.ToString());
            Assert.AreEqual(Scales.Length * Prices.Length * Versements.Length, all.Count);
        }

        /// <summary>
        /// Réglages candidats rejoués avec le trafic simulé (taxis concurrents et mototaxis qui prennent des clients), puis
        /// sensibilité au comportement du joueur (vitesse, accrochages, négociation), sans trafic.
        /// </summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void Sensitivity_Report()
        {
            var data = GameData.Load(withWorld: true);
            var sb = new StringBuilder();
            sb.AppendLine("Avec le trafic et la concurrence (15 carrières de 14 jours = 210 journées par réglage) :");
            sb.AppendLine();
            sb.AppendLine(BalanceStudy.TableHeader.Replace("{H}", Horizon.ToString()));
            foreach (var (scale, price) in new[] { (1f, 2f), (1.5f, 2f), (1f, 1.5f), (8f, 1f) })
                foreach (var r in BalanceStudy.RunCareers(data, scale, price, Versements, 15, Horizon, new AveragePlayer { WithCity = true }))
                    sb.AppendLine(BalanceStudy.Row(r));

            var players = new (string, AveragePlayer)[]
            {
                ("joueur moyen (35 km/h, 1 accrochage / 60 km)", new AveragePlayer()),
                ("prudent : 30 km/h", new AveragePlayer { CruiseKmh = 30f }),
                ("pressé : 45 km/h", new AveragePlayer { CruiseKmh = 45f }),
                ("aucun accrochage", new AveragePlayer { CrashesPerKm = 0f }),
                ("2 fois plus d'accrochages", new AveragePlayer { CrashesPerKm = 2f / 60f }),
                ("refuse les négociations", new AveragePlayer { AcceptHaggle = false }),
            };
            sb.AppendLine();
            sb.AppendLine("Sensibilité au joueur, sans trafic (40 carrières de 14 jours = 560 journées par ligne) :");
            sb.AppendLine();
            sb.AppendLine("| Réglage | Joueur | Versement atteint (7 000 / 6 000 / 5 000) | Reste moyen (7 000) | Renvoyé en 14 j (7 000) | Épargne à 7 j (7 000) |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var (scale, price) in new[] { (1f, 2f), (1.5f, 2f) })
            foreach (var (name, p) in players)
            {
                var r = BalanceStudy.RunCareers(data, scale, price, new[] { 7000, 6000, 5000 }, 40, Horizon, p);
                sb.AppendLine($"| ×{BalanceStudy.Num(scale)}, prix ×{BalanceStudy.Num(price, "0.##")} | {name} | {BalanceStudy.Pct(r[0].ReachedShare)} / {BalanceStudy.Pct(r[1].ReachedShare)} / {BalanceStudy.Pct(r[2].ReachedShare)} | {BalanceStudy.Money(r[0].MeanLeftover)} | {BalanceStudy.Pct(r[0].FiredByHorizon)} | {BalanceStudy.Money(r[0].MeanSavings7)} |");
            }
            Write("sensibilite.md", sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Hors grille, pour comprendre (aucune valeur changée) : prix ×1 et carburant ×1, sans accrochage, puis aussi sans
        /// contrôle de police (copie en mémoire de police.json avec enabled = false). Sans trafic, 40 carrières de 14 jours.
        /// </summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void WhatIf_Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine(BalanceStudy.TableHeader.Replace("{H}", Horizon.ToString()).Replace("| Conso |", "| Cas | Conso |").Replace("\n|---|", "\n|---|---|"));
            foreach (var (name, crashes, police) in new[] { ("joueur moyen", 1f / 60f, true), ("sans accrochage", 0f, true), ("sans accrochage ni contrôle", 0f, false) })
            {
                var data = GameData.Load();
                data.Police.enabled = police;
                foreach (var r in BalanceStudy.RunCareers(data, 1f, 1f, Versements, 40, Horizon, new AveragePlayer { CrashesPerKm = crashes }))
                    sb.AppendLine("| " + name + " " + BalanceStudy.Row(r));
            }
            Write("hypotheses.md", sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Réglage du jeu tel qu'il est dans les données (décision de Kevin du 21/11 : carburant ×1, prix ×2 soit
        /// 600 à 1 600 FCFA, versement 6 000) : 200 carrières de 14 jours sans trafic et 15 avec le trafic, versements
        /// 5 000 / 6 000 / 7 000 pour situer ; avec le renouvellement automatique des papiers (décision du 21/11) et sans
        /// (le jeu d'avant). Rapport : equilibrage/decision.md.
        /// </summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void Decision_Report()
        {
            var sb = new StringBuilder();
            var current = Current(GameData.Load());
            sb.AppendLine($"Données du jeu : conso ×{BalanceStudy.Num(current.ConsumptionScale)}, fares.json ×1, versement {BalanceStudy.Money(current.Versement)} (day.json).");
            sb.AppendLine();
            sb.AppendLine(BalanceStudy.TableHeader.Replace("{H}", Horizon.ToString()).Replace("| Conso |", "| Cas | Conso |").Replace("\n|---|", "\n|---|---|"));
            foreach (var (name, withCity, careers) in new[] { ("sans trafic, 200 carrières", false, 200), ("avec trafic, 15 carrières", true, 15) })
            foreach (var renew in new[] { true, false })
            {
                var data = GameData.Load(withWorld: withCity);
                data.Police.autoRenewPapers &= renew; // false : papiers jamais renouvelés (le jeu avant la décision du 21/11)
                var label = name + (data.Police.autoRenewPapers ? ", papiers renouvelés" : ", papiers jamais renouvelés");
                foreach (var r in BalanceStudy.RunCareers(data, current.ConsumptionScale, 1f, Versements, careers, Horizon, new AveragePlayer { WithCity = withCity }))
                    sb.AppendLine("| " + label + " " + BalanceStudy.Row(r));
            }
            Write("decision.md", sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Montants de renouvellement des papiers (décision de Kevin du 21/11 : « montants bas, pour rester vers 60 à 70 % »).
        /// Réglage des données (conso, prix, versement), plusieurs jeux de montants (assurance / visite technique /
        /// licence), payés sur l'épargne réelle du matin (épargne insuffisante : papier expiré, nouvel essai le lendemain).
        /// Horizons 14 jours (comme les autres rapports : seule l'assurance arrive à échéance, le 13ᵉ jour) et 45 jours
        /// (assurance les 13ᵉ et 43ᵉ jours, visite technique le 26ᵉ, licence le 41ᵉ). Sans trafic : 200 carrières,
        /// versements 5 000 / 6 000 / 7 000 ; avec le trafic : 15 carrières, versement des données. Rapport : equilibrage/papiers.md.
        /// </summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void Papers_Report()
        {
            var baseData = GameData.Load();
            var current = Current(baseData);
            var sb = new StringBuilder();
            sb.AppendLine("| Montants (assurance / visite / licence) | Cas | Horizon | Versement | Versement atteint | Renvoyé à l'horizon | Épargne à 7 j (moy. / méd.) | Renouvellements payés / jour | Journées avec un papier impayé | Contrôles avec papier expiré / jour |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (a, v, l) in PaperCandidates)
            {
                var costs = new Dictionary<string, int> { { "assurance", a }, { "visite-technique", v }, { "licence-taxi", l } };
                var label = $"{BalanceStudy.Money(a)} / {BalanceStudy.Money(v)} / {BalanceStudy.Money(l)}";
                foreach (var (name, withCity, careers, versements) in new[]
                         {
                             ("sans trafic", false, 200, Versements),
                             ("avec trafic", true, 15, new[] { current.Versement }),
                         })
                foreach (var horizon in new[] { Horizon, 45 })
                {
                    var data = BalanceStudy.WithRenewalCosts(GameData.Load(withWorld: withCity), costs);
                    foreach (var r in BalanceStudy.RunCareers(data, current.ConsumptionScale, 1f, versements, careers, horizon, new AveragePlayer { WithCity = withCity }))
                    {
                        var line = $"| {label} | {name}, {careers} carrières | {horizon} j | {BalanceStudy.Money(r.Setting.Versement)} | {BalanceStudy.Pct(r.ReachedShare)} | {BalanceStudy.Pct(r.FiredByHorizon)} | " +
                                   $"{BalanceStudy.Money(r.MeanSavings7)} / {BalanceStudy.Money(r.MedianSavings7)} | {BalanceStudy.Money(r.MeanRenewalCost)} | {BalanceStudy.Pct(r.DaysWithUnpaidPaper)} | {BalanceStudy.Num(r.ExpiredControlsPerDay, "0.###")} |";
                        sb.AppendLine(line);
                        TestContext.Progress.WriteLine(line);
                    }
                }
            }
            Write("papiers.md", sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Jeux de montants essayés par <see cref="Papers_Report"/> (FCFA : assurance, visite technique, licence). Retenu le
        /// 22/11 et mis dans police.json : 500 / 1 000 / 2 000 (docs/tech/equilibrage.md).
        /// </summary>
        public static readonly (int, int, int)[] PaperCandidates =
        {
            (0, 0, 0), (500, 1000, 2000), (1000, 2000, 4000), (1500, 3000, 6000), (2500, 5000, 10000), (5000, 10000, 20000),
        };

        /// <summary>Ce qui se passe dans une journée type, réglage des données (conso et prix de fuel.json et fares.json), carrières de 14 jours (pour la méthode) : contrôles, incidents, disputes, concurrence.</summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void DayContents_Report()
        {
            var data = GameData.Load(withWorld: true);
            var sb = new StringBuilder();
            sb.AppendLine("| Joueur | Journées | Courses | Recette | km | Contrôles | dont papier expiré | Payé à la police | Crevaisons | Pannes | Accrochages | Négociations | Refus de payer | Clients pris par d'autres | Frais imprévus |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var breakdown = new StringBuilder();
            foreach (var (name, p, careers) in new[] { ("sans trafic", new AveragePlayer(), 40), ("avec trafic", new AveragePlayer { WithCity = true }, 4) })
            {
                var days = new List<StudyDay>();
                for (var k = 0; k < careers; k++)
                    days.AddRange(BalanceStudy.PlayCareer(data, Current(data), p, Horizon, 500 + k));
                double n = days.Count;
                var byLabel = new SortedDictionary<string, double>();
                foreach (var d in days)
                    foreach (var e in d.Summary.Expenses)
                        byLabel[e.Label] = (byLabel.TryGetValue(e.Label, out var t) ? t : 0) + e.Amount;
                breakdown.AppendLine($"Frais imprévus par journée ({name}) : " + string.Join(" ; ", byLabel.Select(kv => $"{kv.Key} {BalanceStudy.Money(kv.Value / n)}")) + ".");
                sb.AppendLine($"| {name} | {n} | {BalanceStudy.Num(days.Average(d => d.Summary.RideCount))} | {BalanceStudy.Money(days.Average(d => d.Summary.Revenue))} | {BalanceStudy.Num(days.Average(d => d.Summary.Kilometres))} | " +
                    $"{BalanceStudy.Num(days.Average(d => d.Controls), "0.##")} | {BalanceStudy.Num(days.Average(d => d.ExpiredPaperControls), "0.##")} | {BalanceStudy.Money(days.Average(d => d.PolicePaid))} | {BalanceStudy.Num(days.Average(d => d.Punctures), "0.##")} | {BalanceStudy.Num(days.Average(d => d.Breakdowns), "0.##")} | " +
                    $"{BalanceStudy.Num(days.Average(d => d.Crashes), "0.##")} | {BalanceStudy.Num(days.Average(d => d.Haggles), "0.##")} | {BalanceStudy.Num(days.Average(d => d.Refusals), "0.##")} | {BalanceStudy.Num(days.Average(d => d.ClientsLostToOthers), "0.#")} | {BalanceStudy.Money(days.Average(d => d.Summary.ExpensesTotal))} |");
            }
            sb.AppendLine().Append(breakdown);
            Write("journee-type.md", sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        private static void Write(string name, string text)
        {
            var dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "equilibrage");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text);
        }
    }
}
