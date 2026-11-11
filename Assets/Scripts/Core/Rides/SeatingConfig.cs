using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>
    /// Places du taxi (données : Assets/Resources/Data/seating.json).
    /// Champs publics en camelCase pour JsonUtility (Unity) et System.Text.Json (tests).
    /// </summary>
    [Serializable]
    public class SeatingConfig
    {
        public int minceWidth = 2;
        public int normalWidth = 3;
        public int costaudWidth = 4;
        public int frontBenchWidth = 6;
        public int backBenchWidth = 9;
        public int frontMaxPassengers = 2;
        public int backMaxPassengers = 3;

        public int WidthOf(Corpulence corpulence)
        {
            switch (corpulence)
            {
                case Corpulence.Mince: return minceWidth;
                case Corpulence.Normal: return normalWidth;
                case Corpulence.Costaud: return costaudWidth;
                default: throw new ArgumentOutOfRangeException(nameof(corpulence), corpulence, null);
            }
        }
    }
}
