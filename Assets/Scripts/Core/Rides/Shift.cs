using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Rides
{
    public enum BoardResult
    {
        Boarded,
        NoRoom,
        AlreadyOnBoard,
        DayOver,
    }

    public enum DropResult
    {
        Paid,
        WrongStop,
        NotOnBoard,
    }

    /// <summary>
    /// Une journée de travail : horloge, habitacle et comptes.
    /// La présentation appelle Board quand le taxi est arrêté près du client accepté,
    /// et Drop quand il est arrêté à un arrêt.
    /// </summary>
    public sealed class Shift
    {
        public DayClock Clock { get; }
        public DayLedger Ledger { get; }
        public TaxiCabin Cabin { get; }

        public Shift(DayConfig day, SeatingConfig seating)
        {
            Clock = new DayClock(day);
            Ledger = new DayLedger(day);
            Cabin = new TaxiCabin(seating);
        }

        public BoardResult Board(Client client, out BenchSide side)
        {
            side = default;
            if (Clock.IsOver) return BoardResult.DayOver;
            if (Cabin.Contains(client.Id)) return BoardResult.AlreadyOnBoard;
            return Cabin.TryBoard(client, out side) ? BoardResult.Boarded : BoardResult.NoRoom;
        }

        /// <summary>Fait descendre un client ; il paie s'il est à sa destination, sinon il reste à bord.</summary>
        public DropResult Drop(string clientId, string atStopId)
        {
            var client = Cabin.Find(clientId);
            if (client == null) return DropResult.NotOnBoard;
            if (client.DestinationStopId != atStopId) return DropResult.WrongStop;
            Cabin.Remove(client);
            Ledger.AddFare(client.ProposedFare);
            return DropResult.Paid;
        }

        /// <summary>Fait descendre et encaisse tous les clients arrivés à cet arrêt ; renvoie la somme encaissée.</summary>
        public int DropAllAt(string stopId)
        {
            var total = 0;
            foreach (var c in Cabin.PassengersFor(stopId))
                if (Drop(c.Id, stopId) == DropResult.Paid) total += c.ProposedFare;
            return total;
        }
    }
}
