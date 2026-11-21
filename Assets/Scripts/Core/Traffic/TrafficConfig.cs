using System;

namespace TaxiKmer.Core.Traffic
{
    public enum VehicleKind
    {
        Car,
        /// <summary>Taxi concurrent : il prend aussi les clients au bord de la route.</summary>
        Taxi,
        /// <summary>Mototaxi (« bendskin ») : roule près du bord et se faufile.</summary>
        Moto,
    }

    /// <summary>Réglages d'un type de véhicule (modèle de suivi IDM : accélération, freinage, distance de sécurité).</summary>
    [Serializable]
    public class VehicleKindConfig
    {
        /// <summary>Part de ce type dans le trafic (les parts sont normalisées entre elles).</summary>
        public float share = 1f;
        public float desiredKmh = 50f;
        /// <summary>Écart de vitesse voulue d'un conducteur à l'autre (± km/h).</summary>
        public float desiredKmhSpread = 8f;
        public float length = 4.3f;
        public float width = 1.75f;
        /// <summary>Accélération maximale (m/s²).</summary>
        public float maxAccel = 1.6f;
        /// <summary>Freinage confortable (m/s²) ; le freinage d'urgence peut aller au-delà.</summary>
        public float comfortDecel = 2.5f;
        /// <summary>Temps de suivi voulu derrière le véhicule de devant (s).</summary>
        public float timeHeadway = 1.2f;
        /// <summary>Distance gardée à l'arrêt (m).</summary>
        public float minGap = 2f;
    }

    /// <summary>
    /// Réglages du trafic (données : Assets/Resources/Data/traffic.json). Distances en m, vitesses en km/h,
    /// durées en secondes réelles. Les densités sont par km et par sens, pour chaque heure de 0 h à 23 h.
    /// </summary>
    [Serializable]
    public class TrafficConfig
    {
        /// <summary>Véhicules par km et par sens, heure par heure (24 valeurs) : pointes le matin et le soir.</summary>
        public float[] vehiclesPerKmByHour = DefaultVehicleDensity();
        public VehicleKindConfig car = new VehicleKindConfig { share = 0.45f, desiredKmh = 50f, desiredKmhSpread = 8f, length = 4.3f, width = 1.75f };
        public VehicleKindConfig taxi = new VehicleKindConfig { share = 0.35f, desiredKmh = 45f, desiredKmhSpread = 6f, length = 4.4f, width = 1.72f, maxAccel = 1.8f };
        public VehicleKindConfig moto = new VehicleKindConfig { share = 0.2f, desiredKmh = 50f, desiredKmhSpread = 10f, length = 2f, width = 0.8f, maxAccel = 2.5f, comfortDecel = 3f, timeHeadway = 0.8f, minGap = 1f };

        /// <summary>Écart de la voie à droite de l'axe de la chaussée (m).</summary>
        public float laneOffsetMeters = 1.7f;
        /// <summary>Écart des mototaxis à droite de l'axe : au bord, à côté des voitures.</summary>
        public float motoOffsetMeters = 3.1f;
        /// <summary>Écart de la voie de gauche, utilisée pour doubler (m, à gauche de l'axe sur un double sens).</summary>
        public float overtakeOffsetMeters = -1.7f;
        /// <summary>Un mototaxi qui longe une voiture ne la dépasse pas de plus de … km/h.</summary>
        public float motoPassingDeltaKmh = 15f;

        /// <summary>Le trafic n'existe que dans ce rayon autour du joueur (le long de la route).</summary>
        public float activeRadiusMeters = 380f;
        /// <summary>Les nouveaux véhicules apparaissent au moins à cette distance du joueur (cachés par le brouillard).</summary>
        public float minSpawnDistanceMeters = 160f;
        /// <summary>Écart minimal avec le véhicule le plus proche pour en faire apparaître un.</summary>
        public float minSpawnGapMeters = 18f;
        /// <summary>Apparitions au plus par image (évite les vagues).</summary>
        public int maxSpawnsPerUpdate = 2;

        /// <summary>Bloqué derrière un véhicule arrêté depuis … s : le conducteur double.</summary>
        public float overtakeWaitSeconds = 3f;
        /// <summary>Sur un double sens, il faut … m libres en face pour doubler.</summary>
        public float overtakeClearMeters = 90f;
        /// <summary>Vitesse de changement de voie (m/s de côté).</summary>
        public float lateralSpeed = 2f;

        /// <summary>Chance qu'un taxi concurrent s'arrête pour un client qui attend dans son sens.</summary>
        public float competitorPickupChance = 0.5f;
        /// <summary>Durée de la montée d'un client dans un taxi concurrent (s).</summary>
        public float competitorStopSeconds = 6f;
        /// <summary>Places d'un taxi concurrent ; il apparaît avec 0 à places - 1 passagers.</summary>
        public int competitorCapacity = 5;
        /// <summary>
        /// Les mototaxis prennent aussi des clients (décision de Kevin, 2026-11-14) : tant que le client n'est pas monté
        /// dans le taxi du joueur, même s'il l'a accepté d'un coup de klaxon.
        /// </summary>
        public bool motoTakesClients = true;
        /// <summary>Passagers d'un mototaxi ; il apparaît avec 0 à places - 1 passagers.</summary>
        public int motoCapacity = 2;
        /// <summary>Durée de la montée d'un client sur un mototaxi (s).</summary>
        public float motoStopSeconds = 3f;

