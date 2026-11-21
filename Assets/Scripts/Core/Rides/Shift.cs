using System;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;

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

        /// <summary>Réservoir du taxi (jalon 3) ; null sans réglages de carburant (carburant compté à la distance).</summary>
        public FuelTank Tank { get; }

        public Shift(DayConfig day, SeatingConfig seating, FuelConfig fuel = null, float? startLitres = null)
        {
            Clock = new DayClock(day);
            Ledger = new DayLedger(day);
            Cabin = new TaxiCabin(seating);
            if (fuel != null)
            {
                Tank = new FuelTank(fuel, day, startLitres ?? fuel.startLitres);
                Ledger.UsesFuelPurchases = true;
            }
        }

        /// <summary>Le taxi a roulé <paramref name="meters"/> m : kilomètres du jour et réservoir qui se vide.</summary>
        public void Drive(float meters)
        {
            Ledger.AddDistance(meters);
            Tank?.Consume(meters);
        }

        /// <summary>
        /// À la station : achète du carburant pour <paramref name="fcfa"/> FCFA (sans dépasser le plein), payé sur la
        /// journée. Rend les litres achetés (0 si pas de réservoir ou déjà plein) et leur prix.
        /// </summary>
        public float BuyFuel(int fcfa, out int cost)
        {
            cost = 0;
            if (Tank == null || Tank.IsFull) return 0f;
            var litres = Tank.LitresFor(fcfa, Ledger.FuelPricePerLitre);
            if (litres <= 1e-4f) return 0f;
            cost = FuelTank.Cost(litres, Ledger.FuelPricePerLitre);
            Tank.Add(litres);
            Ledger.BuyFuel(litres, cost);
            return litres;
        }

        public BoardResult Board(Client client, out BenchSide side)
        {
            side = default;
            if (Clock.IsOver) return BoardResult.DayOver;
            if (Cabin.Contains(client.Id)) return BoardResult.AlreadyOnBoard;
            return Cabin.TryBoard(client, out side) ? BoardResult.Boarded : BoardResult.NoRoom;
        }

        /// <summary>Le joueur demande à un passager de changer de banquette ; false si impossible.</summary>
        public bool MovePassenger(string clientId) => !Clock.IsOver && Cabin.TryMove(clientId);

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
