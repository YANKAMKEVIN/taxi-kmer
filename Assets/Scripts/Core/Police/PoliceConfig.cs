using System;

namespace TaxiKmer.Core.Police
{
    /// <summary>Un papier du taxi ou du chauffeur (permis, carte grise, assurance…).</summary>
    [Serializable]
    public class PaperData
    {
        public string id = "";
        public string name = "";
        /// <summary>Durée de validité en jours de jeu (0 : n'expire jamais).</summary>
        public int validityDays;
        /// <summary>Jours de validité qui restent au début d'une carrière (0 : toute la durée).</summary>
        public int startDaysLeft;
        /// <summary>
        /// Coût du renouvellement (FCFA), payé sur l'épargne au renouvellement automatique (<see cref="PaperRenewal"/>).
        /// Montants de police.json : assurance 500, visite technique 1 000, licence 2 000 (Kevin, 22/11 ; equilibrage.md).
        /// </summary>
        public int renewalCost;
    }

    /// <summary>Un poste de contrôle fixe le long de l'itinéraire.</summary>
    [Serializable]
    public class CheckpointData
    {
        public string id = "";
        public string name = "";
        public float distanceAlongRouteMeters;
    }

    /// <summary>Un prétexte inventé par le policier quand tout est en règle (« même en règle, on peut être sommé de payer »).</summary>
    [Serializable]
    public class PretextData
    {
        public string id = "";
        /// <summary>Texte affiché ; ceux des données sont des exemples à remplacer par Kevin.</summary>
        public string label = "";
    }

    /// <summary>
    /// Une issue possible d'un choix (contester, appeler quelqu'un), tirée selon son poids. Les poids, montants et durées
    /// sont des décisions de Kevin (questions du jalon 4) : une issue de poids 0 n'arrive jamais.
    /// </summary>
    [Serializable]
    public class ChoiceOutcomeData
    {
        public string id = "";
        public float weight = 1f;
        /// <summary>Ce que le chauffeur paie, en part de la somme demandée (0 : rien, 1 : la somme, 1,5 : plus).</summary>
        public float payFactor = 1f;
        /// <summary>Minutes de jeu perdues en plus du contrôle.</summary>
        public int lostMinutes;
        /// <summary>Le taxi part à la fourrière : la journée s'arrête.</summary>
        public bool impound;
        /// <summary>Effet sur la réputation. 0 pour payer, refuser ou contester (tranché par Kevin le 21/11).</summary>
        public int reputation;
        /// <summary>
        /// Contester, option B (tranché par Kevin le 21/11) : issue possible seulement si le problème est inventé et que
        /// les papiers sont en règle (« gagne »).
        /// </summary>
        public bool requiresInvented;
        /// <summary>Appeler quelqu'un, option B (tranché par Kevin le 21/11) : issue possible seulement avec un contact.</summary>
        public bool requiresContact;
        /// <summary>Issue possible seulement sans contact (« personne ne répond »).</summary>
        public bool onlyWithoutContact;
        /// <summary>Le chauffeur est arrêté (garde à vue) : la journée s'arrête.</summary>
        public bool arrest;
        /// <summary>Fuir : le chauffeur s'échappe, mais il est recherché jusqu'à la fin de la journée (prochain contrôle).</summary>
        public bool wanted;
    }

    /// <summary>
    /// Contrôles de police (jalon 4, feuille de route ; données : Assets/Resources/Data/police.json). Ce qui est écrit
    /// dans la feuille de route est codé (postes fixes et contrôles volants, papiers à dates d'expiration, problème
    /// trouvé ou inventé, somme demandée, payer / négocier / contester / appeler, temps, argent, fourrière, réputation,
    /// surcharge comme prétexte). Les montants, les chances et l'issue des choix sont des valeurs provisoires ou des
    /// décisions de Kevin : <see cref="enabled"/> reste à false tant qu'il ne les a pas fixés.
    /// </summary>
    /// <summary>
    /// Contacts du chauffeur qui peuvent « arranger » un contrôle (appeler quelqu'un, option B, tranché par Kevin le 21/11).
    /// Crochet du jalon 7 : un événement de vie ajoutera un contact (<see cref="Add"/>) ; tant que le jalon 7 n'existe pas,
    /// <see cref="PoliceConfig.contactsEnabled"/> est à false et personne ne répond.
    /// </summary>
    public sealed class PoliceContacts
    {
        private readonly System.Collections.Generic.List<string> _ids = new System.Collections.Generic.List<string>();
        private readonly bool _enabled;

        public PoliceContacts(PoliceConfig config) => _enabled = config != null && config.contactsEnabled;

