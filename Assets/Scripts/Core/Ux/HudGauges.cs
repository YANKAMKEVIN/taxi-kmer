using System;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Jauges du HUD : temps de journée restant et versement. Une jauge de versement qui se remplit jusqu'au montant
    /// dû, puis passe au vert avec ce qui dépasse ; les textes viennent d'ici pour être testés.
    /// </summary>
    public static class HudGauges
    {
        /// <summary>Part de la journée qui reste, de 1 (début) à 0 (heure de fin).</summary>
        public static float DayLeft(DayClock clock) => 1f - clock.Progress;

        /// <summary>Remplissage de la jauge de versement, de 0 à 1.</summary>
        public static float VersementFill(int revenue, int versement)
        {
            if (versement <= 0) return 1f;
            return Math.Max(0f, Math.Min(1f, revenue / (float)versement));
        }

        public static bool VersementCovered(int revenue, int versement) => revenue >= versement;

        /// <summary>Légende au-dessus de la jauge : « Versement 10 000 », puis « Versement couvert · +1 500 au-delà ».</summary>
        public static string VersementCaption(int revenue, int versement)
        {
            if (!VersementCovered(revenue, versement)) return $"Versement {Amount(versement)}";
            var extra = revenue - versement;
            return extra > 0 ? $"Versement couvert · +{Amount(extra)} au-delà" : "Versement couvert";
        }

        /// <summary>10000 → « 10 000 » (sans la devise).</summary>
        public static string Amount(int amount) => DaySummary.Money(amount).Replace(" FCFA", "");
    }
}
