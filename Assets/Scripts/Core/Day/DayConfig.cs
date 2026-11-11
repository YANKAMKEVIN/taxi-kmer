using System;

namespace TaxiKmer.Core.Day
{
    /// <summary>Paramètres d'une journée de travail (données : Assets/Resources/Data/day.json).</summary>
    [Serializable]
    public class DayConfig
    {
        public float dayLengthRealSeconds = 600f;
        public int startHour = 6;
        public int endHour = 20;
        public int versement = 10000;
        public int fuelPricePerLitre = 840;
        public float fuelLitresPer100Km = 8f;
    }
}
