using System;
using System.Collections.Generic;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Road
{
    /// <summary>Ce qui est arrivé au taxi sur un bout de route.</summary>
    public struct RoadEvents
    {
        public List<PotholeHit> Potholes;
        /// <summary>Le pneu vient de crever.</summary>
        public bool Punctured;
        /// <summary>Le taxi vient de tomber en panne.</summary>
        public bool BrokeDown;
    }

    /// <summary>
    /// La route d'une journée (jalon 6, activé le 21/11) : état du taxi (qui passe d'un jour à l'autre), nids-de-poule
    /// (mêmes positions toute la carrière), pluie selon la saison, pannes et crevaisons ; roue de secours et mécanicien
    /// de quartier payés par le chauffeur (décision de Kevin du 21/11). La présentation appelle <see cref="Drive"/> à
    /// chaque image et propose les boutons quand le taxi est crevé ou en panne.
    /// </summary>
    public sealed class RoadDay
    {
        private readonly RoadConfig _config;
        private readonly Random _random;

        public VehicleCondition Car { get; }
        public PotholeField Potholes { get; }
        public WeatherDay Weather { get; }
        public bool Enabled => _config.enabled;

        public const string SpareWheelLabel = "Roue de secours";
        public const string MechanicExpenseLabel = "Mécanicien de quartier";

        /// <param name="careerSeed">Graine des nids-de-poule (la même toute la carrière).</param>
        /// <param name="damage">Dégâts laissés la veille.</param>
        /// <param name="month">Mois du calendrier (saisons des pluies).</param>
        public RoadDay(RoadConfig config, float routeLength, int careerSeed, float damage, int startMinute, int endMinute, int month, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Car = new VehicleCondition(config, damage);
            Potholes = new PotholeField(config, routeLength, careerSeed);
            Weather = new WeatherDay(config, startMinute, endMinute, random, month);
        }

        /// <summary>Vitesse permise par l'état du taxi (infinie si tout va bien, ou si le jalon est coupé).</summary>
        public float SpeedCapKmh => _config.enabled ? Car.SpeedCapKmh : float.PositiveInfinity;

        /// <summary>Le taxi roule de <paramref name="from"/> à <paramref name="to"/> à cette vitesse.</summary>
        public RoadEvents Drive(float from, float to, float speedKmh)
        {
            var e = new RoadEvents { Potholes = new List<PotholeHit>() };
            if (!_config.enabled) return e;
            var wasFlat = Car.FlatTyre;
            e.Potholes = Potholes.Drive(from, to, speedKmh, Car, _random);
            e.Punctured = !wasFlat && Car.FlatTyre;
            if (RoadIncidents.BreakdownWhileDriving(_config, Car, Math.Abs(to - from), _random))
            {
                Car.BreakDown();
                e.BrokeDown = true;
            }
            return e;
        }

        /// <summary>Le chauffeur change la roue lui-même (temps de jeu perdu). False s'il n'est pas crevé.</summary>
        public bool FitSpareWheel(Shift shift)
        {
            if (shift == null) throw new ArgumentNullException(nameof(shift));
            if (!Car.FlatTyre) return false;
            Car.FitSpareWheel();
            shift.Clock.AdvanceGameMinutes(Math.Max(0, _config.spareWheelMinutes));
            return true;
        }

        /// <summary>Prix du mécanicien de quartier pour tout remettre en état (déplacement compris, part du chauffeur).</summary>
        /// <summary>Bouton de la crevaison : « Changer la roue (20 min) », durée de road.json (spareWheelMinutes).</summary>
        public string SpareWheelButton => $"Changer la roue ({TrafficCrash.Minutes(_config.spareWheelMinutes)})";

        public int MechanicQuote()
        {
            Car.Quote(out var cost, out _);
            var share = Math.Max(0f, Math.Min(1f, _config.driverShareOfRepairs));
            return (int)Math.Round(cost * share) + Math.Max(0, _config.mechanicCallFee);
        }

        /// <summary>
        /// Panne : le mécanicien de quartier vient sur place, répare tout (le chauffeur paie, décision de Kevin) et le temps
        /// de la réparation passe. Rend ce que le chauffeur a payé (0 s'il n'y avait rien à faire).
        /// </summary>
        public int CallMechanic(Shift shift)
        {
            if (shift == null) throw new ArgumentNullException(nameof(shift));
            if (!Car.NeedsRepair) return 0;
            shift.Ledger.AddExpense(MechanicExpenseLabel, Math.Max(0, _config.mechanicCallFee));
            return Car.RepairAtGarage(shift) + Math.Max(0, _config.mechanicCallFee);
        }
    }
}
