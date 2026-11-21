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
        /// <summary>
        /// Panne sèche (décision de Kevin, 2026-11-21 : « On pousse, ou on appelle un technicien du quartier ») :
        /// le taxi s'arrête et le joueur choisit. Pousser : le taxi avance au pas, à cette vitesse (km/h).
        /// </summary>
        public float pushKmh = 3f;
        /// <summary>Appeler un technicien du quartier : il arrive après ce délai (minutes de jeu)…</summary>
        public int technicianDelayMinutes = 45;
        /// <summary>… prend ce prix pour le déplacement (FCFA)…</summary>
        public int technicianFeeFcfa = 2000;
        /// <summary>… et verse ces litres (payés au prix du litre de day.json, compris dans son prix), puis on repart.</summary>
        public float technicianLitres = 5f;
        /// <summary>
        /// Panne sèche avec des passagers : ils descendent sans payer (décision de Kevin du 2026-11-21) et cherchent un
        /// autre taxi ; comptés comme passagers non déposés (bilan, réputation).
        /// </summary>
        public bool passengersLeaveWhenDry = true;
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
        public void Set(float litres)
        {
            Litres = Math.Max(0f, Math.Min(Capacity, litres));
            if (!IsEmpty) Pushing = false;
        }

        /// <summary>Panne sèche : le chauffeur a choisi de pousser (jusqu'à ce qu'il remette du carburant).</summary>
        public bool Pushing { get; private set; }

        /// <summary>Panne sèche, le taxi est arrêté : le joueur doit choisir (pousser ou appeler un technicien).</summary>
        public bool Stranded => IsEmpty && !Pushing;

        /// <summary>Panne sèche : on pousse le taxi (au pas, <see cref="FuelConfig.pushKmh"/>). Sans effet s'il reste du carburant.</summary>
        public bool StartPushing()
        {
            if (!IsEmpty) return false;
            Pushing = true;
            return true;
        }

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

        /// <summary>
        /// Vitesse maximale permise par le réservoir : infinie s'il reste du carburant ; au pas si l'on pousse ;
        /// 0 en panne sèche tant que le joueur n'a pas choisi.
        /// </summary>
        public float SpeedCapKmh => !IsEmpty ? float.PositiveInfinity : Pushing ? Math.Max(0f, _config.pushKmh) : 0f;
    }

    /// <summary>Le technicien du quartier appelé en panne sèche : ce qu'il verse, son prix (carburant compris), son délai.</summary>
    public struct TechnicianCall
    {
        public float Litres;
        /// <summary>Prix total : déplacement + carburant versé.</summary>
        public int Cost;
        public int DelayMinutes;

        /// <summary>Devis du technicien pour ce réservoir (sans rien appliquer).</summary>
        public static TechnicianCall Quote(FuelTank tank, int pricePerLitre)
        {
            var c = tank.Config;
            var litres = Math.Min(tank.Missing, Math.Max(0f, c.technicianLitres));
            return new TechnicianCall
            {
                Litres = litres,
                Cost = Math.Max(0, c.technicianFeeFcfa) + FuelTank.Cost(litres, pricePerLitre),
                DelayMinutes = Math.Max(0, c.technicianDelayMinutes),
            };
        }
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
        /// <summary>« Panne sèche : 3 passagers descendent sans payer » (décision de Kevin du 2026-11-21).</summary>
        public static string PassengersLeftMessage(int count) =>
            count > 1 ? $"Panne sèche : {count} passagers descendent sans payer" : "Panne sèche : ton passager descend sans payer";

        public const string EmptyMessage = "Panne sèche : pousse le taxi, ou appelle un technicien du quartier";

        /// <summary>Bouton « pousser » de la panne sèche.</summary>
        public const string PushLabel = "Pousser le taxi";

        /// <summary>Après avoir choisi de pousser.</summary>
        public const string PushingMessage = "Tu pousses le taxi au pas : direction la station-service";

        /// <summary>Bouton du technicien : « Appeler un technicien (6 200 FCFA, 45 min) ».</summary>
        public static string TechnicianLabel(TechnicianCall quote) =>
            $"Appeler un technicien ({TaxiKmer.Core.Day.DaySummary.Money(quote.Cost)}, {quote.DelayMinutes} min)";

        /// <summary>Le technicien est passé : « Le technicien est arrivé après 45 min : +5 L, 6 200 FCFA. On repart ! ».</summary>
        public static string TechnicianCame(TechnicianCall call) =>
            $"Le technicien est arrivé après {call.DelayMinutes} min : +{TaxiKmer.Core.Day.DaySummary.OneDecimal(call.Litres)} L, {TaxiKmer.Core.Day.DaySummary.Money(call.Cost)}. On repart !";

        /// <summary>Libellé de la dépense dans le bilan.</summary>
        public const string TechnicianExpenseLabel = "Technicien du quartier (carburant compris)";
    }
}
