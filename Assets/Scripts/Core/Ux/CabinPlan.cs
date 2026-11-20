using System.Collections.Generic;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Une place du plan du taxi : sa banquette, son rang, et le passager assis (null si libre).</summary>
    public struct SeatView
    {
        public BenchSide Bench;
        public int Index;
        public Client Passenger;
        /// <summary>Place libre qui « coince » : il reste un siège sur la banquette, mais pas la largeur pour ce client.</summary>
        public bool Blocking;
    }

    /// <summary>Un client qui attend rentre-t-il dans le taxi ?</summary>
    public enum SeatFit
    {
        Fits,
        /// <summary>Il rentre si un passager change de banquette.</summary>
        NeedsMove,
        NoRoom,
    }

    /// <summary>
    /// Plan du taxi vu de dessus pour le HUD : places avant (à côté du chauffeur) puis arrière, dans l'ordre où les
    /// passagers sont assis, et places qui coincent quand un client ne rentre pas (mécanique de corpulence).
    /// </summary>
    public static class CabinPlan
    {
        /// <summary>Toutes les places, avant puis arrière ; <paramref name="refused"/> : client qui ne rentre pas (ou null).</summary>
        public static List<SeatView> Seats(TaxiCabin cabin, Client refused = null)
        {
            var seats = new List<SeatView>();
            Add(seats, cabin.Front, refused);
            Add(seats, cabin.Back, refused);
            return seats;
        }

        /// <summary>Le client rentre-t-il tel quel, en faisant changer quelqu'un de place, ou pas du tout ?</summary>
        public static SeatFit Fit(TaxiCabin cabin, Client client)
        {
            if (cabin.CanBoard(client)) return SeatFit.Fits;
            return cabin.FindMoveToFit(client) != null ? SeatFit.NeedsMove : SeatFit.NoRoom;
        }

        /// <summary>Largeur relative d'une silhouette dans sa place (0 à 1) : mince, normal, costaud.</summary>
        public static float SilhouetteWidth(Corpulence c)
        {
            switch (c)
            {
                case Corpulence.Mince: return 0.55f;
                case Corpulence.Costaud: return 1f;
                default: return 0.75f;
            }
        }

        private static void Add(List<SeatView> seats, Bench bench, Client refused)
        {
            var blocking = refused != null && bench.Passengers.Count < bench.MaxPassengers && !bench.CanSeat(refused);
            for (var i = 0; i < bench.MaxPassengers; i++)
            {
                var p = i < bench.Passengers.Count ? bench.Passengers[i] : null;
                seats.Add(new SeatView { Bench = bench.Side, Index = i, Passenger = p, Blocking = p == null && blocking });
            }
        }
    }
}