        public System.Collections.Generic.IReadOnlyList<string> Ids => _ids;
        /// <summary>Un contact peut être appelé (crochet actif et au moins un contact).</summary>
        public bool HasContact => _enabled && _ids.Count > 0;

        /// <summary>Un événement de vie (jalon 7) donne un contact (« un cousin à la brigade »…).</summary>
        public void Add(string id)
        {
            if (!string.IsNullOrEmpty(id) && !_ids.Contains(id)) _ids.Add(id);
        }
    }

    [Serializable]
    public class PoliceConfig
    {
        /// <summary>Contrôles actifs dans le jeu (activés le 21/11 après les décisions de Kevin ; le reste en valeurs par défaut).</summary>
        public bool enabled = true;

        /// <summary>Postes fixes (positions provisoires, à placer par Kevin).</summary>
        public CheckpointData[] checkpoints =
        {
            new CheckpointData { id = "poste-messassi", name = "Poste de contrôle de Messassi", distanceAlongRouteMeters = 900f },
            new CheckpointData { id = "poste-etoudi", name = "Poste de contrôle d'Étoudi", distanceAlongRouteMeters = 5200f },
        };
        /// <summary>Chance d'être arrêté en passant devant un poste fixe.</summary>
        public float checkpointStopChance = 0.5f;
        /// <summary>Un même poste n'arrête pas le taxi deux fois en moins de … minutes de jeu.</summary>
        public int checkpointCooldownMinutes = 120;
        /// <summary>Contrôles volants par journée (placés au hasard le long de l'itinéraire et dans la journée).</summary>
        public int flyingChecksPerDay = 2;
        /// <summary>Un contrôle volant reste en place … minutes de jeu.</summary>
        public int flyingDurationMinutes = 60;
        /// <summary>Chance d'être arrêté en passant devant un contrôle volant.</summary>
        public float flyingStopChance = 0.7f;

        /// <summary>Durée d'un contrôle (minutes de jeu), même quand tout se passe bien.</summary>
        public int checkMinutes = 10;
        /// <summary>Passagers permis (au-delà : surcharge, prétexte de la feuille de route).</summary>
        public int legalMaxPassengers = 4;
        /// <summary>Passagers permis devant (« deux devant » : prétexte de la feuille de route).</summary>
        public int legalMaxFront = 1;
        /// <summary>Tout est en règle : chance que le policier invente un problème (vision : même en règle, on peut payer).</summary>
        public float inventChanceWhenInOrder = 0.5f;

        /// <summary>Somme demandée (FCFA) : papier expiré, surcharge, problème inventé (tiré entre min et max).</summary>
        public int demandExpiredPaper = 5000;
        public int demandOverload = 3000;
        public int demandInventedMin = 1000;
        public int demandInventedMax = 3000;
        /// <summary>Les sommes sont arrondies à … FCFA.</summary>
        public int roundTo = 500;

        /// <summary>Négocier : la somme baisse de cette part…</summary>
        public float negotiateDiscount = 0.3f;
        /// <summary>… sauf si le policier se braque (chance), et alors elle monte de cette part.</summary>
        public float negotiateFailChance = 0.3f;
        public float negotiateHarden = 0.2f;
        public int negotiateMinutes = 5;
        /// <summary>Nombre de tentatives de négociation par contrôle.</summary>
        public int maxNegotiations = 2;
        /// <summary>La somme ne descend pas sous ce montant en négociant.</summary>
        public int negotiateFloor = 500;

        /// <summary>
        /// Contester, option B (tranché par Kevin le 21/11) : on gagne parfois, environ 1 fois sur 3, seulement si le
        /// problème est inventé et les papiers en règle (« gagne », <see cref="ChoiceOutcomeData.requiresInvented"/>) ;
        /// sinon on perd (paie plus) ou c'est la fourrière. Poids : problème inventé → gagne 1 sur 3 (1 / (1 + 1,5 + 0,5)) ;
        /// problème réel → perd 3 fois sur 4, fourrière 1 fois sur 4 (valeurs par défaut).
        /// </summary>
        public ChoiceOutcomeData[] contestOutcomes =
        {
            new ChoiceOutcomeData { id = "perd", weight = 1.5f, payFactor = 1.5f, lostMinutes = 60 },
            new ChoiceOutcomeData { id = "fourriere", weight = 0.5f, payFactor = 0f, lostMinutes = 0, impound = true },
            new ChoiceOutcomeData { id = "gagne", weight = 1f, payFactor = 0f, lostMinutes = 30, requiresInvented = true },
        };
        /// <summary>
        /// Appeler quelqu'un, option B (tranché par Kevin le 21/11) : ça marche si on a un contact (« arrange ») ; sans
        /// contact, personne ne répond. Les contacts viennent des événements de vie du jalon 7 (<see cref="PoliceContacts"/>,
        /// coupé tant que le jalon 7 n'existe pas : <see cref="contactsEnabled"/>).
        /// </summary>
        public ChoiceOutcomeData[] callOutcomes =
        {
            new ChoiceOutcomeData { id = "personne", weight = 1f, payFactor = 1f, lostMinutes = 20, onlyWithoutContact = true },
            new ChoiceOutcomeData { id = "arrange", weight = 1f, payFactor = 0f, lostMinutes = 20, requiresContact = true },
        };
        /// <summary>Crochet du jalon 7 : les contacts comptent pour « appeler quelqu'un » (false tant que le jalon 7 n'existe pas).</summary>
        public bool contactsEnabled;

