using System;
using System.Collections.Generic;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
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

        /// <summary>La carrière sauvegardée (jour, épargne, manques, renvoi).</summary>
        public static DriverCareer LoadCareer(SaveData data, OwnerConfig owner) =>
            DriverCareer.Restore(owner, data.career.dayNumber, data.career.savings, data.career.missedVersementDays, data.career.fired);

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
            };
            var history = new List<DayRecordData>(data.career.history ?? Array.Empty<DayRecordData>()) { record };
            data.career.history = history.ToArray();
            data.career.dayNumber = career.DayNumber;
            data.career.savings = career.Savings;
            data.career.missedVersementDays = new List<int>(career.MissedDays).ToArray();
            data.career.fired = career.Fired;
            data.hasDayInProgress = false;
            data.day = new DayProgressData();
        }
    }
}
