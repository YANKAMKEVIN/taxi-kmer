using System.Collections.Generic;

namespace TaxiKmer.Core.Rides
{
    public enum BenchSide
    {
        Front,
        Back,
    }

    /// <summary>Une banquette : largeur limitée (somme des carrures) et nombre de places limité.</summary>
    public sealed class Bench
    {
        private readonly List<Client> _passengers = new List<Client>();
        private readonly SeatingConfig _config;

        public BenchSide Side { get; }
        public int Width { get; }
        public int MaxPassengers { get; }
        public IReadOnlyList<Client> Passengers => _passengers;

        public int UsedWidth
        {
            get
            {
                var used = 0;
                foreach (var p in _passengers) used += _config.WidthOf(p.Corpulence);
                return used;
            }
        }

        public Bench(BenchSide side, int width, int maxPassengers, SeatingConfig config)
        {
            Side = side;
            Width = width;
            MaxPassengers = maxPassengers;
            _config = config;
        }

        public bool CanSeat(Client client)
        {
            return _passengers.Count < MaxPassengers
                && UsedWidth + _config.WidthOf(client.Corpulence) <= Width;
        }

        internal void Seat(Client client) => _passengers.Add(client);

        internal bool Remove(Client client) => _passengers.Remove(client);
    }
}
