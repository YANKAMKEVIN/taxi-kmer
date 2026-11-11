using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Tranche de prix : jusqu'à maxDistanceMeters, le client propose fare.</summary>
    [Serializable]
    public class FareBand
    {
        public int maxDistanceMeters;
        public int fare;
    }

    /// <summary>
    /// Prix proposés par les clients selon la distance (données : Assets/Resources/Data/fares.json).
    /// Au-delà de la dernière tranche, le client propose longDistanceFare.
    /// </summary>
    [Serializable]
    public class FareConfig
    {
        public FareBand[] bands = Array.Empty<FareBand>();
        public int longDistanceFare = 800;

        public int FareFor(int distanceMeters)
        {
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            foreach (var band in bands)
                if (distanceMeters <= band.maxDistanceMeters) return band.fare;
            return longDistanceFare;
        }

        public static FareConfig Default()
        {
            return new FareConfig
            {
                bands = new[]
                {
                    new FareBand { maxDistanceMeters = 2000, fare = 300 },
                    new FareBand { maxDistanceMeters = 4000, fare = 400 },
                    new FareBand { maxDistanceMeters = 7000, fare = 500 },
                    new FareBand { maxDistanceMeters = 10000, fare = 600 },
                },
                longDistanceFare = 800,
            };
        }
    }
}
