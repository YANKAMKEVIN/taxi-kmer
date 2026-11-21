using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Un client au bord de la route : où il va, ce qu'il propose de payer, sa carrure.</summary>
    public sealed class Client
    {
        public string Id { get; }
        public string DestinationStopId { get; }
        public int DistanceMeters { get; }
        /// <summary>Prix de la course ; il baisse si le chauffeur accepte la contre-offre du client (négociation, jalon 5).</summary>
        public int ProposedFare { get; private set; }
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

        /// <summary>Négociation acceptée (option A, tranché par Kevin le 21/11) : le client paiera ce prix.</summary>
        public void AgreeFare(int fare)
        {
            if (fare < 0) throw new ArgumentOutOfRangeException(nameof(fare));
            ProposedFare = fare;
        }
    }
}
