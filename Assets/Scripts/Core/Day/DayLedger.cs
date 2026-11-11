using System;

namespace TaxiKmer.Core.Day
{
    /// <summary>Comptes de la journée : recettes, versement, carburant, reste.</summary>
    public sealed class DayLedger
    {
        private readonly DayConfig _config;

        public int Revenue { get; private set; }
        public int RideCount { get; private set; }
        public float MetersDriven { get; private set; }

        public DayLedger(DayConfig config) => _config = config;

        public int Versement => _config.versement;

        public float FuelLitres => MetersDriven / 100000f * _config.fuelLitresPer100Km;

        public int FuelCost => (int)Math.Round(FuelLitres * _config.fuelPricePerLitre, MidpointRounding.AwayFromZero);

        /// <summary>Ce qui reste au chauffeur ; peut être négatif (versement non atteint).</summary>
        public int Remaining => Revenue - Versement - FuelCost;

        public void AddFare(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Revenue += amount;
            RideCount++;
        }

        public void AddDistance(float meters)
        {
            if (meters < 0f) throw new ArgumentOutOfRangeException(nameof(meters));
            MetersDriven += meters;
        }
    }
}
