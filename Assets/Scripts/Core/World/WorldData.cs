using System;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Monde issu d'OpenStreetMap (données : Assets/Resources/Data/world_*.json, générées par tools/osm/osm_route.py).
    /// Coordonnées en mètres locaux : x = est, z = nord ; listes aplaties xz = [x0, z0, x1, z1, ...].
    /// </summary>
    [Serializable]
    public class WorldData
    {
        public string attribution = "";
        public double originLat;
        public double originLon;
        public float corridorMeters;
        public RoadData[] roads = Array.Empty<RoadData>();
        public BuildingData[] buildings = Array.Empty<BuildingData>();
        public IntersectionData[] intersections = Array.Empty<IntersectionData>();
        public RouteData route = new RouteData();
        /// <summary>Trajectoires du trafic, une par sens (tools/osm/osm_traffic.py) ; vides dans un monde plus ancien.</summary>
        public TrafficPathsData traffic = new TrafficPathsData();
    }

    [Serializable]
    public class TrafficPathsData
    {
        /// <summary>Du début de l'itinéraire (dispensaire) à sa fin (Étoudi).</summary>
        public TrafficPathData forward = new TrafficPathData();
        /// <summary>De la fin de l'itinéraire à son début.</summary>
        public TrafficPathData backward = new TrafficPathData();
    }

    [Serializable]
    public class TrafficPathData
    {
        public int lengthMeters;
        public float[] xz = Array.Empty<float>();
        /// <summary>Par segment : 1 si la route est à double sens (trafic d'en face sur la même chaussée), 0 sinon.</summary>
        public int[] twoWay = Array.Empty<int>();
    }

    [Serializable]
    public class RoadData
    {
        public long id;
        public string name = "";
        public string highway = "";
        public int lanes;
        public float width;
        public bool oneway;
        public float[] xz = Array.Empty<float>();
    }

    [Serializable]
    public class BuildingData
    {
        public long id;
        public int levels = 1;
        public float[] xz = Array.Empty<float>();
    }

    [Serializable]
    public class IntersectionData
    {
        public long node;
        public float x;
        public float z;
    }

    [Serializable]
    public class RouteData
    {
        public int lengthMeters;
        public float[] xz = Array.Empty<float>();
    }
}
