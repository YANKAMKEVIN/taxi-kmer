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
    /// et les montants sont des valeurs par défaut. Décisions de Kevin du 21/11 : incidents rares et marquants (≈ 1 panne
    /// par mois de jeu, 1 crevaison par semaine), le chauffeur paie les réparations, pluie selon les saisons de Yaoundé,
    /// fuite du joueur possible (option B), délit de fuite subi : on peut poursuivre le fautif. Activé le 21/11.
    /// </summary>
    [Serializable]
    public class RoadConfig
    {
        public bool enabled = true;

        // État du véhicule (0 = neuf, 100 = épave)
        /// <summary>Dégâts par km/h au choc lors d'un accrochage.</summary>
        public float damagePerImpactKmh = 0.8f;
        /// <summary>Au-delà de ces dégâts, le taxi est limité à <see cref="damagedMaxKmh"/>.</summary>
        public float damagedThreshold = 60f;
        public float damagedMaxKmh = 40f;
        /// <summary>
        /// Dégâts visibles (idée n° 17, valeurs par défaut de Claude) : pare-chocs avant qui pend à partir de
        /// bumperHangDamage, phare gauche cassé à partir de brokenHeadlightDamage, fumée sous le capot à partir de
        /// smokeDamage (plus épaisse jusqu'à 100).
        /// </summary>
        public float bumperHangDamage = 30f;
        public float brokenHeadlightDamage = 45f;
        public float smokeDamage = 60f;

        // Nids-de-poule
        /// <summary>Nids-de-poule par km de route (placés au hasard, même graine pour toute la carrière).</summary>
        public float potholesPerKm = 3f;
        /// <summary>Sans danger en dessous de … km/h.</summary>
        public float potholeSafeKmh = 20f;
        /// <summary>Dégâts par km/h au-dessus de la vitesse sans danger.</summary>
        public float potholeDamagePerKmh = 0.005f;
        /// <summary>
        /// Chance de crever par nid-de-poule pris au-dessus de la vitesse sans danger. Recalculée pour ≈ 1 crevaison par
        /// semaine de jeu (Kevin, 21/11) avec la journée de 35 min : ≈ 23 km et ≈ 35 nids-de-poule pris par jour à 40 km/h.
        /// </summary>
        public float potholePunctureChance = 0.0044f;
        /// <summary>Un nid-de-poule couvre … m de route (largeur de passage).</summary>
        public float potholeLengthMeters = 2f;
        /// <summary>Part de la chaussée occupée : chance de passer dessus en roulant sur sa voie.</summary>
        public float potholeHitChance = 0.5f;

        // Crevaison, panne
        /// <summary>Pneu crevé : le taxi ne dépasse plus … km/h jusqu'à la roue de secours ou au garage.</summary>
        public float flatMaxKmh = 15f;
        /// <summary>Changer la roue soi-même : minutes de jeu.</summary>
        public int spareWheelMinutes = 20;
        /// <summary>
        /// Chance de panne par km roulé, plus … par point de dégâts. Recalculée pour ≈ 1 panne par mois de jeu (Kevin,
        /// 21/11) avec la journée de 35 min (≈ 23 km par jour) : 1 - (1 - 1/30)^(1/23) ≈ 0,0014 par km.
        /// </summary>
        public float breakdownPerKm = 0.0013f;
        public float breakdownPerKmPerDamage = 0.00001f;

        // Pluie
        /// <summary>Chance qu'il pleuve dans la journée quand le mois n'est pas connu.</summary>
        public float rainChancePerDay = 0.3f;
        /// <summary>
        /// Chance qu'il pleuve dans la journée selon le mois (janvier à décembre) : saisons des pluies de Yaoundé, mars à
        /// juin et septembre à novembre (tranché par Kevin le 21/11) ; petite saison sèche (juillet-août) et grande saison
        /// sèche (décembre-février) plus rares. Valeurs par défaut de Claude.
        /// </summary>
        public float[] rainChanceByMonth = { 0.05f, 0.1f, 0.4f, 0.5f, 0.5f, 0.4f, 0.15f, 0.2f, 0.5f, 0.6f, 0.4f, 0.05f };
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

        /// <summary>
        /// Fuite du joueur après un accrochage où il est fautif (option B, tranché par Kevin le 21/11) : il ne paie rien sur
        /// le moment, perd de la réputation, et la police le retrouve avec cette chance : il paie alors les dégâts de l'autre
        /// multipliés par … et perd … minutes de jeu (convocation).
        /// </summary>
        public bool playerCanFlee = true;
        public int playerFleeReputation = -10;
        public float playerFleeFoundChance = 0.5f;
        public float playerFleeFoundFactor = 1.5f;
        public int playerFleeFoundMinutes = 90;

        /// <summary>
        /// Délit de fuite subi (tranché par Kevin le 21/11 : on peut poursuivre le fautif) : la poursuite prend … minutes
        /// de jeu ; avec cette chance on le rattrape et il paie les réparations.
        /// </summary>
        public int chaseMinutes = 20;
        public float chaseCatchChance = 0.4f;

        /// <summary>
        /// Détection des accrochages avec le trafic dans le jeu (valeurs par défaut de Claude, à régler) : sous … km/h de
        /// rapprochement, simple frôlement ; après un accrochage, … secondes réelles sans en compter un autre ; le joueur a
        /// … secondes réelles pour choisir ; sans réponse, il est « parti » s'il roule encore à … km/h ou plus.
        /// </summary>
        public float collisionMinKmh = 5f;
        public float collisionCooldownSeconds = 5f;
        public float collisionChoiceSeconds = 10f;
        public float collisionDriveAwayKmh = 15f;

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
        /// Qui paie les réparations tant que le taxi est au propriétaire : **le chauffeur** (tranché par Kevin le 21/11) ;
        /// part payée par le chauffeur (1 = tout).
        /// </summary>
        public float driverShareOfRepairs = 1f;
        /// <summary>Mécanicien de quartier appelé sur place (panne) : prix du déplacement (FCFA), en plus de la réparation.</summary>
        public int mechanicCallFee = 2000;
    }
}
