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

            var root = transform;
            Emit(root, "Routes", roads, roadMaterial, false);
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
