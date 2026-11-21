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
            var f = BalanceStudy.ScaleFares(FareConfig.Default(), 1.25f);
            Assert.AreEqual(new[] { 400, 500, 650, 750 }, f.bands.Select(b => b.fare).ToArray()); // 375 → 400, 625 → 650
            Assert.AreEqual(1000, f.longDistanceFare);
            Assert.AreEqual(2000, f.bands[0].maxDistanceMeters);
            var same = BalanceStudy.ScaleFares(FareConfig.Default(), 1f);
            Assert.AreEqual(new[] { 300, 400, 500, 600 }, same.bands.Select(b => b.fare).ToArray());
            Assert.AreEqual(800, same.longDistanceFare);
        }

        [Test]
        public void Settings_AreAppliedToCopies_GameDataUnchanged()
        {
            var data = GameData.Load();
            var s = new BalanceSetting { ConsumptionScale = 1.5f, PriceMultiplier = 2f, Versement = 5000 };
            Assert.AreEqual(1.5f, data.FuelFor(s).consumptionScale);
            Assert.AreEqual(5000, data.DayFor(s).versement);
            Assert.AreEqual(600, data.FaresFor(s).bands[0].fare);
            // Les fichiers du jeu, relus, n'ont pas bougé.
            Assert.AreEqual(Current(data).ConsumptionScale, BalanceBot.Load<Fuel.FuelConfig>("fuel.json").consumptionScale);
            Assert.AreEqual(300, BalanceBot.Load<FareConfig>("fares.json").bands[0].fare);
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
            Assert.Greater(days.Sum(d => d.Summary.FuelCost), 0, "Du carburant acheté au carburant ×8.");
            Assert.That(days.Any(d => d.Reputation != data.Reputation.start), "La réputation bouge et passe d'un jour à l'autre.");
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

        /// <summary>Ce qui se passe dans une journée type, conso ×1 et prix ×2, carrières de 14 jours (pour la méthode) : contrôles, incidents, disputes, concurrence.</summary>
        [Test, Explicit("Long : lancer à la demande"), Category("Equilibrage")]
        public void DayContents_Report()
        {
            var data = GameData.Load(withWorld: true);
            var sb = new StringBuilder();
            sb.AppendLine("| Joueur | Journées | Courses | Recette | km | Contrôles | Payé à la police | Crevaisons | Pannes | Accrochages | Négociations | Refus de payer | Clients pris par d'autres | Frais imprévus |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var breakdown = new StringBuilder();
            foreach (var (name, p, careers) in new[] { ("sans trafic", new AveragePlayer(), 40), ("avec trafic", new AveragePlayer { WithCity = true }, 4) })
            {
                var days = new List<StudyDay>();
                for (var k = 0; k < careers; k++)
                    days.AddRange(BalanceStudy.PlayCareer(data, new BalanceSetting { ConsumptionScale = 1f, PriceMultiplier = 2f, Versement = 7000 }, p, Horizon, 500 + k));
                double n = days.Count;
                var byLabel = new SortedDictionary<string, double>();
                foreach (var d in days)
                    foreach (var e in d.Summary.Expenses)
                        byLabel[e.Label] = (byLabel.TryGetValue(e.Label, out var t) ? t : 0) + e.Amount;
                breakdown.AppendLine($"Frais imprévus par journée ({name}) : " + string.Join(" ; ", byLabel.Select(kv => $"{kv.Key} {BalanceStudy.Money(kv.Value / n)}")) + ".");
                sb.AppendLine($"| {name} | {n} | {BalanceStudy.Num(days.Average(d => d.Summary.RideCount))} | {BalanceStudy.Money(days.Average(d => d.Summary.Revenue))} | {BalanceStudy.Num(days.Average(d => d.Summary.Kilometres))} | " +
                    $"{BalanceStudy.Num(days.Average(d => d.Controls), "0.##")} | {BalanceStudy.Money(days.Average(d => d.PolicePaid))} | {BalanceStudy.Num(days.Average(d => d.Punctures), "0.##")} | {BalanceStudy.Num(days.Average(d => d.Breakdowns), "0.##")} | " +
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
