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
        /// <summary>Coût du renouvellement (FCFA). Question 2 du jalon 4 : à fixer par Kevin.</summary>
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
        /// <summary>Effet sur la réputation (0 par défaut : à décider par Kevin).</summary>
        public int reputation;
    }

    /// <summary>
    /// Contrôles de police (jalon 4, feuille de route ; données : Assets/Resources/Data/police.json). Ce qui est écrit
    /// dans la feuille de route est codé (postes fixes et contrôles volants, papiers à dates d'expiration, problème
    /// trouvé ou inventé, somme demandée, payer / négocier / contester / appeler, temps, argent, fourrière, réputation,
    /// surcharge comme prétexte). Les montants, les chances et l'issue des choix sont des valeurs provisoires ou des
    /// décisions de Kevin : <see cref="enabled"/> reste à false tant qu'il ne les a pas fixés.
    /// </summary>
    [Serializable]
    public class PoliceConfig
    {
        /// <summary>Contrôles actifs dans le jeu (false : rien ne se passe ; à activer par Kevin).</summary>
        public bool enabled;

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

        /// <summary>Contester (question 3 du jalon 4 : peut-on gagner ?). « gagne » a un poids 0 tant que Kevin n'a pas décidé.</summary>
        public ChoiceOutcomeData[] contestOutcomes =
        {
            new ChoiceOutcomeData { id = "perd", weight = 1f, payFactor = 1.5f, lostMinutes = 60 },
            new ChoiceOutcomeData { id = "fourriere", weight = 0f, payFactor = 0f, lostMinutes = 0, impound = true },
            new ChoiceOutcomeData { id = "gagne", weight = 0f, payFactor = 0f, lostMinutes = 30 },
        };
        /// <summary>Appeler quelqu'un (qui, et ce que ça change : décision de Kevin). L'issue « arrange » a un poids 0.</summary>
        public ChoiceOutcomeData[] callOutcomes =
        {
            new ChoiceOutcomeData { id = "personne", weight = 1f, payFactor = 1f, lostMinutes = 20 },
            new ChoiceOutcomeData { id = "arrange", weight = 0f, payFactor = 0f, lostMinutes = 20 },
        };
        /// <summary>Frais de fourrière (FCFA), payés sur la journée.</summary>
        public int impoundFee = 15000;
        /// <summary>Réputation : payer le policier, refuser (contester ou appeler). 0 tant que Kevin n'a pas décidé.</summary>
        public int reputationPaid;

        public PaperData[] papers =
        {
            new PaperData { id = "permis", name = "Permis de conduire", validityDays = 0 },
            new PaperData { id = "carte-grise", name = "Carte grise", validityDays = 0 },
            new PaperData { id = "assurance", name = "Assurance", validityDays = 30, startDaysLeft = 12 },
            new PaperData { id = "visite-technique", name = "Visite technique", validityDays = 60, startDaysLeft = 25 },
            new PaperData { id = "licence-taxi", name = "Licence de taxi", validityDays = 90, startDaysLeft = 40 },
        };

        /// <summary>Exemples à remplacer par Kevin (ton du jalon 4 : satirique, dramatique ou les deux).</summary>
        public PretextData[] pretexts =
        {
            new PretextData { id = "extincteur", label = "Extincteur introuvable (exemple à remplacer)" },
            new PretextData { id = "triangle", label = "Pas de triangle de signalisation (exemple à remplacer)" },
        };
    }
}