        /// <summary>Véhicules qui sortent d'une rue latérale pour entrer sur l'axe, par minute et par carrefour, à la densité de pointe.</summary>
        public float junctionEntriesPerMinuteAtPeak = 2f;
        /// <summary>Multiplicateur pour les chemins de service (cours, parkings).</summary>
        public float serviceRoadFactor = 0.3f;
        /// <summary>Chance qu'un véhicule de l'axe tourne dans une rue latérale à droite qu'il croise.</summary>
        public float exitChance = 0.12f;
        /// <summary>Vitesse pour tourner au carrefour (km/h).</summary>
        public float turnSpeedKmh = 15f;
        /// <summary>Vitesse dans les rues latérales (km/h).</summary>
        public float sideRoadSpeedKmh = 20f;
        /// <summary>Pour entrer sur l'axe, il faut … s d'avance sur le véhicule (ou le joueur) qui arrive.</summary>
        public float enterGapSeconds = 4f;
        /// <summary>Après … s d'attente à la ligne d'arrêt, il se contente d'un trou de 1,5 s (le suivant freine).</summary>
        public float forceInAfterSeconds = 20f;
        /// <summary>Ligne d'arrêt dans la rue latérale, à … m du carrefour.</summary>
        public float stopLineMeters = 6f;
        /// <summary>Véhicules en attente au plus par rue latérale.</summary>
        public int maxQueuePerJunction = 2;

        /// <summary>Feux tricolores : durée du vert pour l'axe (s).</summary>
        public float lightGreenSeconds = 25f;
        /// <summary>Durée de l'orange (s).</summary>
        public float lightAmberSeconds = 3f;
        /// <summary>Durée du rouge pour l'axe, pendant que les rues latérales passent (s).</summary>
        public float lightRedSeconds = 20f;
        /// <summary>Ligne d'arrêt à … m avant le feu.</summary>
        public float lightStopLineMeters = 6f;
        /// <summary>Une rue latérale à moins de … m d'un feu attend que l'axe soit au rouge pour entrer.</summary>
        public float lightJunctionRadiusMeters = 25f;
        /// <summary>Chance qu'un mototaxi grille un feu rouge (décidée à chaque feu).</summary>
        public float motoRunsRedChance = 0.3f;

        public static float[] DefaultVehicleDensity()
        {
            // 0 h … 23 h. La journée de jeu va de 6 h à 20 h ; pointes à 7-8 h et 17-18 h.
            return new[]
            {
                3f, 2f, 2f, 2f, 3f, 6f, 12f, 20f, 22f, 15f, 11f, 10f,
                11f, 11f, 10f, 11f, 15f, 21f, 22f, 16f, 10f, 7f, 5f, 4f,
            };
        }

        /// <summary>Densité à l'heure donnée (heures fractionnaires interpolées), en véhicules par km et par sens.</summary>
        public float DensityAt(float hour) => Interpolate(vehiclesPerKmByHour, hour);

        public VehicleKindConfig Kind(VehicleKind kind) => kind switch
        {
            VehicleKind.Taxi => taxi,
            VehicleKind.Moto => moto,
            _ => car,
        };

        internal static float Interpolate(float[] byHour, float hour)
        {
            if (byHour == null || byHour.Length == 0) return 0f;
            var n = byHour.Length;
            var h = ((hour % n) + n) % n;
            var i = (int)Math.Floor(h);
            var t = h - i;
            return byHour[i % n] * (1f - t) + byHour[(i + 1) % n] * t;
        }
    }

    /// <summary>Réglages des piétons (dans traffic.json, champ pedestrians).</summary>
    [Serializable]
    public class PedestrianConfig
    {
        /// <summary>Piétons par km et par côté de chaussée, heure par heure (24 valeurs).</summary>
        public float[] perKmBySideByHour = DefaultDensity();
        /// <summary>Distance du trottoir à l'axe de la chaussée (m).</summary>
        public float sidewalkOffsetMeters = 5f;
        public float walkSpeedMin = 1.0f;
        public float walkSpeedMax = 1.5f;
        /// <summary>Chance qu'un piéton décide de traverser, par minute de marche.</summary>
        public float crossChancePerMinute = 0.6f;
        /// <summary>Marge de temps qu'il garde avant l'arrivée du taxi du joueur pour traverser (s).</summary>
        public float crossSafetySeconds = 2f;
        /// <summary>Il ne traverse pas si le taxi du joueur est à moins de … m, même lent.</summary>
        public float crossMinClearMeters = 20f;
        /// <summary>Les piétons existent dans ce rayon autour du joueur ; ils apparaissent au-delà de minSpawnDistanceMeters.</summary>
        public float activeRadiusMeters = 220f;
        public float minSpawnDistanceMeters = 90f;
        public int maxSpawnsPerUpdate = 2;

        public static float[] DefaultDensity()
        {
            return new[]
            {
                1f, 0f, 0f, 0f, 1f, 4f, 10f, 14f, 14f, 10f, 9f, 9f,
                10f, 10f, 9f, 9f, 11f, 14f, 14f, 11f, 7f, 4f, 2f, 1f,
            };
        }

        public float DensityAt(float hour) => TrafficConfig.Interpolate(perKmBySideByHour, hour);
    }

    /// <summary>Fichier traffic.json : véhicules et piétons.</summary>
    [Serializable]
    public class CityConfig
    {
        public TrafficConfig vehicles = new TrafficConfig();
        public PedestrianConfig pedestrians = new PedestrianConfig();
    }
}
