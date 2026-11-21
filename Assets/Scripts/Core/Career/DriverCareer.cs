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
        /// <summary>
        /// Fourrière (jalon 4) : le propriétaire renvoie le chauffeur seulement à la 2ᵉ fois (tranché par Kevin le 21/11) ;
        /// la 1ʳᵉ fois, il menace. 0 : jamais.
        /// </summary>
        public int impoundsBeforeFired = 2;
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
        /// <summary>Fois où le taxi est parti à la fourrière (le propriétaire renvoie à la 2ᵉ).</summary>
        public int Impounds { get; private set; }
        /// <summary>Jours où le versement a manqué (encore dans la fenêtre ou non).</summary>
        public IReadOnlyList<int> MissedDays => _missedDays;

        public DriverCareer(OwnerConfig owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Reprend une carrière sauvegardée.</summary>
        public static DriverCareer Restore(OwnerConfig owner, int dayNumber, int savings, IEnumerable<int> missedDays, bool fired, int impounds = 0)
        {
            var c = new DriverCareer(owner) { DayNumber = Math.Max(1, dayNumber), Savings = savings, Fired = fired, Impounds = Math.Max(0, impounds) };
            if (missedDays != null) c._missedDays.AddRange(missedDays);
            return c;
        }

        /// <summary>
        /// Paie une dépense hors journée sur l'épargne (renouvellement des papiers au début de la journée, décision de
        /// Kevin du 21/11). Le contrôle « assez d'épargne ? » est fait par l'appelant.
        /// </summary>
        public void PayFromSavings(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Savings -= amount;
        }

        /// <summary>Versements manqués dans la fenêtre qui se termine au jour <paramref name="day"/>.</summary>
        public int MissesInWindow(int day)
        {
            var n = 0;
            foreach (var d in _missedDays)
                if (d <= day && d > day - Math.Max(1, _owner.windowDays)) n++;
            return n;
        }

        /// <summary>Versements manqués avant le renvoi, avec l'attitude du propriétaire selon la réputation du bilan.</summary>
        public int MissesBeforeFired(DaySummary summary, ReputationEffectsConfig effects)
        {
            var tolerance = summary != null && summary.Reputation >= 0 ? ReputationEffects.OwnerTolerance(effects, summary.Reputation) : 0;
            return Math.Max(1, _owner.missesBeforeFired + tolerance);
        }

        /// <summary>
        /// Clôt la journée : recettes moins carburant et frais imprévus, versement payé (complété par l'épargne si
        /// permis), reste ajouté à l'épargne ; manque → menace, ou renvoi au bout de trop de manques. Puis jour suivant.
        /// </summary>
        /// <param name="effects">
        /// Effets de la réputation (décision de Kevin du 2026-11-21 : attitude du propriétaire) : avec le score du bilan,
        /// le propriétaire tolère un manque de plus (bonne réputation) ou de moins (mauvaise). Null : aucun effet.
        /// </param>
        public DayClosure CloseDay(DaySummary summary, ReputationEffectsConfig effects = null)
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
                mood = MissesInWindow(DayNumber) >= MissesBeforeFired(summary, effects) ? OwnerMood.Fired : OwnerMood.Threatens;
            }
            // Arrêté et taxi saisi par la gendarmerie : le propriétaire le renvoie aussi (son taxi est à la fourrière).
            if (summary.VehicleSeized) mood = OwnerMood.Fired;
            // Fourrière : il menace la 1ʳᵉ fois, renvoie à la 2ᵉ (décision de Kevin du 21/11).
            if (summary.Impounded)
            {
                Impounds++;
                if (_owner.impoundsBeforeFired > 0 && Impounds >= _owner.impoundsBeforeFired) mood = OwnerMood.Fired;
                else if (mood == OwnerMood.Satisfied) mood = OwnerMood.Threatens;
            }

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
                MissesBeforeFired = MissesBeforeFired(summary, effects),
            };
            if (mood == OwnerMood.Fired) Fired = true;
            DayNumber++;
            return closure;
        }
    }
}
