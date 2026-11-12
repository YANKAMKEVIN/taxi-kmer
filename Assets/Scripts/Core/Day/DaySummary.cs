using System;
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

        /// <summary>Le versement est couvert par les recettes.</summary>
        public bool VersementReached => Revenue >= Versement;
        /// <summary>Ce qui manque aux recettes pour couvrir le versement (0 s'il est couvert).</summary>
        public int VersementShortfall => Math.Max(0, Versement - Revenue);

        public static DaySummary From(DayLedger ledger, int unpaidPassengers = 0, int unpaidFares = 0)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            return new DaySummary
            {
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
