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

        public IEnumerable<Client> Passengers
        {
            get
            {
                foreach (var p in Back.Passengers) yield return p;
                foreach (var p in Front.Passengers) yield return p;
            }
        }

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
            foreach (var p in Passengers)
            {
                var from = BenchOf(p);
                var to = Other(from);
                if (!to.CanSeat(p)) continue;
                from.Remove(p);
                to.Seat(p);
                var fits = CanBoard(newcomer);
                to.Remove(p);
                from.Seat(p);
                if (fits) return p;
            }
            return null;
        }

        internal bool Remove(Client client) => Back.Remove(client) || Front.Remove(client);
    }
}
