using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Écran titre (idée n° 10) : « Continuer » une carrière commencée (jour, épargne, réputation), « Nouvelle partie »
    /// avec confirmation (la carrière repart de zéro), « Réglages ». La reprise d'une journée interrompue reste derrière
    /// save.json → resumeOnStart (coupée, activation à valider par Kevin).
    /// </summary>
    public static class TitleScreen
    {
        /// <summary>Une carrière existe : au moins une journée finie, ou une journée en cours sauvegardée.</summary>
        public static bool HasCareer(SaveData data) =>
            data != null && data.career != null && (data.career.dayNumber > 1 || data.hasDayInProgress || data.career.savings != 0);

        public static string PrimaryLabel(SaveData data) => HasCareer(data) ? "Continuer" : "Commencer la journée";

        /// <summary>Ligne sous le titre : où en est la carrière, ou le sous-titre du jeu pour une première partie.</summary>
        public static string Subtitle(SaveData data)
        {
            if (!HasCareer(data)) return "Une journée de taxi, de Messassi à Étoudi";
            var c = data.career;
            var line = $"Jour {c.dayNumber} · épargne {Day.DaySummary.Money(c.savings)}";
            if (c.reputation >= 0) line += $" · réputation {c.reputation}";
            return line;
        }

        /// <summary>Libellé du bouton « Nouvelle partie » : le second appui confirme.</summary>
        public static string NewCareerLabel(bool confirming) =>
            confirming ? "Confirmer : tout recommencer (l'épargne est perdue)" : "Nouvelle partie";
    }
}
