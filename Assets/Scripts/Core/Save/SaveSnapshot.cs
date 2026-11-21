using System;
using System.Collections.Generic;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Police;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Save
{
    /// <summary>Passage de l'état du jeu (journée en cours, bilans) aux données de sauvegarde, et retour.</summary>
    public static class SaveSnapshot
    {
        /// <summary>Photographie la journée en cours dans <paramref name="data"/> (le reste de la carrière est gardé).</summary>
        public static void CaptureDay(SaveData data, Street street, float taxiAlong, int taxiDirection, int seed)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (street == null) throw new ArgumentNullException(nameof(street));
            var shift = street.Shift;
            var expenses = new List<ExpenseData>();
            foreach (var e in shift.Ledger.Expenses) expenses.Add(new ExpenseData { label = e.Label, amount = e.Amount });
            var passengers = new List<PassengerData>();
            foreach (var p in shift.Cabin.Passengers)
                passengers.Add(new PassengerData
                {
                    id = p.Id,
                    destinationStopId = p.DestinationStopId,
                    distanceMeters = p.DistanceMeters,
                    proposedFare = p.ProposedFare,
                    corpulence = (int)p.Corpulence,
                    bench = (int)shift.Cabin.BenchOf(p).Side,
                });
            data.hasDayInProgress = !street.DayFinished;
            data.day = new DayProgressData
            {
                elapsedRealSeconds = shift.Clock.ElapsedRealSeconds,
                revenue = shift.Ledger.Revenue,
                rideCount = shift.Ledger.RideCount,
                metersDriven = shift.Ledger.MetersDriven,
                expenses = expenses.ToArray(),
                passengers = passengers.ToArray(),
                leftUnpaid = street.LeftUnpaid,
                leftUnpaidFares = street.LeftUnpaidFares,
                taxiAlong = taxiAlong,
                taxiDirection = taxiDirection >= 0 ? 1 : -1,
                seed = seed,
                tankLitres = shift.Tank != null ? shift.Tank.Litres : -1f,
                fuelBoughtLitres = shift.Ledger.FuelBoughtLitres,
                fuelBoughtCost = shift.Ledger.FuelBoughtCost,
                reputationDayStart = street.Reputation?.DayStart ?? -1,
                reputation = street.Reputation?.Score ?? -1,
            };
        }

        /// <summary>
        /// Remet la journée sauvegardée dans une rue toute neuve (créée avec la même graine) : heure, comptes,
        /// passagers à bord sur leur banquette. Les clients qui attendent au bord de la route sont ceux de la rue neuve.
        /// Rend le nombre de passagers qui n'ont pas pu être remis (banquettes changées entre deux versions).
        /// </summary>
        public static int RestoreDay(DayProgressData day, Street street)
        {
            if (day == null) throw new ArgumentNullException(nameof(day));
            if (street == null) throw new ArgumentNullException(nameof(street));
            var shift = street.Shift;
            shift.Clock.Restore(day.elapsedRealSeconds);
            var expenses = new List<Expense>();
            foreach (var e in day.expenses ?? Array.Empty<ExpenseData>())
                expenses.Add(new Expense { Label = e.label, Amount = e.amount });
            shift.Ledger.Restore(day.revenue, day.rideCount, day.metersDriven, expenses);
            shift.Ledger.RestoreFuel(day.fuelBoughtLitres, day.fuelBoughtCost);
            if (shift.Tank != null && day.tankLitres >= 0f) shift.Tank.Set(day.tankLitres);
            if (street.Reputation != null && day.reputation >= 0)
                street.Reputation.Restore(day.reputationDayStart >= 0 ? day.reputationDayStart : day.reputation, day.reputation);
            street.RestoreLeftUnpaid(day.leftUnpaid, day.leftUnpaidFares);

            var lost = 0;
            foreach (var p in day.passengers ?? Array.Empty<PassengerData>())
            {
                Client client;
                try
                {
                    client = new Client(p.id, p.destinationStopId, p.distanceMeters, p.proposedFare,
                        Enum.IsDefined(typeof(Corpulence), p.corpulence) ? (Corpulence)p.corpulence : Corpulence.Normal);
                }
                catch (ArgumentException) { lost++; continue; }
                if (street.FindStop(client.DestinationStopId) == null) { lost++; continue; }
                var side = p.bench == (int)BenchSide.Front ? BenchSide.Front : BenchSide.Back;
                if (!shift.Cabin.Restore(client, side)) lost++;
            }
            return lost;
        }

        /// <summary>Carburant au début de la journée : celui laissé la veille, ou le réservoir de départ d'une carrière.</summary>
        public static float TankAtDayStart(SaveData data, Fuel.FuelConfig fuel)
        {
            if (fuel == null) throw new ArgumentNullException(nameof(fuel));
            var saved = data?.career?.fuelLitres ?? -1f;
            return saved >= 0f ? saved : fuel.startLitres;
        }

        /// <summary>Réputation au début de la journée : celle de la veille, ou celle du départ d'une carrière.</summary>
        public static DriverReputation LoadReputation(SaveData data, ReputationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var saved = data?.career?.reputation ?? -1;
            return new DriverReputation(config, saved >= 0 ? saved : (int?)null);
        }

        /// <summary>
        /// Les papiers de la carrière : ceux du début de carrière (police.json), puis les dates sauvegardées (version 6).
        /// Un papier inconnu des données est ignoré.
        /// </summary>
        public static PaperSet LoadPapers(SaveData data, PoliceConfig police)
        {
            if (police == null) throw new ArgumentNullException(nameof(police));
            var papers = new PaperSet(police);
            foreach (var p in data?.career?.papers ?? Array.Empty<PaperDayData>())
                if (p != null && !string.IsNullOrEmpty(p.id)) papers.Restore(p.id, p.lastValidDay);
            return papers;
        }

        /// <summary>Garde les dates des papiers qui expirent (après un renouvellement).</summary>
        public static void RecordPapers(SaveData data, PaperSet papers)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (papers == null) throw new ArgumentNullException(nameof(papers));
            var list = new List<PaperDayData>();
            foreach (var p in papers.Papers)
                if (p.validityDays > 0) list.Add(new PaperDayData { id = p.id, lastValidDay = papers.LastValidDay(p.id) });
            data.career.papers = list.ToArray();
        }

        /// <summary>
        /// Début de journée (décision de Kevin du 21/11) : les papiers arrivés à échéance se renouvellent, payés sur
        /// l'épargne de <paramref name="career"/> ; dates et épargne gardées dans <paramref name="data"/>. Rend ce qui a
        /// été fait (vide si rien n'était à échéance ou si le renouvellement automatique est coupé).
        /// </summary>
        public static List<PaperRenewalResult> RenewPapersAtDayStart(SaveData data, PaperSet papers, DriverCareer career, PoliceConfig police)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (police == null) throw new ArgumentNullException(nameof(police));
            if (!police.autoRenewPapers) return new List<PaperRenewalResult>();
            var results = PaperRenewal.AtDayStart(papers, career);
            if (results.Count > 0)
            {
                RecordPapers(data, papers);
                data.career.savings = career.Savings;
            }
            return results;
        }

        /// <summary>La carrière sauvegardée (jour, épargne, manques, renvoi).</summary>
        public static DriverCareer LoadCareer(SaveData data, OwnerConfig owner) =>
            DriverCareer.Restore(owner, data.career.dayNumber, data.career.savings, data.career.missedVersementDays, data.career.fired, data.career.impounds);

        /// <summary>
        /// Ajoute le bilan d'une journée terminée et l'état de la carrière après sa clôture (<paramref name="career"/>
        /// est déjà passé au jour suivant) ; plus de journée en cours.
        /// </summary>
        public static void RecordDay(SaveData data, DaySummary summary, DayClosure closure, DriverCareer career)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (career == null) throw new ArgumentNullException(nameof(career));
            var record = new DayRecordData
            {
                dayNumber = closure.DayNumber,
                versementPaid = closure.VersementPaid,
                versementMissing = closure.VersementMissing,
                savingsAfter = closure.SavingsAfter,
                ownerMood = (int)closure.Mood,
                revenue = summary.Revenue,
                rideCount = summary.RideCount,
                versement = summary.Versement,
                kilometres = summary.Kilometres,
                fuelLitres = summary.FuelLitres,
                fuelCost = summary.FuelCost,
                expensesTotal = summary.ExpensesTotal,
                remaining = summary.Remaining,
                unpaidPassengers = summary.UnpaidPassengers,
                unpaidFares = summary.UnpaidFares,
                arrested = summary.Arrested,
                vehicleSeized = summary.VehicleSeized,
                reputation = summary.Reputation,
                reputationChange = summary.ReputationChange,
            };
            var history = new List<DayRecordData>(data.career.history ?? Array.Empty<DayRecordData>()) { record };
            data.career.history = history.ToArray();
            data.career.dayNumber = career.DayNumber;
            data.career.savings = career.Savings;
            data.career.missedVersementDays = new List<int>(career.MissedDays).ToArray();
            data.career.fired = career.Fired;
            data.career.impounds = career.Impounds;
            if (summary.TankLitres >= 0f) data.career.fuelLitres = summary.TankLitres;
            if (summary.Reputation >= 0) data.career.reputation = summary.Reputation;
            data.hasDayInProgress = false;
            data.day = new DayProgressData();
        }
    }
}
