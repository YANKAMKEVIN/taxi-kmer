// Équilibrage (dotnet seulement) : un chauffeur automatique joue des journées types sur les vraies données
// (arrêts, prix, places, clients, carburant, propriétaire) pour chiffrer recettes, carburant et versement.
// Ne change aucune valeur de game design : sert à documenter les chiffres pour que Kevin tranche (jalon-03.md).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.DotnetTests
{
    /// <summary>Résultat d'une journée simulée.</summary>
    public struct SimDay
    {
        public int Revenue;
        public int Rides;
        public float Km;
        public float LitresBurned;
        public int FuelBoughtCost;
        public float TankEnd;
        /// <summary>Passagers à bord en moyenne sur la journée.</summary>
        public float Occupancy;
        public DaySummary Summary;
    }

    /// <summary>
    /// Chauffeur automatique : fait l'aller-retour dispensaire ↔ Étoudi à vitesse constante, accepte tout client qui
    /// annonce sa destination, s'arrête pour faire monter et descendre (quelques secondes), fait demi-tour aux deux bouts,
    /// passe à la station quand le réservoir descend sous un seuil. Pas de trafic, pas de feux, pas d'accident : c'est
    /// une journée « propre », plutôt optimiste pour les recettes.
    /// </summary>
    public sealed class BalanceBot
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };
        public static T Load<T>(string name) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", name)), Opts);

        public float CruiseKmh = 40f;
        public float StopSeconds = 4f;
        public float UTurnSeconds = 8f;
        /// <summary>Passe à la station sous cette part du réservoir.</summary>
        public float RefuelBelow = 0.25f;
        /// <summary>Remplit jusqu'à cette part du réservoir (par appuis successifs).</summary>
        public float RefuelUpTo = 0.5f;
        public float ConsumptionScale = -1f; // < 0 : valeur de fuel.json
        public float ClientsFactor = 1f;
        /// <summary>Durée de la journée (s réelles) ; &lt; 0 : valeur de day.json.</summary>
        public float DayLengthSeconds = -1f;

        public SimDay Play(int seed, float startLitres)
        {
            var day = Load<DayConfig>("day.json");
            if (DayLengthSeconds > 0f) day.dayLengthRealSeconds = DayLengthSeconds;
            var fuel = Load<FuelConfig>("fuel.json");
            if (ConsumptionScale >= 0f) fuel.consumptionScale = ConsumptionScale;
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var shift = new Shift(day, Load<SeatingConfig>("seating.json"), fuel, startLitres);
            var routeLength = stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters;
            var street = new Street(Load<StreetConfig>("street.json"), stops, new ClientFactory(stops, Load<FareConfig>("fares.json"), seed), shift, routeLength)
            {
                SpawnRateScale = ClientsFactor,
            };

            const float dt = 0.25f;
            var along = 0f;
            var dir = 1;
            var wait = 0f;
            var burned = 0f;
            var guard = 0;
            var seatSeconds = 0f;
            var seconds = 0f;
            while (!street.DayFinished && guard++ < 1000000)
            {
                // Panne sèche hors de la station : le chauffeur automatique appelle le technicien du quartier.
                if (street.DryTankChoice(new TaxiOnRoute { Along = along, Direction = dir, Lateral = dir * 1.7f })) street.CallTechnician(out _);
                var moving = wait <= 0f;
                var speed = moving ? Math.Min(CruiseKmh, shift.Tank.SpeedCapKmh) : 0f;
                var taxi = new TaxiOnRoute { Along = along, Direction = dir, Lateral = dir * 1.7f, SpeedKmh = speed };
                shift.Clock.Advance(dt);
                var meters = speed / 3.6f * dt;
                var before = shift.Tank.Litres;
                shift.Drive(meters);
                burned += before - shift.Tank.Litres;
                street.Update(dt, taxi);
                seatSeconds += shift.Cabin.PassengerCount * dt;
                seconds += dt;
                if (street.DayFinished) break;

                if (!moving)
                {
                    wait -= dt;
                    continue;
                }
                street.Honk(taxi, out _);

                // S'arrêter ? (descente, montée, station, fin de journée avec passagers)
                var stopped = new TaxiOnRoute { Along = along, Direction = dir, Lateral = dir * 1.7f, SpeedKmh = 0f };
                var action = street.AvailableAction(stopped);
                var wantsFuel = action.Kind == StreetActionKind.Refuel && shift.Tank.Fraction < RefuelBelow;
                if (action.Kind == StreetActionKind.Drop || action.Kind == StreetActionKind.Board
                    || action.Kind == StreetActionKind.EndDay || wantsFuel)
                {
                    if (wantsFuel)
                        while (shift.Tank.Fraction < RefuelUpTo && street.AvailableAction(stopped).Kind == StreetActionKind.Refuel)
                            street.Perform(stopped);
                    else street.Perform(stopped);
                    wait = StopSeconds;
                    continue;
                }

                along += dir * meters;
                if (along >= routeLength) { along = routeLength; dir = -1; wait = UTurnSeconds; }
                else if (along <= 0f) { along = 0f; dir = 1; wait = UTurnSeconds; }
            }

            var s = street.Summary;
            return new SimDay
            {
                Revenue = s.Revenue,
                Rides = s.RideCount,
                Km = s.Kilometres,
                LitresBurned = burned,
                FuelBoughtCost = s.FuelCost,
                TankEnd = shift.Tank.Litres,
                Occupancy = seconds > 0f ? seatSeconds / seconds : 0f,
                Summary = s,
            };
        }
    }

    public class BalanceSimulationTests
    {
        private static string F(int v) => DaySummary.Money(v).Replace(" FCFA", "");

        [Test]
        public void TypicalDays_FuelVersusVersement_Report()
        {
            var day = BalanceBot.Load<DayConfig>("day.json");
            var fuel = BalanceBot.Load<FuelConfig>("fuel.json");
            var report = new StringBuilder();
            report.AppendLine($"Journée de {day.dayLengthRealSeconds:F0} s réelles, versement {F(day.versement)}, {day.fuelPricePerLitre} FCFA/L, {day.fuelLitresPer100Km} L/100 km.");
            report.AppendLine("vitesse | conso ×   | courses | à bord (moy.) | recette | km   | carburant brûlé (FCFA) | reste après carburant | versement atteint ? | journées où il l'est");

            foreach (var kmh in new[] { 30f, 40f, 50f })
            foreach (var scale in new[] { 1f, 4f, fuel.consumptionScale })
            {
                double rev = 0, rides = 0, km = 0, litres = 0, occ = 0;
                const int seeds = 10;
                var reached = 0;
                for (var seed = 1; seed <= seeds; seed++)
                {
                    var bot = new BalanceBot { CruiseKmh = kmh, ConsumptionScale = scale };
                    var d = bot.Play(seed, fuel.tankLitres); // réservoir plein : on mesure ce qui est brûlé
                    rev += d.Revenue; rides += d.Rides; km += d.Km; litres += d.LitresBurned; occ += d.Occupancy;
                    if (d.Revenue - (int)Math.Round(d.LitresBurned * day.fuelPricePerLitre) >= day.versement) reached++;
                }
                rev /= seeds; rides /= seeds; km /= seeds; litres /= seeds; occ /= seeds;
                var fuelCost = (int)Math.Round(litres * day.fuelPricePerLitre);
                var net = (int)Math.Round(rev) - fuelCost;
                report.AppendLine($"{kmh,4:F0} km/h | ×{scale,-7:0.#} | {rides,7:F1} | {occ,13:F1} | {F((int)rev),7} | {km,4:F1} | {F(fuelCost),22} | {F(net),21} | {(net >= day.versement ? "oui" : $"non, il manque {F(day.versement - net)}")} | {reached}/{seeds}");
                Assert.Greater(rides, 0, "Le chauffeur automatique fait des courses.");
            }
            TestContext.WriteLine(report.ToString());
            WriteReport("equilibrage-journees.txt", report.ToString());
        }

        /// <summary>
        /// Piste B (décision de Kevin, 2026-11-21) : journée de 30 à 40 min réelles, carburant réaliste inchangé.
        /// Pour chaque durée, 30 journées à 40 km/h avec la consommation de fuel.json : part des journées où la recette
        /// moins le carburant brûlé couvre le versement (et, pour comparaison, où la recette seule le couvre).
        /// </summary>
        [Test]
        public void DayLength_VersementReachable_Report()
        {
            var day = BalanceBot.Load<DayConfig>("day.json");
            var fuel = BalanceBot.Load<FuelConfig>("fuel.json");
            var report = new StringBuilder();
            report.AppendLine($"40 km/h, consommation ×{fuel.consumptionScale:0.#} ({day.fuelLitresPer100Km} L/100 km, {day.fuelPricePerLitre} FCFA/L), versement {F(day.versement)}, 30 journées par durée.");
            report.AppendLine("durée | courses | recette moy. (min-max) | km | carburant brûlé | reste après carburant | versement atteint après carburant | recette seule ≥ versement");
            const int seeds = 30;
            foreach (var minutes in new[] { 10, 30, 35, 40 })
            {
                double rev = 0, rides = 0, km = 0, fuelCost = 0;
                int min = int.MaxValue, max = 0, reached = 0, reachedNoFuel = 0;
                for (var seed = 1; seed <= seeds; seed++)
                {
                    var d = new BalanceBot { DayLengthSeconds = minutes * 60f }.Play(seed, fuel.tankLitres);
                    var cost = (int)Math.Round(d.LitresBurned * day.fuelPricePerLitre);
                    rev += d.Revenue; rides += d.Rides; km += d.Km; fuelCost += cost;
                    min = Math.Min(min, d.Revenue); max = Math.Max(max, d.Revenue);
                    if (d.Revenue - cost >= day.versement) reached++;
                    if (d.Revenue >= day.versement) reachedNoFuel++;
                }
                rev /= seeds; rides /= seeds; km /= seeds; fuelCost /= seeds;
                var mark = Math.Abs(minutes * 60f - day.dayLengthRealSeconds) < 1f ? " (day.json)" : "";
                report.AppendLine($"{minutes} min{mark} | {rides:F1} | {F((int)rev)} ({F(min)}-{F(max)}) | {km:F1} | {F((int)fuelCost)} | {F((int)(rev - fuelCost))} | {reached}/{seeds} | {reachedNoFuel}/{seeds}");
                Assert.Greater(rides, 0);
            }
            TestContext.WriteLine(report.ToString());
            WriteReport("equilibrage-duree.txt", report.ToString());
        }

        [Test]
        public void Week_WithOwner_DefaultValues_Report()
        {
            var fuel = BalanceBot.Load<FuelConfig>("fuel.json");
            var calendar = new Calendar(BalanceBot.Load<CalendarConfig>("calendar.json"));
            var report = new StringBuilder();
            var dayCfg = BalanceBot.Load<DayConfig>("day.json");
            report.AppendLine($"Une semaine (journée de {dayCfg.dayLengthRealSeconds / 60f:0.#} min, 40 km/h, valeurs par défaut des données, passage à la station sous 25 %) :");
            report.AppendLine("jour | courses | recette | carburant acheté | versement remis | manque | épargne | propriétaire");
            var career = new DriverCareer(BalanceBot.Load<OwnerConfig>("owner.json"));
            var tank = fuel.startLitres;
            for (var i = 0; i < 7 && !career.Fired; i++)
            {
                var today = calendar.For(career.DayNumber);
                var d = new BalanceBot { ClientsFactor = today.ClientsFactor }.Play(100 + i, tank);
                tank = d.TankEnd;
                var c = career.CloseDay(d.Summary);
                report.AppendLine($"{c.DayNumber} {Calendar.Short(today),-4} | {d.Rides,7} | {F(d.Revenue),7} | {F(d.FuelBoughtCost),16} | {F(c.VersementPaid),15} | {F(c.VersementMissing),6} | {F(c.SavingsAfter),7} | {c.Mood}");
            }
            TestContext.WriteLine(report.ToString());
            WriteReport("equilibrage-semaine.txt", report.ToString());
            Assert.GreaterOrEqual(career.DayNumber, 2);
        }

        private static void WriteReport(string name, string text)
        {
            var dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "equilibrage");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text);
        }
    }
}
