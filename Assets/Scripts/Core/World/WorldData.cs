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
        /// <summary>Relief (idée n° 8, tools/osm/osm_elevation.py) ; vide = monde plat.</summary>
        public TerrainGrid terrain = new TerrainGrid();
    }

    [Serializable]
    public class TrafficPathsData
    {
        /// <summary>Du début de l'itinéraire (dispensaire) à sa fin (Étoudi).</summary>
        public TrafficPathData forward = new TrafficPathData();
        /// <summary>De la fin de l'itinéraire à son début.</summary>
        public TrafficPathData backward = new TrafficPathData();
        /// <summary>Feux tricolores (position dans le monde), faits main : tools/osm/traffic_lights_*.json.</summary>
        public TrafficLightData[] lights = Array.Empty<TrafficLightData>();
        /// <summary>Murets centraux de la double voie (avec lampadaires), ouverts aux carrefours.</summary>
        public MedianData[] medians = Array.Empty<MedianData>();
    }

    [Serializable]
    public class MedianData
    {
        public float[] xz = Array.Empty<float>();
    }

    [Serializable]
    public class TrafficLightData
    {
        public int id;
        public string name = "";
        public float x;
        public float z;
    }

    [Serializable]
    public class PathLightData
    {
        public int id;
        /// <summary>Position du feu le long de la trajectoire (m).</summary>
        public float along;
    }

    [Serializable]
    public class TrafficPathData
    {
        public int lengthMeters;
        public float[] xz = Array.Empty<float>();
        /// <summary>Par segment : 1 si la route est à double sens (trafic d'en face sur la même chaussée), 0 sinon.</summary>
        public int[] twoWay = Array.Empty<int>();
        /// <summary>Carrefours avec une rue latérale à droite (on y entre et on en sort en tournant à droite).</summary>
        public JunctionData[] junctions = Array.Empty<JunctionData>();
        /// <summary>Feux tricolores rencontrés dans ce sens.</summary>
        public PathLightData[] lights = Array.Empty<PathLightData>();
    }

    [Serializable]
    public class JunctionData
    {
        /// <summary>Position du carrefour le long de la trajectoire (m).</summary>
        public float along;
        /// <summary>Début de la rue latérale, depuis le carrefour vers l'extérieur (≤ 40 m).</summary>
        public float[] xz = Array.Empty<float>();
        /// <summary>1 si des véhicules peuvent en sortir pour entrer sur l'axe.</summary>
        public int canEnter;
        /// <summary>1 si des véhicules de l'axe peuvent s'y engager.</summary>
        public int canExit;
        public string highway = "";
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

    /// <summary>
    /// Grille d'altitudes du monde (idée n° 8) : Copernicus DEM GLO-30, en mètres relatifs à l'origine du monde,
    /// ligne par ligne depuis (originX, originZ) vers +x puis +z. Vide (width = 0) : monde plat.
    /// </summary>
    [Serializable]
    public class TerrainGrid
    {
        public string attribution = "";
        public float originX;
        public float originZ;
        public float cellMeters = 30f;
        public int width;
        public int height;
        public float baseAltitude;
        public float[] heights = Array.Empty<float>();

        public bool HasData => width >= 2 && height >= 2 && heights != null && heights.Length == width * height;

        /// <summary>
        /// Altitude (m, relative à l'origine) au point x, z ; 0 sans relief, bornée au bord de la grille. Interpolée sur les
        /// <b>mêmes triangles que le sol construit</b> par WorldBuilder (diagonale de (i+1, j) à (i, j+1)) : ce qui est posé
        /// à cette hauteur touche exactement le collider du sol (décision de Kevin du 23/11 : trafic, piétons et clients sur
        /// le relief).
        /// </summary>
        public float HeightAt(float x, float z)
        {
            if (!HasData) return 0f;
            var fx = Math.Max(0f, Math.Min(width - 1.001f, (x - originX) / cellMeters));
            var fz = Math.Max(0f, Math.Min(height - 1.001f, (z - originZ) / cellMeters));
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = heights[j * width + i], b = heights[j * width + i + 1];
            float c = heights[(j + 1) * width + i], d = heights[(j + 1) * width + i + 1];
            // Triangle (a, c, b) sous la diagonale, (b, c, d) au-dessus.
            return tx + tz <= 1f
                ? a + (b - a) * tx + (c - a) * tz
                : d + (c - d) * (1f - tx) + (b - d) * (1f - tz);
        }

        /// <summary>Pente (en %, positive en montée) le long de la polyligne au point <paramref name="along"/>, mesurée sur ± <paramref name="span"/> m.</summary>
        public float GradePercent(float[] xz, float[] cumulative, float along, float span = 30f)
        {
            if (!HasData || cumulative == null || cumulative.Length < 2) return 0f;
            var total = cumulative[cumulative.Length - 1];
            float a = Math.Max(0f, along - span), b = Math.Min(total, along + span);
            if (b - a < 1f) return 0f;
            WorldGeometry.SampleAlong(xz, cumulative, a, out var ax, out var az, out _, out _);
            WorldGeometry.SampleAlong(xz, cumulative, b, out var bx, out var bz, out _, out _);
            return (HeightAt(bx, bz) - HeightAt(ax, az)) / (b - a) * 100f;
        }
    }
}
