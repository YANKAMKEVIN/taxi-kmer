using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>
    /// Crée les clients qui attendent au bord de la route : destination tirée parmi les autres arrêts,
    /// prix proposé selon la distance, carrure selon les poids des données. Aléatoire déterministe (graine).
    /// </summary>
    public sealed class ClientFactory
    {
        private readonly StopsConfig _stops;
        private readonly FareConfig _fares;
        private readonly Random _random;
        private int _counter;

        public ClientFactory(StopsConfig stops, FareConfig fares, int seed)
        {
            if (stops.stops.Length < 2) throw new ArgumentException("Il faut au moins deux arrêts.");
            if (stops.minceWeight < 0 || stops.normalWeight < 0 || stops.costaudWeight < 0
                || stops.minceWeight + stops.normalWeight + stops.costaudWeight == 0)
                throw new ArgumentException("Poids de carrure invalides.");
            _stops = stops;
            _fares = fares;
            _random = new Random(seed);
        }

        public Stop FindStop(string id)
        {
            foreach (var s in _stops.stops)
                if (s.id == id) return s;
            return null;
        }

        /// <summary>Un client qui attend à l'arrêt donné.</summary>
        public Client CreateAt(string pickupStopId)
        {
            var pickup = FindStop(pickupStopId) ?? throw new ArgumentException($"Arrêt inconnu : {pickupStopId}");
            Stop destination;
            do destination = _stops.stops[_random.Next(_stops.stops.Length)];
            while (destination.id == pickup.id);

            var distance = Math.Abs(destination.distanceAlongRouteMeters - pickup.distanceAlongRouteMeters);
            _counter++;
            return new Client($"client-{_counter}", destination.id, distance, _fares.FareFor(distance), PickCorpulence());
        }

        private Corpulence PickCorpulence()
        {
            var total = _stops.minceWeight + _stops.normalWeight + _stops.costaudWeight;
            var roll = _random.Next(total);
            if (roll < _stops.minceWeight) return Corpulence.Mince;
            if (roll < _stops.minceWeight + _stops.normalWeight) return Corpulence.Normal;
            return Corpulence.Costaud;
        }
    }
}
