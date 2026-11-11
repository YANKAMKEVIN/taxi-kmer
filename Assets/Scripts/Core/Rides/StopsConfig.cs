using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Un arrêt sur la route, repéré par sa distance depuis le début du tronçon.</summary>
    [Serializable]
    public class Stop
    {
        public string id;
        public string name;
        public int distanceAlongRouteMeters;
    }

    /// <summary>Arrêts du tronçon et répartition des carrures (données : Assets/Resources/Data/stops_*.json).</summary>
    [Serializable]
    public class StopsConfig
    {
        public Stop[] stops = Array.Empty<Stop>();
        public int minceWeight = 30;
        public int normalWeight = 50;
        public int costaudWeight = 20;
    }
}
