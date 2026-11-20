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

        /// <summary>Durée de l'avertissement de fin de journée (minutes de jeu).</summary>
        public const int LastHourMinutes = 60;

        /// <summary>Dernière heure de jeu avant l'heure de fin : l'horloge passe à l'orange et bat doucement.</summary>
        public static bool LastHour(DayClock clock) => !clock.IsOver && clock.MinutesLeft <= LastHourMinutes;

        /// <summary>Rappel affiché au moment où commence la dernière heure.</summary>
        public const string LastHourMessage = "Plus qu'une heure : choisis bien tes derniers clients";

        /// <summary>
        /// Rappel qui reste affiché après l'heure de fin tant que des passagers sont à bord ; vide sinon.
        /// </summary>
        public static string EndOfDayReminder(DayClock clock, int passengersOnBoard)
        {
            if (!clock.IsOver || passengersOnBoard <= 0) return "";
            var who = passengersOnBoard == 1 ? "ton passager" : $"tes {passengersOnBoard} passagers";
            return $"Journée finie : dépose {who}, ou arrête-toi pour terminer";
        }

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
