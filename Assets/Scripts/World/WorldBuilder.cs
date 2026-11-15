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
            var roads = new Dictionary<Vector2Int, MeshData>();
            var mainRoads = new Dictionary<Vector2Int, MeshData>();
            var buildings = new Dictionary<Vector2Int, MeshData>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;

            foreach (var r in data.roads)
            {
                if (r.xz.Length < 4) continue;
                var main = MainRoads.Contains(r.highway);
                var target = Get(main ? mainRoads : roads, Cell(r.xz[0], r.xz[1]));
                // Les grands axes un peu plus haut que les petites rues : pas de scintillement aux carrefours.
                WorldGeometry.AddRoadStrip(target, r.xz, Mathf.Max(3f, r.width), main ? 0.04f : 0.03f);
                for (var i = 0; i < r.xz.Length; i += 2)
                {
                    minX = Mathf.Min(minX, r.xz[i]); maxX = Mathf.Max(maxX, r.xz[i]);
                    minZ = Mathf.Min(minZ, r.xz[i + 1]); maxZ = Mathf.Max(maxZ, r.xz[i + 1]);
                }
            }

            foreach (var b in data.buildings)
            {
                if (b.xz.Length < 6) continue;
                var target = Get(buildings, Cell(b.xz[0], b.xz[1]));
                WorldGeometry.AddBuilding(target, b.xz, Mathf.Max(1, b.levels) * WorldGeometry.LevelHeight);
            }

            // Murets centraux et lampadaires doubles (références photo de Kevin : RN1 en 2 × 2 voies).
            var medians = new Dictionary<Vector2Int, MeshData>();
            var lamps = new Dictionary<Vector2Int, MeshData>();
            if (data.traffic?.medians != null)
                foreach (var m in data.traffic.medians)
                {
                    if (m.xz == null || m.xz.Length < 4) continue;
                    WorldGeometry.AddWallStrip(Get(medians, Cell(m.xz[0], m.xz[1])), m.xz, medianWidth, medianHeight);
                    AddLamps(lamps, m.xz);
                }

            var root = transform;
            Emit(root, "Routes", roads, roadMaterial, false);
            Emit(root, "Murets centraux", medians, buildingMaterial, true);
            Emit(root, "Lampadaires", lamps, buildingMaterial, false);
            Emit(root, "Grands axes", mainRoads, mainRoadMaterial != null ? mainRoadMaterial : roadMaterial, false);
            Emit(root, "Bâtiments", buildings, buildingMaterial, buildingColliders);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Sol";
            ground.transform.SetParent(root, false);
            const float margin = 300f;
            ground.transform.position = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
            // Un Plane Unity mesure 10 × 10 m.
            ground.transform.localScale = new Vector3((maxX - minX + 2 * margin) / 10f, 1f, (maxZ - minZ + 2 * margin) / 10f);
            if (groundMaterial != null) ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            Debug.Log($"[TaxiKmer] Monde construit : {data.roads.Length} routes, {data.buildings.Length} bâtiments, " +
                      $"{roads.Count + mainRoads.Count + buildings.Count} blocs de maillage.");
        }

        /// <summary>Lampadaire double tous les <see cref="lampSpacing"/> m : mât au milieu du muret, deux bras vers les chaussées.</summary>
        private void AddLamps(Dictionary<Vector2Int, MeshData> lamps, float[] xz)
        {
            var cum = WorldGeometry.CumulativeLengths(xz);
            var length = cum[cum.Length - 1];
            if (length < lampSpacing * 0.5f || lampSpacing <= 0f) return;
            for (var d = lampSpacing * 0.5f; d < length; d += lampSpacing)
            {
                WorldGeometry.SampleAlong(xz, cum, d, out var x, out var z, out var dx, out var dz);
                var mesh = Get(lamps, Cell(x, z));
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

        private static MeshData Get(Dictionary<Vector2Int, MeshData> map, Vector2Int key)
        {
            if (!map.TryGetValue(key, out var m)) map[key] = m = new MeshData();
            return m;
        }

        private static void Emit(Transform root, string name, Dictionary<Vector2Int, MeshData> chunks, Material material, bool collider)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            foreach (var kv in chunks)
            {
                var data = kv.Value;
                if (data.Vertices.Count == 0) continue;
                var mesh = new Mesh
                {
                    name = $"{name} {kv.Key.x},{kv.Key.y}",
                    indexFormat = data.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                };
                var verts = new Vector3[data.Vertices.Count];
                for (var i = 0; i < verts.Length; i++)
                {
                    var v = data.Vertices[i];
                    verts[i] = new Vector3(v.X, v.Y, v.Z);
                }
                mesh.vertices = verts;
                mesh.triangles = data.Triangles.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject(mesh.name);
                go.transform.SetParent(group, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }
}
