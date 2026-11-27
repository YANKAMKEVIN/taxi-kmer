using System.Collections.Generic;
using TaxiKmer.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaxiKmer.World
{
    /// <summary>
    /// Construit le monde au lancement à partir du JSON OSM : sol, routes, bâtiments (blocs gris),
    /// regroupés par cases de <see cref="chunkSize"/> m pour limiter les appels de rendu sur téléphone.
    /// Le monde est plat pour le jalon 1 (pas de relief).
    /// </summary>
    public sealed class WorldBuilder : MonoBehaviour
    {
        [Tooltip("Fichier dans Assets/Resources, sans extension.")]
        public string worldResource = "Data/world_messassi_etoudi";
        public Material roadMaterial;
        public Material mainRoadMaterial;
        public Material buildingMaterial;
        public Material groundMaterial;
        public float chunkSize = 250f;
        public bool buildingColliders = true;
        [Tooltip("Muret central de la double voie (références photo : RN1 à Emana, séparateur béton).")]
        public float medianWidth = 0.5f;
        public float medianHeight = 0.8f;
        [Tooltip("Lampadaires doubles sur le muret, tous les … m.")]
        public float lampSpacing = 38f;
        public float lampHeight = 10f;

        public WorldData Data { get; private set; }

        /// <summary>Rues goudronnées en plus des grands axes ; les autres (résidentielles, de service) sont en terre.</summary>
        private static readonly HashSet<string> Paved = new HashSet<string> { "tertiary", "tertiary_link", "unclassified" };

        private static readonly HashSet<string> MainRoads = new HashSet<string>
        {
            "trunk", "primary", "secondary", "trunk_link", "primary_link", "secondary_link",
        };

        private void Awake() => BuildNow();

        /// <summary>Charge et construit le monde (aussi appelé par l'éditeur pour les captures de contrôle).</summary>
        public void BuildNow()
        {
            if (Data != null) return;
            var text = Resources.Load<TextAsset>(worldResource);
            if (text == null)
            {
                Debug.LogError($"[TaxiKmer] Monde introuvable : Resources/{worldResource}");
                return;
            }
            Data = JsonUtility.FromJson<WorldData>(text.text);
            Build(Data);
        }

        private void Build(WorldData data)
        {
            // Monde habillé (planche d'ambiance v1) : un jeu de sous-maillages par bloc, un par matériau (CitySlot).
            var roads = new Dictionary<Vector2Int, MeshSet>();
            var buildings = new Dictionary<Vector2Int, MeshSet>();
            var medians = new Dictionary<Vector2Int, MeshSet>();
            var lamps = new Dictionary<Vector2Int, MeshSet>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;

            foreach (var r in data.roads)
            {
                if (r.xz.Length < 4) continue;
                var main = MainRoads.Contains(r.highway);
                // Grands axes goudronnés ; rues de quartier en terre rouge (références photo de Kevin).
                var paved = main || Paved.Contains(r.highway);
                var target = GetSet(roads, Cell(r.xz[0], r.xz[1]))[paved ? CitySlot.RoadPaved : CitySlot.RoadDirt];
                // Les grands axes un peu plus haut que les petites rues : pas de scintillement aux carrefours.
                WorldGeometry.AddRoadStrip(target, r.xz, Mathf.Max(3f, r.width), main ? 0.04f : 0.03f);
                for (var i = 0; i < r.xz.Length; i += 2)
                {
                    minX = Mathf.Min(minX, r.xz[i]); maxX = Mathf.Max(maxX, r.xz[i]);
                    minZ = Mathf.Min(minZ, r.xz[i + 1]); maxZ = Mathf.Max(maxZ, r.xz[i + 1]);
                }
            }

            var style = LoadStyle();
            var route = data.route != null && data.route.xz != null && data.route.xz.Length >= 4 ? data.route.xz : null;
            var routeCum = route != null ? WorldGeometry.CumulativeLengths(route) : null;
            bool FacesRoute(float x, float z, float nx, float nz)
            {
                if (route == null) return true;
                WorldGeometry.Project(route, routeCum, x, z, out var along, out var dist);
                WorldGeometry.SampleAlong(route, routeCum, along, out var px, out var pz, out _, out _);
                var dx = px - x;
                var dz = pz - z;
                var l = Mathf.Sqrt(dx * dx + dz * dz);
                return l < 1e-3f || (dx * nx + dz * nz) / l > 0.4f;
            }
            var detailed = 0;
            for (var bi = 0; bi < data.buildings.Length; bi++)
            {
                var b = data.buildings[bi];
                if (b.xz.Length < 6) continue;
                var distance = 1e6f;
                if (route != null)
                {
                    WorldGeometry.Project(route, routeCum, b.xz[0], b.xz[1], out _, out distance);
                }
                var s = CityStyle.For(style, b.id != 0 ? (int)(b.id % int.MaxValue) : bi, b.xz.Length / 2, distance);
                if (b.levels > 1) s.Levels = b.levels; // étages connus d'OpenStreetMap : prioritaires
                if (s.Detailed) detailed++;
                FacadeBuilder.Build(GetSet(buildings, Cell(b.xz[0], b.xz[1])), b.xz, s, style, FacesRoute);
            }

            // Murets centraux et lampadaires doubles (références photo de Kevin : RN1 en 2 × 2 voies).
            if (data.traffic?.medians != null)
                foreach (var m in data.traffic.medians)
                {
                    if (m.xz == null || m.xz.Length < 4) continue;
                    WorldGeometry.AddWallStrip(GetSet(medians, Cell(m.xz[0], m.xz[1]))[CitySlot.Median], m.xz, medianWidth, medianHeight);
                    AddLamps(lamps, m.xz);
                }

            // Bas-côtés de l'itinéraire : poteaux et fils, arbres, étals sous parasol (Core/World/RoadsideProps).
            var props = new Dictionary<Vector2Int, MeshSet>();
            var obstacles = BuildProps(data, props);

            var root = transform;
            Emit(root, "Routes", roads, false);
            Emit(root, "Murets centraux", medians, true);
            Emit(root, "Lampadaires", lamps, false);
            Emit(root, "Bâtiments", buildings, buildingColliders);
            Emit(root, "Bas-côtés", props, false);
            AddObstacles(root, obstacles);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Sol";
            ground.transform.SetParent(root, false);
            const float margin = 300f;
            ground.transform.position = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
            // Un Plane Unity mesure 10 × 10 m.
            var sizeX = maxX - minX + 2 * margin;
            var sizeZ = maxZ - minZ + 2 * margin;
            ground.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
            var groundMat = CityMaterials.Named("Ground", groundMaterial);
            if (groundMat != null)
            {
                // Latérite répétée tous les 6 m (instance propre au sol : sa répétition dépend de la taille du monde).
                var mat = new Material(groundMat) { name = "Sol (latérite)" };
                mat.mainTextureScale = new Vector2(sizeX / 6f, sizeZ / 6f);
                ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            Debug.Log($"[TaxiKmer] Monde construit : {data.roads.Length} routes, {data.buildings.Length} bâtiments " +
                      $"(dont {detailed} détaillés), {roads.Count + buildings.Count} blocs de maillage.");
        }

        /// <summary>Planifie et construit les décors de bas-côté ; rend les obstacles à doter d'un collisionneur (troncs, poteaux).</summary>
        private List<Prop> BuildProps(WorldData data, Dictionary<Vector2Int, MeshSet> props)
        {
            var text = Resources.Load<TextAsset>("Data/roadside");
            var config = text != null ? JsonUtility.FromJson<RoadsideConfig>(text.text) : new RoadsideConfig();
            var stopsText = Resources.Load<TextAsset>(worldResource.Replace("world_", "stops_"));
            var stopsAlong = new List<float>();
            if (stopsText != null)
                foreach (var s in JsonUtility.FromJson<Core.Rides.StopsConfig>(stopsText.text).stops) stopsAlong.Add(s.distanceAlongRouteMeters);

            var medians = data.traffic?.medians ?? System.Array.Empty<MedianData>();
            bool Dual(float x, float z)
            {
                foreach (var m in medians)
                {
                    if (m.xz == null || m.xz.Length < 4) continue;
                    WorldGeometry.Project(m.xz, WorldGeometry.CumulativeLengths(m.xz), x, z, out _, out var d);
                    if (d < 6f) return true;
                }
                return false;
            }
            bool NearOtherRoad(float x, float z)
            {
                foreach (var r in data.roads)
                {
                    if (MainRoads.Contains(r.highway) || r.xz.Length < 4) continue;
                    for (var i = 0; i + 3 < r.xz.Length; i += 2)
                        if (SegmentDistance(x, z, r.xz[i], r.xz[i + 1], r.xz[i + 2], r.xz[i + 3]) < config.junctionClearance + r.width / 2f) return true;
                }
                return false;
            }
            var list = RoadsideProps.Plan(config, data.route?.xz, stopsAlong, Dual, NearOtherRoad, 2711);
            foreach (var p in list) PropBuilder.Build(GetSet(props, Cell(p.X, p.Z)), p, config);
            return list;
        }

        private static float SegmentDistance(float x, float z, float ax, float az, float bx, float bz)
        {
            float ex = bx - ax, ez = bz - az;
            var len2 = ex * ex + ez * ez;
            var t = len2 > 1e-6f ? Mathf.Clamp01(((x - ax) * ex + (z - az) * ez) / len2) : 0f;
            float dx = ax + ex * t - x, dz = az + ez * t - z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Collisionneurs simples des troncs et poteaux (on peut les percuter) ; étals et palmes sans collision.</summary>
        private static void AddObstacles(Transform root, List<Prop> props)
        {
            var group = new GameObject("Obstacles des bas-côtés").transform;
            group.SetParent(root, false);
            foreach (var p in props)
            {
                if (p.Kind == PropKind.Stall) continue;
                var go = new GameObject(p.Kind.ToString());
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(p.X, 2f, p.Z);
                go.isStatic = true;
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.radius = p.Kind == PropKind.Pole ? 0.15f : 0.3f * p.Scale;
                capsule.height = 4f;
            }
        }

        private static CityStyleConfig LoadStyle()
        {
            var text = Resources.Load<TextAsset>("Data/city_style");
            return text != null ? JsonUtility.FromJson<CityStyleConfig>(text.text) : new CityStyleConfig();
        }

        /// <summary>Lampadaire double tous les <see cref="lampSpacing"/> m : mât au milieu du muret, deux bras vers les chaussées.</summary>
        private void AddLamps(Dictionary<Vector2Int, MeshSet> lamps, float[] xz)
        {
            var cum = WorldGeometry.CumulativeLengths(xz);
            var length = cum[cum.Length - 1];
            if (length < lampSpacing * 0.5f || lampSpacing <= 0f) return;
            for (var d = lampSpacing * 0.5f; d < length; d += lampSpacing)
            {
                WorldGeometry.SampleAlong(xz, cum, d, out var x, out var z, out var dx, out var dz);
                var mesh = GetSet(lamps, Cell(x, z))[CitySlot.Lamp];
                float rx = dz, rz = -dx; // perpendiculaire au muret
                WorldGeometry.AddBox(mesh, x, lampHeight / 2f, z, 0.18f, lampHeight, 0.18f, dx, dz);
                foreach (var side in new[] { -1f, 1f })
                {
                    // Bras de 2,4 m vers chaque chaussée, légèrement plus bas que le haut du mât, et la tête de lampe au bout.
                    WorldGeometry.AddBox(mesh, x + rx * side * 1.2f, lampHeight - 0.3f, z + rz * side * 1.2f, 0.1f, 0.1f, 2.4f, rx, rz);
                    WorldGeometry.AddBox(mesh, x + rx * side * 2.4f, lampHeight - 0.45f, z + rz * side * 2.4f, 0.25f, 0.15f, 0.6f, rx, rz);
                }
            }
        }

        private Vector2Int Cell(float x, float z) =>
            new Vector2Int(Mathf.FloorToInt(x / chunkSize), Mathf.FloorToInt(z / chunkSize));

        private static MeshSet GetSet(Dictionary<Vector2Int, MeshSet> map, Vector2Int key)
        {
            if (!map.TryGetValue(key, out var m)) map[key] = m = new MeshSet();
            return m;
        }

        /// <summary>Un objet par bloc : un maillage à sous-maillages (un par matériau), statique.</summary>
        private void Emit(Transform root, string name, Dictionary<Vector2Int, MeshSet> chunks, bool collider)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var materials = new List<Material>();
            var submeshes = new List<int[]>();
            foreach (var kv in chunks)
            {
                verts.Clear();
                uvs.Clear();
                materials.Clear();
                submeshes.Clear();
                foreach (var slot in kv.Value.Slots)
                {
                    var data = slot.Value;
                    if (data.Vertices.Count == 0) continue;
                    var offset = verts.Count;
                    for (var i = 0; i < data.Vertices.Count; i++)
                    {
                        var v = data.Vertices[i];
                        verts.Add(new Vector3(v.X, v.Y, v.Z));
                        var uv = data.Uvs[i];
                        uvs.Add(new Vector2(uv.U, uv.V));
                    }
                    var tris = new int[data.Triangles.Count];
                    for (var i = 0; i < tris.Length; i++) tris[i] = data.Triangles[i] + offset;
                    submeshes.Add(tris);
                    materials.Add(CityMaterials.Slot(slot.Key, Fallback((CitySlot)slot.Key)));
                }
                if (verts.Count == 0) continue;
                var mesh = new Mesh
                {
                    name = $"{name} {kv.Key.x},{kv.Key.y}",
                    indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = submeshes.Count;
                for (var i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents(); // cartes de normales
                mesh.RecalculateBounds();

                var go = new GameObject(mesh.name);
                go.transform.SetParent(group, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        /// <summary>Matériau de secours (scène d'avant l'habillage) si celui de Resources/Art/Materials manque.</summary>
        private Material Fallback(CitySlot slot) => slot switch
        {
            CitySlot.RoadPaved => mainRoadMaterial != null ? mainRoadMaterial : roadMaterial,
            CitySlot.RoadDirt => roadMaterial,
            _ => buildingMaterial,
        };
    }
}
