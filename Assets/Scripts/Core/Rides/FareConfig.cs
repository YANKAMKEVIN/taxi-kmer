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
    /// Au-delà de la dernière tranche, le client propose longDistanceFare. Prix ×2 des prix de Yaoundé (600 à 1 600 FCFA
    /// au lieu de 300 à 800), tranché par Kevin le 21/11 après l'étude d'équilibrage (docs/tech/equilibrage.md).
    /// </summary>
    [Serializable]
    public class FareConfig
    {
        public FareBand[] bands = Array.Empty<FareBand>();
        public int longDistanceFare = 1600;

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
                    new FareBand { maxDistanceMeters = 2000, fare = 600 },
                    new FareBand { maxDistanceMeters = 4000, fare = 800 },
                    new FareBand { maxDistanceMeters = 7000, fare = 1000 },
                    new FareBand { maxDistanceMeters = 10000, fare = 1200 },
                },
                longDistanceFare = 1600,
            };
        }
    }
}
