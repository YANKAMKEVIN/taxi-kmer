using System;
using System.Collections.Generic;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Career
{
    /// <summary>
    /// Règles du propriétaire du taxi (données : Assets/Resources/Data/owner.json). Décision de Kevin (2026-11-15) :
    /// si le versement n'est pas atteint, le propriétaire menace, et renvoie le chauffeur s'il n'y arrive pas au bout
    /// d'un certain temps. Les nombres sont des valeurs par défaut, à régler avec Kevin.
    /// </summary>
    [Serializable]
    public class OwnerConfig
    {
        /// <summary>Renvoyé quand il a manqué le versement ce nombre de fois…</summary>
        public int missesBeforeFired = 3;
        /// <summary>… sur les … derniers jours (les manques plus anciens sont oubliés).</summary>
        public int windowDays = 7;
        /// <summary>
        /// Si la recette du jour ne suffit pas, le chauffeur complète le versement avec son épargne (sinon il ne
        /// verse que la recette et le reste manque). Choix de Claude, à confirmer par Kevin.
        /// </summary>
        public bool payShortfallFromSavings = true;
    }

    /// <summary>Humeur du propriétaire après la journée.</summary>
    public enum OwnerMood
    {
        /// <summary>Versement payé en entier.</summary>
        Satisfied,
        /// <summary>Versement manqué : il menace (avertissement n sur missesBeforeFired - 1).</summary>
        Threatens,
        /// <summary>Trop de versements manqués : il reprend son taxi.</summary>
        Fired,
    }

    /// <summary>Ce que la fin de journée a changé à la carrière.</summary>
    public struct DayClosure
    {
        public int DayNumber;
        public int SavingsBefore;
        public int SavingsAfter;
        /// <summary>Versement effectivement remis au propriétaire.</summary>
        public int VersementPaid;
        /// <summary>Ce qui manque au versement (0 s'il est payé).</summary>
        public int VersementMissing;
        /// <summary>Pris sur l'épargne pour compléter le versement.</summary>
        public int TakenFromSavings;
        public OwnerMood Mood;
        /// <summary>Versements manqués dans la fenêtre, celui du jour compris.</summary>
        public int RecentMisses;
        public int MissesBeforeFired;
    }

    /// <summary>
    /// La carrière d'un jour à l'autre : l'argent qui reste au chauffeur passe d'un jour à l'autre (décision de Kevin,
    /// 2026-11-15), le versement est dû au propriétaire chaque jour, et les manques mènent au renvoi.
    /// </summary>
    public sealed class DriverCareer
    {
        private readonly OwnerConfig _owner;
        private readonly List<int> _missedDays = new List<int>();

        /// <summary>Jour en cours (1 au départ).</summary>
        public int DayNumber { get; private set; } = 1;
        /// <summary>Argent du chauffeur, reporté d'un jour à l'autre (peut être négatif après des frais imprévus).</summary>
        public int Savings { get; private set; }
        /// <summary>Le propriétaire a repris son taxi : la carrière s'arrête.</summary>
        public bool Fired { get; private set; }
        /// <summary>Jours où le versement a manqué (encore dans la fenêtre ou non).</summary>
        public IReadOnlyList<int> MissedDays => _missedDays;

        public DriverCareer(OwnerConfig owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Reprend une carrière sauvegardée.</summary>
        public static DriverCareer Restore(OwnerConfig owner, int dayNumber, int savings, IEnumerable<int> missedDays, bool fired)
        {
            var c = new DriverCareer(owner) { DayNumber = Math.Max(1, dayNumber), Savings = savings, Fired = fired };
            if (missedDays != null) c._missedDays.AddRange(missedDays);
            return c;
        }

        /// <summary>Versements manqués dans la fenêtre qui se termine au jour <paramref name="day"/>.</summary>
        public int MissesInWindow(int day)
        {
            var n = 0;
            foreach (var d in _missedDays)
                if (d <= day && d > day - Math.Max(1, _owner.windowDays)) n++;
            return n;
        }

        /// <summary>
        /// Clôt la journée : recettes moins carburant et frais imprévus, versement payé (complété par l'épargne si
        /// permis), reste ajouté à l'épargne ; manque → menace, ou renvoi au bout de trop de manques. Puis jour suivant.
        /// </summary>
        public DayClosure CloseDay(DaySummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (Fired) throw new InvalidOperationException("Le chauffeur a été renvoyé : plus de journée.");

            var before = Savings;
            // Ce qui reste de la recette une fois le carburant et les frais payés (ils se paient dans la journée).
            var afterCosts = summary.Revenue - summary.FuelCost - summary.ExpensesTotal;
            var fromDay = Math.Max(0, Math.Min(summary.Versement, afterCosts));
            var missing = summary.Versement - fromDay;
            var fromSavings = 0;
            if (missing > 0 && _owner.payShortfallFromSavings)
            {
                fromSavings = Math.Max(0, Math.Min(missing, before + Math.Min(0, afterCosts)));
                missing -= fromSavings;
            }
            var paid = fromDay + fromSavings;
            Savings = before + afterCosts - paid;

            var mood = OwnerMood.Satisfied;
            if (missing > 0)
            {
                _missedDays.Add(DayNumber);
                mood = MissesInWindow(DayNumber) >= _owner.missesBeforeFired ? OwnerMood.Fired : OwnerMood.Threatens;
            }
            // Arrêté et taxi saisi par la gendarmerie : le propriétaire le renvoie aussi (son taxi est à la fourrière).
            if (summary.VehicleSeized) mood = OwnerMood.Fired;

            var closure = new DayClosure
            {
                DayNumber = DayNumber,
                SavingsBefore = before,
                SavingsAfter = Savings,
                VersementPaid = paid,
                VersementMissing = missing,
                TakenFromSavings = fromSavings,
                Mood = mood,
                RecentMisses = MissesInWindow(DayNumber),
                MissesBeforeFired = _owner.missesBeforeFired,
            };
            if (mood == OwnerMood.Fired) Fired = true;
            DayNumber++;
            return closure;
        }
    }
}
