using System;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Fuel
{
    /// <summary>Une station-service le long de l'itinéraire.</summary>
    [Serializable]
    public class StationData
    {
        public string id = "";
        public string name = "";
        /// <summary>Position le long de l'itinéraire (m depuis le dispensaire).</summary>
        public float distanceAlongRouteMeters;
    }

    /// <summary>
    /// Carburant (jalon 3 : jauge réelle, station-service sur la portion ; données : Assets/Resources/Data/fuel.json).
    /// Le prix au litre et la consommation de base restent dans day.json. Valeurs par défaut, à régler avec Kevin.
    /// </summary>
    [Serializable]
    public class FuelConfig
    {
        /// <summary>Contenance du réservoir (L).</summary>
        public float tankLitres = 40f;
        /// <summary>Réservoir au début d'une carrière (L) ; ensuite, il passe d'un jour à l'autre.</summary>
        public float startLitres = 12f;
        /// <summary>
        /// Multiplie la consommation réelle (day.json : fuelLitresPer100Km) : une journée de jeu fait peu de kilomètres,
        /// ce facteur lui fait consommer comme une vraie journée de taxi (sinon la jauge ne bougerait presque pas).
        /// </summary>
        public float consumptionScale = 8f;
        /// <summary>Un appui sur le bouton à la station achète pour ce montant (on « met 2 000 »), sans dépasser le plein.</summary>
        public int refuelStepFcfa = 2000;
        /// <summary>Zone de la station le long de l'itinéraire (m de part et d'autre).</summary>
        public float stationRadiusMeters = 30f;
        /// <summary>Réservoir vide : le taxi ne dépasse plus cette vitesse (km/h), de quoi se traîner jusqu'à la station.</summary>
        public float emptyCrawlKmh = 8f;
        /// <summary>En dessous de cette part du réservoir, la jauge passe au rouge et un message prévient.</summary>
        public float lowFraction = 0.15f;
        /// <summary>Stations sur l'itinéraire (position provisoire, à corriger par Kevin).</summary>
        public StationData[] stations =
        {
            new StationData { id = "station-emana", name = "Station-service d'Emana", distanceAlongRouteMeters = 1850f },
        };
    }

    /// <summary>Le réservoir du taxi : se vide en roulant, se remplit à la station.</summary>
    public sealed class FuelTank
    {
        private readonly FuelConfig _config;
        private readonly float _litresPerMeter;

        public FuelConfig Config => _config;
        public float Capacity => Math.Max(0.1f, _config.tankLitres);
        public float Litres { get; private set; }
        public float Fraction => Litres / Capacity;
        public bool IsEmpty => Litres <= 1e-4f;
        public bool IsLow => Fraction < _config.lowFraction;
        public bool IsFull => Litres >= Capacity - 1e-3f;
        /// <summary>Ce qui manque pour faire le plein (L).</summary>
        public float Missing => Math.Max(0f, Capacity - Litres);

        public FuelTank(FuelConfig config, DayConfig day, float litres)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (day == null) throw new ArgumentNullException(nameof(day));
            _litresPerMeter = Math.Max(0f, day.fuelLitresPer100Km) / 100000f * Math.Max(0f, config.consumptionScale);
            Set(litres);
        }

        /// <summary>Remet le réservoir à ce niveau (sauvegarde), borné à sa contenance.</summary>
        public void Set(float litres) => Litres = Math.Max(0f, Math.Min(Capacity, litres));

        /// <summary>Consommation pour <paramref name="meters"/> m parcourus ; rend les litres brûlés.</summary>
        public float Consume(float meters)
        {
            if (meters < 0f) throw new ArgumentOutOfRangeException(nameof(meters));
            var used = Math.Min(Litres, meters * _litresPerMeter);
            Litres -= used;
            return used;
        }

        /// <summary>Litres que <paramref name="fcfa"/> FCFA achètent au prix donné, sans dépasser le plein.</summary>
        public float LitresFor(int fcfa, int pricePerLitre) =>
            pricePerLitre <= 0 || fcfa <= 0 ? Missing : Math.Min(Missing, fcfa / (float)pricePerLitre);

        /// <summary>Prix de ces litres (arrondi au franc).</summary>
        public static int Cost(float litres, int pricePerLitre) =>
            (int)Math.Round(litres * Math.Max(0, pricePerLitre), MidpointRounding.AwayFromZero);

        public void Add(float litres)
        {
            if (litres < 0f) throw new ArgumentOutOfRangeException(nameof(litres));
            Set(Litres + litres);
        }

        /// <summary>La station dans la zone de laquelle se trouve ce point de l'itinéraire (null sinon).</summary>
        public StationData StationAt(float along)
        {
            if (_config.stations == null) return null;
            foreach (var s in _config.stations)
                if (Math.Abs(s.distanceAlongRouteMeters - along) <= _config.stationRadiusMeters) return s;
            return null;
        }

        /// <summary>Vitesse maximale permise par le réservoir (infinie s'il reste du carburant).</summary>
        public float SpeedCapKmh => IsEmpty ? Math.Max(0f, _config.emptyCrawlKmh) : float.PositiveInfinity;
    }
}

namespace TaxiKmer.Core.Fuel
{
    /// <summary>Textes du carburant pour le HUD et les messages (testés).</summary>
    public static class FuelGauge
    {
        /// <summary>« 12 L » (litres entiers, arrondis vers le bas pour ne pas promettre ce qu'on n'a pas).</summary>
        public static string Label(FuelTank tank) => $"{(int)System.Math.Floor(tank.Litres + 1e-3f)} L";

        /// <summary>Bouton à la station : « Carburant +2,4 L (2 000 FCFA) ».</summary>
        public static string ActionLabel(float litres, int cost) =>
            $"Carburant +{TaxiKmer.Core.Day.DaySummary.OneDecimal(litres)} L ({TaxiKmer.Core.Day.DaySummary.Money(cost)})";

        /// <summary>Après un achat : « +2,4 L pour 2 000 FCFA · réservoir 14 / 40 L ».</summary>
        public static string Bought(float litres, int cost, FuelTank tank) =>
            $"+{TaxiKmer.Core.Day.DaySummary.OneDecimal(litres)} L pour {TaxiKmer.Core.Day.DaySummary.Money(cost)} · réservoir {Label(tank).Replace(" L", "")} / {(int)System.Math.Round(tank.Capacity)} L";

        public const string LowMessage = "Réserve de carburant : passe à la station-service";
        public const string EmptyMessage = "Panne sèche : le taxi se traîne, pousse jusqu'à la station-service";
    }
}
