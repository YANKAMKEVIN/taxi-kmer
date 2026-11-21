using System;

namespace TaxiKmer.Core.Road
{
    /// <summary>Ce que fait l'autre conducteur après un accrochage dont il est fautif (tiré selon les poids).</summary>
    [Serializable]
    public class OtherDriverData
    {
        /// <summary>« paie » : s'arrête et paie ; « negocie » : paie une partie ; « fuit » : délit de fuite.</summary>
        public string id = "";
        public float weight = 1f;
        /// <summary>Part des dégâts qu'il rembourse.</summary>
        public float payFactor = 1f;
        public int lostMinutes = 15;
    }

    /// <summary>
    /// Imprévus de la route (jalon 6, feuille de route ; données : Assets/Resources/Data/road.json) : état du véhicule,
    /// accrochages (le fautif s'arrête, négocie ou fuit), pannes, crevaisons, nids-de-poule, pluie, garage. Les fréquences
    /// et les montants sont provisoires (question 1 du jalon 6 : fréquence voulue ; question 3 : qui paie) ;
    /// <see cref="enabled"/> reste à false tant que Kevin ne les a pas fixés.
    /// </summary>
    [Serializable]
    public class RoadConfig
    {
        public bool enabled;

        // État du véhicule (0 = neuf, 100 = épave)
        /// <summary>Dégâts par km/h au choc lors d'un accrochage.</summary>
        public float damagePerImpactKmh = 0.8f;
        /// <summary>Au-delà de ces dégâts, le taxi est limité à <see cref="damagedMaxKmh"/>.</summary>
        public float damagedThreshold = 60f;
        public float damagedMaxKmh = 40f;

        // Nids-de-poule
        /// <summary>Nids-de-poule par km de route (placés au hasard, même graine pour toute la carrière).</summary>
        public float potholesPerKm = 3f;
        /// <summary>Sans danger en dessous de … km/h.</summary>
        public float potholeSafeKmh = 20f;
        /// <summary>Dégâts par km/h au-dessus de la vitesse sans danger.</summary>
        public float potholeDamagePerKmh = 0.05f;
        /// <summary>Chance de crever par nid-de-poule pris au-dessus de la vitesse sans danger.</summary>
        public float potholePunctureChance = 0.03f;
        /// <summary>Un nid-de-poule couvre … m de route (largeur de passage).</summary>
        public float potholeLengthMeters = 2f;
        /// <summary>Part de la chaussée occupée : chance de passer dessus en roulant sur sa voie.</summary>
        public float potholeHitChance = 0.5f;

        // Crevaison, panne
        /// <summary>Pneu crevé : le taxi ne dépasse plus … km/h jusqu'à la roue de secours ou au garage.</summary>
        public float flatMaxKmh = 15f;
        /// <summary>Changer la roue soi-même : minutes de jeu.</summary>
        public int spareWheelMinutes = 20;
        /// <summary>Chance de panne par km roulé, plus … par point de dégâts.</summary>
        public float breakdownPerKm = 0.03f;
        public float breakdownPerKmPerDamage = 0.0003f;

        // Pluie
        /// <summary>Chance qu'il pleuve dans la journée.</summary>
        public float rainChancePerDay = 0.3f;
        /// <summary>Durée d'une averse (minutes de jeu), entre … et ….</summary>
        public int rainMinMinutes = 60;
        public int rainMaxMinutes = 180;
        /// <summary>Sous la pluie : adhérence (1 = sec), densité du trafic, arrivée des clients, patience des clients.</summary>
        public float rainGrip = 0.7f;
        public float rainTraffic = 1.3f;
        public float rainClients = 1.3f;
        public float rainPatience = 0.7f;

        // Accrochages
        public OtherDriverData[] otherDriverAtFault =
        {
            new OtherDriverData { id = "paie", weight = 0.5f, payFactor = 1f, lostMinutes = 20 },
            new OtherDriverData { id = "negocie", weight = 0.3f, payFactor = 0.5f, lostMinutes = 30 },
            new OtherDriverData { id = "fuit", weight = 0.2f, payFactor = 0f, lostMinutes = 5 },
        };
        /// <summary>Chauffeur fautif : il paie les dégâts de l'autre, … FCFA par km/h au choc.</summary>
        public int otherDamagePerKmh = 1000;
        public int atFaultLostMinutes = 30;

        // Garage / mécanicien de quartier
        /// <summary>Réparation : … FCFA par point de dégâts.</summary>
        public int repairCostPerDamage = 500;
        public int repairMinutesPerDamage = 2;
        public int punctureRepairCost = 1500;
        public int punctureRepairMinutes = 20;
        /// <summary>Panne : dépannage et réparation (FCFA, minutes de jeu).</summary>
        public int breakdownRepairCost = 10000;
        public int breakdownRepairMinutes = 120;
        /// <summary>
        /// Qui paie les réparations tant que le taxi est au propriétaire (question 3 du jalon 6) : part payée par le
        /// chauffeur (1 = tout, 0 = le propriétaire paie). Provisoire.
        /// </summary>
        public float driverShareOfRepairs = 1f;
    }
}
