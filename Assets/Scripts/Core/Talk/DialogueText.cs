using System.Collections.Generic;
using System.Text;

namespace TaxiKmer.Core.Talk
{
    /// <summary>Noms des valeurs qu'une réplique peut citer entre accolades (« Je te donne {offre} »).</summary>
    public static class TalkValues
    {
        /// <summary>
        /// Destination du passager qui parle, forme parlée avec son article (« le dispensaire de Messassi », « Emana ») :
        /// « vers {destination} », « {prix} pour {destination} ». Majuscule ajoutée en début de phrase.
        /// </summary>
        public const string Destination = "destination";
        /// <summary>« à » + destination, contracté (« au dispensaire de Messassi », « à Emana ») : « je descends {a_destination} ».</summary>
        public const string DestinationAt = "a_destination";
        /// <summary>« de » + destination, contracté ou élidé (« du pont d'Emana », « d'Étoudi ») : « la route {de_destination} ».</summary>
        public const string DestinationOf = "de_destination";
        /// <summary>Destination sans article, pour le pidgin (« for Carrefour Messassi »).</summary>
        public const string DestinationBare = "destination_nue";
        /// <summary>Prix annoncé de la course (« 600 francs »).</summary>
        public const string Price = "prix";
        /// <summary>Contre-offre du client qui négocie (« 450 francs »).</summary>
        public const string Offer = "offre";
        /// <summary>Ce que vend le vendeur ambulant (« de l'eau fraîche », idée n° 24).</summary>
        public const string Article = "article";

        public static readonly string[] All = { Destination, DestinationAt, DestinationOf, DestinationBare, Price, Offer };

        /// <summary>Valeurs liées à la destination (fournies ensemble par <see cref="AddPlace"/>).</summary>
        public static readonly string[] Place = { Destination, DestinationAt, DestinationOf, DestinationBare };

        /// <summary>
        /// Les quatre formes parlées d'un lieu (décision de Kevin, 23/11) : celles des données
        /// (<see cref="Rides.Stop.spoken"/>), sinon tirées du nom court (« Emana » → « à Emana », « d'Emana »).
        /// </summary>
        public static Rides.SpokenPlace PlaceForms(Rides.Stop stop, string fallbackName = "")
        {
            var shortName = Ux.StopNames.Short(stop?.name ?? fallbackName ?? "");
            var s = stop?.spoken;
            string Or(string v, string d) => string.IsNullOrEmpty(v) ? d : v;
            var name = Or(s?.name, shortName);
            return new Rides.SpokenPlace
            {
                name = name,
                at = Or(s?.at, string.IsNullOrEmpty(name) ? "" : "à " + name),
                of = Or(s?.of, string.IsNullOrEmpty(name) ? "" : (DialogueText.StartsWithVowel(name) ? "d'" : "de ") + name),
                bare = Or(s?.bare, shortName),
            };
        }

        /// <summary>Ajoute les formes parlées de la destination aux valeurs d'une réplique (rien si le lieu est inconnu).</summary>
        public static void AddPlace(IDictionary<string, string> values, Rides.Stop stop, string fallbackName = "")
        {
            if (values == null) return;
            var f = PlaceForms(stop, fallbackName);
            if (string.IsNullOrEmpty(f.name)) return;
            values[Destination] = f.name;
            values[DestinationAt] = f.at;
            values[DestinationOf] = f.of;
            values[DestinationBare] = f.bare;
        }

        /// <summary>Valeurs d'une course pour les répliques : destination (formes parlées), prix et contre-offre s'il y en a.</summary>
        public static Dictionary<string, string> ForRide(Rides.Stop stop, string stopId, int fare, int offer = 0)
        {
            var values = new Dictionary<string, string>();
            if (stop != null || !string.IsNullOrEmpty(stopId)) AddPlace(values, stop, stopId);
            if (fare > 0) values[Price] = Amount(fare);
            if (offer > 0) values[Offer] = Amount(offer);
            return values;
        }

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
                    return new[] { TalkValues.Destination, TalkValues.DestinationAt, TalkValues.DestinationOf, TalkValues.DestinationBare, TalkValues.Price, TalkValues.Offer };
                case TalkTriggers.Boarding:
                case TalkTriggers.Price:
                case TalkTriggers.Destination:
                case TalkTriggers.Paid:
                case TalkTriggers.RefusesToPay:
                case TalkTriggers.Detour:
                    return new[] { TalkValues.Destination, TalkValues.DestinationAt, TalkValues.DestinationOf, TalkValues.DestinationBare, TalkValues.Price };
                case TalkTriggers.Vendor:
                    return new[] { TalkValues.Article };
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

        /// <summary>
        /// Remplace chaque {nom} par sa valeur ; un nom sans valeur reste tel quel (voir <see cref="CanFill"/>). Une valeur
        /// en début de phrase prend une majuscule (« le carrefour Messassi, s'il vous plaît » → « Le carrefour… »).
        /// </summary>
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
                ReplaceCapitalized(sb, key, kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Remplace chaque <paramref name="key"/> par <paramref name="value"/>, avec une majuscule en début de phrase
        /// (« {a_destination}, tu gares » → « Au dispensaire… », « Que {destination} » garde la minuscule).
        /// </summary>
        private static void ReplaceCapitalized(StringBuilder sb, string key, string value)
        {
            var i = 0;
            while (true)
            {
                var at = sb.ToString().IndexOf(key, i, System.StringComparison.Ordinal);
                if (at < 0) return;
                var v = SentenceStart(sb, at) && value.Length > 0 ? char.ToUpperInvariant(value[0]) + value.Substring(1) : value;
                sb.Remove(at, key.Length).Insert(at, v);
                i = at + v.Length;
            }
        }

        /// <summary>Rien avant, ou une fin de phrase (. ! ? …) juste avant les espaces.</summary>
        private static bool SentenceStart(StringBuilder sb, int index)
        {
            var j = index - 1;
            while (j >= 0 && sb[j] == ' ') j--;
            return j < 0 || ".!?…".IndexOf(sb[j]) >= 0;
        }

        /// <summary>Mots élidés devant une voyelle (« de Emana » → « d'Emana », « que Étoudi » → « qu'Étoudi »).</summary>
        private static readonly string[] Elided = { "de", "De", "que", "Que" };

        internal static bool StartsWithVowel(string s) =>
            !string.IsNullOrEmpty(s) && "AEIOUYÉÈÊÀÂÎÔÛaeiouyéèêàâîôû".IndexOf(s[0]) >= 0;
    }
}
