using System;
using System.Collections.Generic;
using System.Text;

namespace TaxiKmer.Core.Day
{
    /// <summary>
    /// Bilan de fin de journée (tâche 1.6) : photographie des comptes au moment où la journée se termine.
    /// Reste = recettes − versement − carburant (les autres sorties et les charges du foyer viendront plus tard).
    /// </summary>
    public sealed class DaySummary
    {
        public int Revenue { get; private set; }
        public int RideCount { get; private set; }
        public int Versement { get; private set; }
        public float Kilometres { get; private set; }
        public float FuelLitres { get; private set; }
        public int FuelCost { get; private set; }
        public int Remaining { get; private set; }
        /// <summary>Passagers encore à bord quand le chauffeur a terminé (ils n'ont pas payé).</summary>
        public int UnpaidPassengers { get; private set; }
        /// <summary>Ce que ces passagers auraient payé.</summary>
        public int UnpaidFares { get; private set; }
        /// <summary>Dépenses imprévues (soins d'un piéton renversé…).</summary>
        public IReadOnlyList<Expense> Expenses { get; private set; } = Array.Empty<Expense>();
        public int ExpensesTotal { get; private set; }
        /// <summary>La journée s'est terminée par une interpellation de la gendarmerie (garde à vue).</summary>
        public bool Arrested { get; private set; }
        /// <summary>Le taxi a été saisi par la gendarmerie.</summary>
        public bool VehicleSeized { get; private set; }
        /// <summary>Carburant dans le réservoir à la fin de la journée (L) ; -1 sans réservoir.</summary>
        public float TankLitres { get; private set; } = -1f;
        /// <summary>Réputation à la fin de la journée (-1 si elle n'est pas suivie) et sa variation du jour.</summary>
        public int Reputation { get; private set; } = -1;
        public int ReputationChange { get; private set; }

        /// <summary>
        /// Recette qui reste pour le versement une fois payés le carburant et les frais imprévus de la journée (ils se
        /// paient d'abord, comme dans <see cref="Career.DriverCareer.CloseDay"/>) ; peut être négative.
        /// </summary>
        public int TowardVersement => Revenue - FuelCost - ExpensesTotal;
        /// <summary>Le versement est couvert par la journée seule (recettes moins carburant et frais), sans l'épargne.</summary>
        public bool VersementReached => TowardVersement >= Versement;
        /// <summary>Ce qui manque à la journée seule pour couvrir le versement (0 s'il est couvert), avant l'épargne.</summary>
        public int VersementShortfall => Versement - Math.Max(0, Math.Min(Versement, TowardVersement));

        public static DaySummary From(DayLedger ledger, int unpaidPassengers = 0, int unpaidFares = 0,
            bool arrested = false, bool vehicleSeized = false, float tankLitres = -1f,
            int reputation = -1, int reputationChange = 0)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            return new DaySummary
            {
                Expenses = new List<Expense>(ledger.Expenses),
                ExpensesTotal = ledger.ExpensesTotal,
                Arrested = arrested,
                TankLitres = tankLitres,
                Reputation = reputation,
                ReputationChange = reputationChange,
                VehicleSeized = vehicleSeized,
                Revenue = ledger.Revenue,
                RideCount = ledger.RideCount,
                Versement = ledger.Versement,
                Kilometres = ledger.MetersDriven / 1000f,
                FuelLitres = ledger.FuelLitres,
                FuelCost = ledger.FuelCost,
                Remaining = ledger.Remaining,
                UnpaidPassengers = unpaidPassengers,
                UnpaidFares = unpaidFares,
            };
        }

        /// <summary>1200 → « 1 200 FCFA », -500 → « -500 FCFA ».</summary>
        public static string Money(int amount)
        {
            var s = Math.Abs((long)amount).ToString();
            var sb = new StringBuilder();
            if (amount < 0) sb.Append('-');
            for (var i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.Append(" FCFA").ToString();
        }

        /// <summary>3.25 → « 3,3 » (une décimale, virgule française).</summary>
        public static string OneDecimal(float value)
        {
            var tenths = (long)Math.Round(value * 10f, MidpointRounding.AwayFromZero);
            var sign = tenths < 0 ? "-" : "";
            tenths = Math.Abs(tenths);
            return $"{sign}{tenths / 10},{tenths % 10}";
        }
    }
}