        /// <summary>
        /// Fuir un contrôle, option B (tranché par Kevin le 21/11) : possible, avec une poursuite. Rattrapé → fourrière ;
        /// échappé → recherché jusqu'à la fin de la journée.
        /// </summary>
        public bool fleeEnabled = true;
        public ChoiceOutcomeData[] fleeOutcomes =
        {
            new ChoiceOutcomeData { id = "echappe", weight = 0.6f, payFactor = 0f, lostMinutes = 5, wanted = true },
            new ChoiceOutcomeData { id = "rattrape", weight = 0.4f, payFactor = 0f, lostMinutes = 15, impound = true },
        };
        /// <summary>Poursuite visible quand on fuit (idée n° 9) ; enabled = false : tirage de fleeOutcomes comme avant.</summary>
        public ChaseData chase = new ChaseData();
        /// <summary>Recherché : le prochain contrôle en place l'arrête à coup sûr…</summary>
        public float wantedStopChance = 1f;
        /// <summary>… et le policier parle de cette somme si ça ne finit pas en fourrière ou en arrestation (FCFA).</summary>
        public int demandFled = 10000;
        /// <summary>Au prochain poste, recherché : fourrière, arrestation, ou contrôle ordinaire (« oublie »).</summary>
        public ChoiceOutcomeData[] wantedOutcomes =
        {
            new ChoiceOutcomeData { id = "fourriere", weight = 0.4f, payFactor = 0f, lostMinutes = 0, impound = true },
            new ChoiceOutcomeData { id = "arrestation", weight = 0.2f, payFactor = 0f, lostMinutes = 0, arrest = true },
            new ChoiceOutcomeData { id = "oublie", weight = 0.4f, payFactor = 0f, lostMinutes = 0 },
        };

        /// <summary>Frais de fourrière (FCFA), payés sur la journée. Le propriétaire renvoie à la 2ᵉ fourrière (owner.json).</summary>
        public int impoundFee = 15000;
        /// <summary>Réputation : payer le policier. 0 : payer, refuser ou contester ne la changent pas (tranché par Kevin le 21/11).</summary>
        public int reputationPaid;

        /// <summary>
        /// Les papiers se renouvellent tout seuls à l'échéance, payés sur l'épargne (tranché par Kevin le 21/11) ; épargne
        /// insuffisante : le papier reste expiré, un message l'annonce, nouvel essai chaque matin (validé par Kevin).
        /// </summary>
        public bool autoRenewPapers = true;

        public PaperData[] papers =
        {
            new PaperData { id = "permis", name = "Permis de conduire", validityDays = 0 },
            new PaperData { id = "carte-grise", name = "Carte grise", validityDays = 0 },
            // Option A (tranché par Kevin le 21/11) : durées proches du réel, en jours de jeu. Montants bas (Kevin, 22/11 :
            // « rester vers 60 à 70 % » de journées où le versement est atteint), choisis par l'étude (equilibrage.md).
            new PaperData { id = "assurance", name = "Assurance", validityDays = 30, startDaysLeft = 12, renewalCost = 500 },
            new PaperData { id = "visite-technique", name = "Visite technique", validityDays = 180, startDaysLeft = 25, renewalCost = 1000 },
            new PaperData { id = "licence-taxi", name = "Licence de taxi", validityDays = 365, startDaysLeft = 40, renewalCost = 2000 },
        };

        /// <summary>Exemples à remplacer par Kevin (ton du jalon 4 : satirique, dramatique ou les deux).</summary>
        public PretextData[] pretexts =
        {
            new PretextData { id = "extincteur", label = "Extincteur introuvable (exemple à remplacer)" },
            new PretextData { id = "triangle", label = "Pas de triangle de signalisation (exemple à remplacer)" },
        };
    }
}
