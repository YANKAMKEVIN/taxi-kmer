using System;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Police
{
    /// <summary>
    /// Textes du contrôle de police à l'écran. **Ton réaliste** (décision de Kevin, 2026-11-21) : on suggère la
    /// corruption sans la montrer crûment. Le policier ne dit jamais ce qu'il demande, la somme n'a pas de nom (ni
    /// « amende » ni « pot-de-vin »), on ne parle pas de reçu ; problème réel ou inventé, la phrase est la même : c'est au
    /// joueur de comprendre. Les mots crus sont interdits dans ces textes et dans police.json (<see cref="CrudeWords"/>,
    /// vérifié par les tests). Formulations provisoires de Claude, à relire par Kevin.
    /// </summary>
    public static class PoliceTexts
    {
        /// <summary>Ce qui ne doit jamais apparaître à l'écran pendant un contrôle (montrer crûment la corruption).</summary>
        public static readonly string[] CrudeWords =
        {
            "corruption", "corrompu", "pot-de-vin", "pots-de-vin", "bakchich", "backchich", "racket", "dessous-de-table", "pourboire",
        };

        /// <summary>« Assurance expirée »… Le policier parle de 3 000 FCFA.</summary>
        public static string Demand(PoliceControl control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (control.Finding.Kind == ControlFindingKind.None) return "Le policier regarde les papiers, longtemps.";
            return $"« {control.Finding.Label} »… Le policier parle de {DaySummary.Money(control.Demand)}.";
        }

        /// <summary>Libellé du bouton d'un choix.</summary>
        public static string Choice(ControlChoice choice) => choice switch
        {
            ControlChoice.Pay => "Payer",
            ControlChoice.Negotiate => "Négocier",
            ControlChoice.Contest => "Contester",
            ControlChoice.Call => "Appeler quelqu'un",
            ControlChoice.Flee => "Fuir",
            _ => "",
        };

        /// <summary>Fin du contrôle : « 2 000 FCFA laissés au contrôle. Bonne route. » (rien de plus explicite).</summary>
        public static string Closed(PoliceControl control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (control.Arrested) return "Le chauffeur est emmené au poste. La journée s'arrête.";
            if (control.Impounded) return control.OutcomeId == "rattrape" ? "Rattrapé. Le taxi part à la fourrière." : "Le taxi part à la fourrière.";
            if (control.BecameWanted) return "Le taxi s'échappe. Mais ils ont relevé la plaque…";
            return control.Paid > 0 ? $"{DaySummary.Money(control.Paid)} laissés au contrôle. Bonne route." : "Bonne route.";
        }

        /// <summary>Le texte contient-il un mot cru (<see cref="CrudeWords"/>) ?</summary>
        public static bool IsCrude(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var lower = text.ToLowerInvariant();
            foreach (var w in CrudeWords)
                if (lower.Contains(w)) return true;
            return false;
        }
    }
}
