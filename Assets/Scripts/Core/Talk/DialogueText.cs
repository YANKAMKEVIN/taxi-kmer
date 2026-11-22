using System.Collections.Generic;
using System.Text;

namespace TaxiKmer.Core.Talk
{
    /// <summary>Noms des valeurs qu'une réplique peut citer entre accolades (« Je te donne {offre} »).</summary>
    public static class TalkValues
    {
        /// <summary>Destination du passager qui parle (nom court du lieu : « Emana »).</summary>
        public const string Destination = "destination";
        /// <summary>Prix annoncé de la course (« 600 francs »).</summary>
        public const string Price = "prix";
        /// <summary>Contre-offre du client qui négocie (« 450 francs »).</summary>
        public const string Offer = "offre";

        public static readonly string[] All = { Destination, Price, Offer };

        /// <summary>
        /// Une somme telle qu'un passager la dit : « 600 francs », « 1 200 francs » (espace des milliers). « francs » et
        /// non « FCFA » : on parle ainsi dans le taxi (valeur par défaut de Claude).
        /// </summary>
        public static string Amount(int fcfa)
        {
            var s = System.Math.Abs((long)fcfa).ToString();
            var sb = new StringBuilder();
            if (fcfa < 0) sb.Append('-');
            for (var i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.Append(" francs").ToString();
        }
    }

    /// <summary>
    /// Valeurs dans les répliques (jalon 5) : une réplique peut citer la destination, le prix ou la contre-offre du
    /// moment entre accolades, pour ne jamais contredire le jeu (« Trois cents pour Emana ? » alors que la course coûte
    /// 800 et va à Étoudi). Une réplique dont une valeur manque n'est pas dite.
    /// </summary>
    public static class DialogueText
    {
        /// <summary>
        /// Valeurs que le jeu fournit pour chaque situation (la présentation les passe à
        /// <see cref="DialogueDirector.Trigger"/>) : toute situation liée à une course connaît la destination et le prix,
        /// la négociation connaît aussi la contre-offre ; les autres (discussion, place, panne, contrôle, pluie, silence)
        /// n'en ont aucune. Sert à vérifier les données : une réplique ne cite que ce que sa situation fournit.
        /// </summary>
        public static string[] ValuesFor(string trigger)
        {
            switch (trigger)
            {
                case TalkTriggers.Haggle:
                    return new[] { TalkValues.Destination, TalkValues.Price, TalkValues.Offer };
                case TalkTriggers.Boarding:
                case TalkTriggers.Price:
                case TalkTriggers.Destination:
                case TalkTriggers.Paid:
                case TalkTriggers.RefusesToPay:
                case TalkTriggers.Detour:
                    return new[] { TalkValues.Destination, TalkValues.Price };
                default:
                    return new string[0];
            }
        }

        /// <summary>Noms cités entre accolades, dans l'ordre (« {prix} pour {destination} ? » → prix, destination).</summary>
        public static List<string> Placeholders(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;
            var i = 0;
            while (i < text.Length)
            {
                var open = text.IndexOf('{', i);
                if (open < 0) break;
                var close = text.IndexOf('}', open + 1);
                if (close < 0) break;
                list.Add(text.Substring(open + 1, close - open - 1));
                i = close + 1;
            }
            return list;
        }

        /// <summary>Toutes les valeurs citées par le texte sont fournies (et non vides).</summary>
        public static bool CanFill(string text, IDictionary<string, string> values)
        {
            foreach (var name in Placeholders(text))
                if (values == null || !values.TryGetValue(name, out var v) || string.IsNullOrEmpty(v)) return false;
            return true;
        }

        /// <summary>Remplace chaque {nom} par sa valeur ; un nom sans valeur reste tel quel (voir <see cref="CanFill"/>).</summary>
        public static string Fill(string text, IDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(text) || values == null || text.IndexOf('{') < 0) return text ?? "";
            var sb = new StringBuilder(text);
            foreach (var kv in values)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                var key = "{" + kv.Key + "}";
                if (StartsWithVowel(kv.Value))
                    foreach (var w in Elided)
                    {
                        // « la route de {destination} » + « Emana » → « la route d'Emana » (élision du français).
                        sb.Replace(" " + w + " " + key, " " + w.Substring(0, w.Length - 1) + "'" + kv.Value);
                        var head = w + " " + key;
                        if (sb.Length >= head.Length && sb.ToString(0, head.Length) == head)
                            sb.Remove(0, head.Length).Insert(0, w.Substring(0, w.Length - 1) + "'" + kv.Value);
                    }
                sb.Replace(key, kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>Mots élidés devant une voyelle (« de Emana » → « d'Emana », « que Étoudi » → « qu'Étoudi »).</summary>
        private static readonly string[] Elided = { "de", "De", "que", "Que" };

        private static bool StartsWithVowel(string s) =>
            !string.IsNullOrEmpty(s) && "AEIOUYÉÈÊÀÂÎÔÛaeiouyéèêàâîôû".IndexOf(s[0]) >= 0;
    }
}
