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

    /// <summary>
    /// Langues et registres des répliques (décision de Kevin, 2026-11-21 : français, camfranglais et pidgin).
    /// </summary>
    public static class TalkRegisters
    {
        public const string French = "fr";
        public const string Camfranglais = "camfranglais";
        public const string Pidgin = "pidgin";

        public static readonly string[] All = { French, Camfranglais, Pidgin };
    }

    /// <summary>Un registre de langue et sa part parmi les passagers (poids relatif ; 0 : aucun passager ne le parle).</summary>
    [Serializable]
    public class RegisterData
    {
        public string id = "";
        public string name = "";
        public float weight = 1f;
    }

    /// <summary>Un personnage type (« la commerçante du marché », « l'étudiant »…) : contenu de Kevin.</summary>
    [Serializable]
    public class CharacterData
    {
        public string id = "";
        public string name = "";
        /// <summary>Registre que parle toujours ce personnage (<see cref="TalkRegisters"/>) ; vide : tiré comme pour les autres passagers.</summary>
        public string register = "";
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
        /// <summary>Langue ou registre de la réplique (<see cref="TalkRegisters"/>) ; vide : convient à tous les passagers.</summary>
        public string register = "";
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

        /// <summary>
        /// Registres parlés par les passagers et leur part (décision de Kevin : français, camfranglais, pidgin). Chaque
        /// passager reçoit un registre (<see cref="DialogueDirector.RegisterFor"/>) et ne dit que des répliques de ce
        /// registre ou sans registre. Parts par défaut (Yaoundé, ville francophone) : choix de Claude, à régler par Kevin.
        /// </summary>
        public RegisterData[] registers =
        {
            new RegisterData { id = TalkRegisters.French, name = "français", weight = 0.5f },
            new RegisterData { id = TalkRegisters.Camfranglais, name = "camfranglais", weight = 0.35f },
            new RegisterData { id = TalkRegisters.Pidgin, name = "pidgin", weight = 0.15f },
        };
        /// <summary>
        /// Registre de secours : si aucune réplique n'existe dans le registre du passager pour cette situation, il dit une
        /// réplique de ce registre (vide : il se tait).
        /// </summary>
        public string fallbackRegister = TalkRegisters.French;

        public CharacterData[] characters = Array.Empty<CharacterData>();
        public LineData[] lines = Array.Empty<LineData>();
    }
}
