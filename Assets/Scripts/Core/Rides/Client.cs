using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Un client au bord de la route : où il va, ce qu'il propose de payer, sa carrure.</summary>
    public sealed class Client
    {
        public string Id { get; }
        public string DestinationStopId { get; }
        public int DistanceMeters { get; }
        public int ProposedFare { get; }
        public Corpulence Corpulence { get; }

        public Client(string id, string destinationStopId, int distanceMeters, int proposedFare, Corpulence corpulence)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Identifiant vide.", nameof(id));
            if (string.IsNullOrEmpty(destinationStopId)) throw new ArgumentException("Destination vide.", nameof(destinationStopId));
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            if (proposedFare < 0) throw new ArgumentOutOfRangeException(nameof(proposedFare));
            Id = id;
            DestinationStopId = destinationStopId;
            DistanceMeters = distanceMeters;
            ProposedFare = proposedFare;
            Corpulence = corpulence;
        }
    }
}
