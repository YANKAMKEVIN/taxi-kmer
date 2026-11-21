using System;

namespace TaxiKmer.Core.Save
{
    /// <summary>
    /// Contenu d'une sauvegarde (jalon 3, partie technique). Champs publics simples pour JsonUtility (Unity) et
    /// System.Text.Json (tests) : pas de propriétés, pas de null pour les objets (JsonUtility n'en a pas).
    /// N'y entre que ce qui est déjà décidé : la journée en cours et l'historique des bilans. Ce qui dépend des
    /// réponses de Kevin (dette envers le propriétaire, réputation, épargne) viendra en champs ajoutés, avec une
    /// nouvelle version du format.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Version du contenu (voir <see cref="SaveMigrations.CurrentVersion"/>).</summary>
        public int version = SaveMigrations.CurrentVersion;
        /// <summary>Date de la sauvegarde (UTC, ISO 8601), pour l'affichage.</summary>
        public string savedAtUtc = "";
        /// <summary>Version du jeu qui a écrit le fichier (Application.version), pour le diagnostic.</summary>
        public string gameVersion = "";
        public CareerData career = new CareerData();
        /// <summary>Une journée est en cours (sauvegarde en arrière-plan, ou en cours de route).</summary>
        public bool hasDayInProgress;
        public DayProgressData day = new DayProgressData();
    }

    /// <summary>La carrière : numéro du jour et bilans des journées terminées.</summary>
    [Serializable]
    public class CareerData
    {
        /// <summary>Numéro du jour en cours ou du prochain (1 au départ).</summary>
        public int dayNumber = 1;
        /// <summary>Argent du chauffeur, reporté d'un jour à l'autre (version 2).</summary>
        public int savings;
        /// <summary>Jours où le versement a manqué (version 2).</summary>
        public int[] missedVersementDays = Array.Empty<int>();
        /// <summary>Renvoyé par le propriétaire : la carrière est finie (version 2).</summary>
        public bool fired;
        /// <summary>Carburant dans le réservoir à la fin de la dernière journée (L, version 3) ; -1 = inconnu (réservoir de départ).</summary>
        public float fuelLitres = -1f;
        /// <summary>Réputation à la fin de la dernière journée (version 4) ; -1 = inconnue (réputation de départ).</summary>
        public int reputation = -1;
        /// <summary>Fois où le taxi est parti à la fourrière (version 5) ; le propriétaire renvoie à la 2ᵉ.</summary>
        public int impounds;
        /// <summary>Dégâts du taxi à la fin de la dernière journée (0 à 100, version 5, jalon 6) : ils passent d'un jour à l'autre.</summary>
        public float vehicleDamage;
        public DayRecordData[] history = Array.Empty<DayRecordData>();
    }

    /// <summary>Bilan d'une journée terminée (copie de <see cref="Day.DaySummary"/>).</summary>
    [Serializable]
    public class DayRecordData
    {
        public int dayNumber;
        public int revenue;
        public int rideCount;
        public int versement;
        public float kilometres;
        public float fuelLitres;
        public int fuelCost;
        public int expensesTotal;
        public int remaining;
        public int unpaidPassengers;
        public int unpaidFares;
        public bool arrested;
        public bool vehicleSeized;
        /// <summary>Version 2 : versement remis, ce qui manquait, épargne après la journée, humeur du propriétaire (valeur de OwnerMood).</summary>
        public int versementPaid;
        public int versementMissing;
        public int savingsAfter;
        public int ownerMood;
        /// <summary>Version 4 : réputation à la fin de la journée (-1 si pas suivie) et sa variation.</summary>
        public int reputation = -1;
        public int reputationChange;
    }

    /// <summary>État d'une journée en cours, pour la reprendre là où on l'a laissée.</summary>
    [Serializable]
    public class DayProgressData
    {
        /// <summary>Temps réel écoulé depuis le début de la journée (s) : donne l'heure du jeu.</summary>
        public float elapsedRealSeconds;
        public int revenue;
        public int rideCount;
        public float metersDriven;
        public ExpenseData[] expenses = Array.Empty<ExpenseData>();
        public PassengerData[] passengers = Array.Empty<PassengerData>();
        /// <summary>Passagers descendus sans payer plus tôt dans la journée (accident) et ce qu'ils devaient.</summary>
        public int leftUnpaid;
        public int leftUnpaidFares;
        /// <summary>Position du taxi le long de l'itinéraire (m) et sens (+1 vers Étoudi, -1 vers le dispensaire).</summary>
        public float taxiAlong;
        public int taxiDirection = 1;
        /// <summary>Graine des clients de la journée.</summary>
        public int seed;
        /// <summary>Version 3 : réservoir (L, -1 = inconnu), carburant acheté dans la journée (L et FCFA).</summary>
        public float tankLitres = -1f;
        public float fuelBoughtLitres;
        public int fuelBoughtCost;
        /// <summary>Version 4 : réputation au début de la journée et maintenant (-1 = inconnue).</summary>
        public int reputationDayStart = -1;
        public int reputation = -1;
    }

    [Serializable]
    public class ExpenseData
    {
        public string label = "";
        public int amount;
    }

    [Serializable]
    public class PassengerData
    {
        public string id = "";
        public string destinationStopId = "";
        public int distanceMeters;
        public int proposedFare;
        /// <summary>Valeur de <see cref="Rides.Corpulence"/>.</summary>
        public int corpulence;
        /// <summary>Valeur de <see cref="Rides.BenchSide"/>.</summary>
        public int bench;
    }
}
