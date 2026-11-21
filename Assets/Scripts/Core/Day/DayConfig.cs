using System;

namespace TaxiKmer.Core.Day
{
    /// <summary>Paramètres d'une journée de travail (données : Assets/Resources/Data/day.json).</summary>
    [Serializable]
    public class DayConfig
    {
        public float dayLengthRealSeconds = 2100f;
        public int startHour = 6;
        public int endHour = 20;
        /// <summary>Versement dû chaque jour (FCFA) : environ 7 000, tranché par Kevin le 21/11 (2026).</summary>
        public int versement = 7000;
        public int fuelPricePerLitre = 840;
        public float fuelLitresPer100Km = 8f;
    }
}
