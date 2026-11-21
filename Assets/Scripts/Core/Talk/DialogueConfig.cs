using System;

namespace TaxiKmer.Core.Talk
{
    /// <summary>Situations qui font parler les passagers (feuille de route, jalon 5).</summary>
    public static class TalkTriggers
    {
        /// <summary>Un client monte.</summary>
        public const string Boarding = "montee";
        /// <summary>Discussion entre passagers pendant le trajet (de temps en temps).</summary>
        public const string Chat = "discussion";
        /// <summary>Le chauffeur fait changer quelqu'un de place pour faire monter un client (dispute de place).</summary>
        public const string SeatMove = "place";
        /// <summary>Le taxi s'éloigne de la destination d'un passager (plainte sur le détour).</summary>
        public const string Detour = "detour";
        /// <summary>Un passager descend à sa destination et paie.</summary>
        public const string Paid = "paiement";
        /// <summary>Négociation du prix (conséquence à décider par Kevin : seulement le mécanisme).</summary>
        public const string Haggle = "negociation";
        /// <summary>Client qui ne veut pas payer (conséquence à décider par Kevin : seulement le mécanisme).</summary>
        public const string RefusesToPay = "refus-paiement";

        public static readonly string[] All = { Boarding, Chat, SeatMove, Detour, Paid, Haggle, RefusesToPay };
    }

    /// <summary>Un personnage type (« la commerçante du marché », « l'étudiant »…) : contenu de Kevin.</summary>
    [Serializable]
    public class CharacterData
    {
        public string id = "";
        public string name = "";
    }

    /// <summary>Une réponse rapide du chauffeur (un bouton qui ne bloque pas la conduite).</summary>
    [Serializable]
    public class ReplyData
    {
        public string id = "";
        public string text = "";
        /// <summary>Effet sur la réputation (0 par défaut : les effets des réponses sont à décider par Kevin).</summary>
        public int reputation;
    }

    /// <summary>Une réplique : dite dans une situation, éventuellement par un personnage type, avec des réponses possibles.</summary>
    [Serializable]
    public class LineData
    {
        public string id = "";
        /// <summary>Situation (<see cref="TalkTriggers"/>).</summary>
        public string trigger = "";
        /// <summary>Personnage type qui la dit (vide : n'importe quel passager).</summary>
        public string character = "";
        public string text = "";
        public ReplyData[] replies = Array.Empty<ReplyData>();
    }

    /// <summary>
    /// Dialogues des passagers (jalon 5 ; données : Assets/Resources/Data/dialogues.json). Le mécanisme est codé ;
    /// les répliques sont le contenu de Kevin (question 3 du jalon 5) : celles des données sont des exemples à remplacer.
    /// </summary>
    [Serializable]
    public class DialogueConfig
    {
        /// <summary>Les passagers parlent dans le jeu (false : mécanisme prêt, coupé en attendant les vraies répliques).</summary>
        public bool enabled;
        /// <summary>Lecture en conduisant : une bulle reste au moins … s…</summary>
        public float minShowSeconds = 2.5f;
        /// <summary>… et le temps de lire … caractères par seconde.</summary>
        public float charsPerSecond = 14f;
        /// <summary>Une bulle ne dépasse pas … caractères (plus long : à couper dans les données).</summary>
        public int maxChars = 90;
        /// <summary>Silence d'au moins … s entre deux bulles (une seule bulle à la fois).</summary>
        public float minGapSeconds = 6f;
        /// <summary>Une réplique ne revient pas avant que … autres aient été dites (anti-répétition).</summary>
        public int noRepeatWithin = 12;
        /// <summary>Les réponses rapides restent … s ; sans réponse, la bulle part toute seule (rien ne bloque).</summary>
        public float replySeconds = 4f;
        /// <summary>Avec au moins deux passagers, une discussion démarre en moyenne toutes les … s de trajet (0 : jamais).</summary>
        public float chatEverySeconds = 45f;
        /// <summary>Plainte sur le détour quand le taxi s'est éloigné de … m de la destination d'un passager.</summary>
        public float detourMeters = 150f;
        /// <summary>Chance qu'un client négocie le prix à la montée (0 : jamais ; conséquences à décider par Kevin).</summary>
        public float haggleChance;
        /// <summary>Chance qu'un client refuse de payer à l'arrivée (0 : jamais ; conséquences à décider par Kevin).</summary>
        public float refusesToPayChance;

        public CharacterData[] characters = Array.Empty<CharacterData>();
        public LineData[] lines = Array.Empty<LineData>();
    }
}
