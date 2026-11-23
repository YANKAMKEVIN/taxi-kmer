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
        /// <summary>Négociation du prix : le client propose moins (option A, tranché par Kevin le 21/11).</summary>
        public const string Haggle = "negociation";
        /// <summary>Client qui ne veut pas payer (option C, tranché par Kevin le 21/11 : on peut appeler la police).</summary>
        public const string RefusesToPay = "refus-paiement";
        /// <summary>Un passager précise ou rappelle sa destination.</summary>
        public const string Destination = "destination";
        /// <summary>Un passager parle du prix annoncé.</summary>
        public const string Price = "prix";
        /// <summary>Panne sèche, crevaison ou panne (jalons 3 et 6).</summary>
        public const string Breakdown = "panne";
        /// <summary>Contrôle de police (jalon 4), pendant ou juste après.</summary>
        public const string Control = "controle";
        /// <summary>Il pleut (jalon 6).</summary>
        public const string Rain = "pluie";
        /// <summary>Long silence dans le taxi : un passager le rompt (ou le savoure).</summary>
        public const string Silence = "silence";

        public static readonly string[] All = { Boarding, Chat, SeatMove, Detour, Paid, Haggle, RefusesToPay, Destination, Price, Breakdown, Control, Rain, Silence };
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
        /// <summary>Petit son de voix par défaut du personnage (id de <see cref="DialogueConfig.voiceSounds"/>) ; vide : aucun.</summary>
        public string voice = "";
        /// <summary>« proposé » : personnage écrit par Claude, à valider par Kevin.</summary>
        public string statut = "";
    }

    /// <summary>
    /// Un petit son de voix (« hmm », « eh ! ») joué avec une bulle (texte + petits sons, option B, tranché par Kevin le
    /// 21/11). Aucun son fourni : <see cref="clip"/> vide = emplacement « à fournir ».
    /// </summary>
    [Serializable]
    public class VoiceSoundData
    {
        public string id = "";
        /// <summary>Ce qu'on entend (« hmm », « eh ! »), pour celui qui enregistrera le son.</summary>
        public string label = "";
        /// <summary>Chemin du son sous Resources (sans extension) ; vide : à fournir, une voix synthétique provisoire joue (SoundSynth.Voice).</summary>
        public string clip = "";
        public string statut = "à fournir";
    }

    /// <summary>Une réponse rapide du chauffeur (un bouton qui ne bloque pas la conduite).</summary>
    [Serializable]
    public class ReplyData
    {
        public string id = "";
        public string text = "";
        /// <summary>Effet sur la réputation (décision de Kevin du 21/11 : les réponses comptent).</summary>
        public int reputation;
        /// <summary>Pourboire promis (FCFA), payé avec la course (décision de Kevin du 21/11).</summary>
        public int tip;
    }

    /// <summary>Histoire d'un passager régulier : 3 à 5 rencontres, au moins minDaysBetween jours d'écart.</summary>
    [Serializable]
    public class StoryArcData
    {
        public string id = "";
        public string character = "";
        public string name = "";
        public int steps = 3;
        public int minDaysBetween = 1;
        public string statut = "proposée";
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
        /// <summary>Petit son de voix (id de <see cref="DialogueConfig.voiceSounds"/>) ; vide : celui du personnage, ou aucun.</summary>
        public string voice = "";
        /// <summary>« proposée » : écrite par Claude, à valider par Kevin (« validée » ensuite).</summary>
        public string statut = "";
        /// <summary>« à traduire » : écrite en français faute de tournure sûre dans le registre visé.</summary>
        public string traduction = "";
        /// <summary>
        /// Plage horaire (décision de Kevin, 23/11) : la réplique n'est dite qu'à partir de <see cref="fromTime"/> et
        /// avant <see cref="untilTime"/> (« HH:MM » ; « Bonsoir » le soir, « Bonjour » le matin). Vide : à toute heure.
        /// Une plage qui passe minuit (« 22:00 » → « 05:00 ») est permise.
        /// </summary>
        public string fromTime = "";
        public string untilTime = "";
        /// <summary>
        /// Histoire d'un passager régulier (idée n° 13) : id de l'arc (<see cref="DialogueConfig.arcs"/>) et rang de la
        /// rencontre (1, 2, 3…). La réplique n'est dite qu'à son tour dans l'histoire (<see cref="StoryArcs"/>).
        /// </summary>
        public string arc = "";
        public int arcStep;

        /// <summary>La réplique peut être dite à cette heure (minutes depuis minuit ; négatif : heure inconnue, toujours vrai).</summary>
        public bool SaidAt(int minuteOfDay)
        {
            if (minuteOfDay < 0) return true;
            var from = ParseTime(fromTime);
            var until = ParseTime(untilTime);
            if (from < 0 && until < 0) return true;
            if (from < 0) from = 0;
            if (until < 0) until = 24 * 60;
            var m = minuteOfDay % (24 * 60);
            return from <= until ? m >= from && m < until : m >= from || m < until;
        }

        /// <summary>« 07:30 » → 450 ; « 24:00 » → 1440 ; vide ou illisible → -1.</summary>
        public static int ParseTime(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) return -1;
            var parts = hhmm.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var mn)) return -1;
            if (h < 0 || mn < 0 || mn > 59 || h * 60 + mn > 24 * 60) return -1;
            return h * 60 + mn;
        }
    }

    /// <summary>
    /// Dialogues des passagers (jalon 5 ; données : Assets/Resources/Data/dialogues.json). Répliques écrites par Claude,
    /// validées par Kevin (option B, tranché par Kevin le 21/11) : <see cref="LineData.statut"/> « proposée » jusqu'à sa validation.
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
        /// <summary>
        /// Négociation, option A (tranché par Kevin le 21/11) : chance qu'un client accepté propose moins (multipliée par
        /// l'effet de la réputation) ; il propose entre haggleDiscountMin et haggleDiscountMax de moins, arrondi à
        /// haggleRoundTo FCFA, pas moins que haggleFloor. Le joueur accepte ou refuse.
        /// </summary>
        public float haggleChance;
        public float haggleDiscountMin = 0.15f;
        public float haggleDiscountMax = 0.35f;
        public int haggleRoundTo = 50;
        public int haggleFloor = 100;
        /// <summary>
        /// Refus de payer, option C (tranché par Kevin le 21/11) : chance qu'un passager arrivé refuse de payer ; le joueur
        /// le laisse partir ou appelle la police (refusalPoliceMinutes perdues, il paie selon refusalPolicePayChance).
        /// </summary>
        public float refusesToPayChance;
        public int refusalPoliceMinutes = 30;
        public float refusalPolicePayChance = 0.6f;
        /// <summary>Après … s sans bulle avec au moins un passager, un passager rompt le silence (situation « silence » ; 0 : jamais).</summary>
        public float silenceAfterSeconds = 90f;
        /// <summary>Petits sons de voix (texte + petits sons, option B, tranché par Kevin le 21/11) ; sons à fournir.</summary>
        public VoiceSoundData[] voiceSounds = Array.Empty<VoiceSoundData>();

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

        /// <summary>Part des passagers qui sont un personnage type (les autres sont des passagers quelconques).</summary>
        public float characterShare = 0.6f;
        public CharacterData[] characters = Array.Empty<CharacterData>();
        public LineData[] lines = Array.Empty<LineData>();
        /// <summary>Histoires de passagers réguliers (idée n° 13), à valider par Kevin.</summary>
        public StoryArcData[] arcs = Array.Empty<StoryArcData>();

        /// <summary>Règles des disputes d'argent pour la rue (négociation, refus de payer).</summary>
        public Rides.FareDisputeRules FareRules() => new Rides.FareDisputeRules
        {
            HaggleChance = haggleChance,
            HaggleDiscountMin = haggleDiscountMin,
            HaggleDiscountMax = haggleDiscountMax,
            HaggleRoundTo = haggleRoundTo,
            HaggleFloor = haggleFloor,
            RefusesToPayChance = refusesToPayChance,
            PoliceMinutes = refusalPoliceMinutes,
            PolicePayChance = refusalPolicePayChance,
        };

        /// <summary>Son de voix d'une réplique : le sien, sinon celui du personnage ; null si aucun ou son à fournir.</summary>
        public VoiceSoundData VoiceFor(LineData line, string character)
        {
            if (line == null) return null;
            var id = line.voice;
            if (string.IsNullOrEmpty(id) && characters != null)
                foreach (var c in characters)
                    if (c.id == (string.IsNullOrEmpty(character) ? line.character : character)) { id = c.voice; break; }
            if (string.IsNullOrEmpty(id) || voiceSounds == null) return null;
            foreach (var v in voiceSounds) if (v.id == id) return v;
            return null;
        }
    }
}
