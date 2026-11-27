using System.Collections.Generic;

namespace TaxiKmer.Core.Rides
{
    /// <summary>
    /// L'habitacle : banquette avant (à côté du chauffeur) et arrière.
    /// Par défaut on remplit l'arrière d'abord, puis l'avant.
    /// </summary>
    public sealed class TaxiCabin
    {
        public Bench Front { get; }
        public Bench Back { get; }

        public TaxiCabin(SeatingConfig config)
        {
            Front = new Bench(BenchSide.Front, config.frontBenchWidth, config.frontMaxPassengers, config);
            Back = new Bench(BenchSide.Back, config.backBenchWidth, config.backMaxPassengers, config);
        }

        public int PassengerCount => Front.Passengers.Count + Back.Passengers.Count;

        /// <summary>Passagers, banquette arrière d'abord. Parcours sans allocation (lu à chaque image par l'affichage).</summary>
        public CabinPassengers Passengers => new CabinPassengers(Back.Passengers, Front.Passengers);

        public bool CanBoard(Client client) => Back.CanSeat(client) || Front.CanSeat(client);

        /// <summary>Fait monter le client ; renvoie false s'il ne rentre nulle part.</summary>
        public bool TryBoard(Client client, out BenchSide side)
        {
            if (Contains(client.Id)) { side = default; return false; }
            if (Back.CanSeat(client)) { Back.Seat(client); side = BenchSide.Back; return true; }
            if (Front.CanSeat(client)) { Front.Seat(client); side = BenchSide.Front; return true; }
            side = default;
            return false;
        }

        public bool Contains(string clientId) => Find(clientId) != null;

        public Client Find(string clientId)
        {
            foreach (var p in Passengers)
                if (p.Id == clientId) return p;
            return null;
        }

        /// <summary>Clients à bord dont la destination est cet arrêt.</summary>
        public List<Client> PassengersFor(string stopId)
        {
            var result = new List<Client>();
            foreach (var p in Passengers)
                if (p.DestinationStopId == stopId) result.Add(p);
            return result;
        }

        public Bench BenchOf(Client client)
        {
            foreach (var p in Back.Passengers) if (p == client) return Back;
            foreach (var p in Front.Passengers) if (p == client) return Front;
            return null;
        }

        private Bench Other(Bench bench) => bench == Back ? Front : Back;

        /// <summary>
        /// Fait passer un passager sur l'autre banquette (le joueur lui demande de changer de place).
        /// Renvoie false s'il n'est pas à bord ou s'il ne rentre pas de l'autre côté.
        /// </summary>
        public bool TryMove(string clientId)
        {
            var client = Find(clientId);
            if (client == null) return false;
            var from = BenchOf(client);
            var to = Other(from);
            if (!to.CanSeat(client)) return false;
            from.Remove(client);
            to.Seat(client);
            return true;
        }

        /// <summary>
        /// Quand le client ne rentre pas : passager à faire changer de place pour lui libérer une place, ou null.
        /// Le joueur choisit ensuite : refuser le client, ou demander ce changement de place.
        /// </summary>
        public Client FindMoveToFit(Client newcomer)
        {
            if (CanBoard(newcomer)) return null;
            // Copie : l'essai déplace les passagers d'une banquette à l'autre, ce qui interdit d'énumérer les banquettes
            // elles-mêmes (exception dès que le premier essai échouait, au klaxon avec un taxi presque plein).
            foreach (var p in new List<Client>(Passengers))
            {
                var from = BenchOf(p);
                var to = Other(from);
                if (!to.CanSeat(p)) continue;
                var index = from.IndexOf(p);
                from.Remove(p);
                to.Seat(p);
                var fits = CanBoard(newcomer);
                to.Remove(p);
                from.SeatAt(index, p); // remis à sa place exacte : l'ordre des banquettes ne change pas
                if (fits) return p;
            }
            return null;
        }

        internal bool Remove(Client client) => Back.Remove(client) || Front.Remove(client);

        /// <summary>Remet un passager sur sa banquette (sauvegarde) ; sur l'autre si elle est pleine ; false s'il ne rentre plus.</summary>
        internal bool Restore(Client client, BenchSide side)
        {
            if (Contains(client.Id)) return false;
            var bench = side == BenchSide.Front ? Front : Back;
            if (bench.CanSeat(client)) { bench.Seat(client); return true; }
            return TryBoard(client, out _);
        }
    }

    /// <summary>Les passagers des deux banquettes, parcourus par foreach sans allocation (énumérateur en struct).</summary>
    public readonly struct CabinPassengers : IEnumerable<Client>
    {
        private readonly IReadOnlyList<Client> _first, _second;

        public CabinPassengers(IReadOnlyList<Client> first, IReadOnlyList<Client> second)
        {
            _first = first;
            _second = second;
        }

        public int Count => _first.Count + _second.Count;

        public Enumerator GetEnumerator() => new Enumerator(_first, _second);
        IEnumerator<Client> IEnumerable<Client>.GetEnumerator() => GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<Client>
        {
            private readonly IReadOnlyList<Client> _first, _second;
            private int _index;

            public Enumerator(IReadOnlyList<Client> first, IReadOnlyList<Client> second)
            {
                _first = first;
                _second = second;
                _index = -1;
            }

            public Client Current => _index < _first.Count ? _first[_index] : _second[_index - _first.Count];
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext() => ++_index < _first.Count + _second.Count;
            public void Reset() => _index = -1;
            public void Dispose() { }
        }
    }
}
