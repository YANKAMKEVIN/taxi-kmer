using System.Collections;
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
        [Tooltip("Relief réel (idée n° 8, world.terrain) : routes, bâtiments et sol suivent les collines. Coupé tant que " +
                 "le trafic, les piétons et les clients sont posés à y = 0 (à vérifier dans Unity avant de l'activer).")]
        public bool applyTerrain;
        [Tooltip("Idée n° 21 : construire le monde sur plusieurs images (écran de chargement) au lieu de figer le lancement.")]
        public bool progressive = true;
        [Tooltip("Temps de construction par image (ms) en mode progressif.")]
        public float frameBudgetMs = 12f;

        public WorldData Data { get; private set; }
        /// <summary>Le monde est entièrement construit (idée n° 21 : construction sur plusieurs images).</summary>
        public bool Ready { get; private set; }
        /// <summary>Avancement de la construction (0 à 1), pour l'écran de chargement.</summary>
        public float Progress { get; private set; }
        private bool _building;

        /// <summary>Altitude du sol au point x, z (0 si le relief est coupé ou absent).</summary>
        public float HeightAt(float x, float z) => applyTerrain && Data != null && Data.terrain != null ? Data.terrain.HeightAt(x, z) : 0f;
        /// <summary>Groupe des têtes de lampadaires (allumées la nuit par le cycle jour / nuit).</summary>
        public Transform LampHeads { get; private set; }

        private static readonly HashSet<string> MainRoads = new HashSet<string>
        {
            "trunk", "primary", "secondary", "trunk_link", "primary_link", "secondary_link",
        };

        private void Awake()
        {
            // Idée n° 21 : en jeu, le monde se construit sur plusieurs images derrière un écran de chargement ; les
            // données sont lues tout de suite (les autres composants en ont besoin dès leur Start).
            if (!progressive || !Application.isPlaying) { BuildNow(); return; }
            if (LoadData()) StartCoroutine(BuildOverFrames());
        }

        /// <summary>Charge et construit le monde d'un coup (aussi appelé par l'éditeur pour les captures de contrôle).</summary>
        public void BuildNow()
        {
            if (Ready || _building) return;
            if (Data == null && !LoadData()) return;
            _building = true;
            var steps = Build(Data);
            while (steps.MoveNext()) { }
        }

        private bool LoadData()
        {
            if (Data != null) return true;
            var text = Resources.Load<TextAsset>(worldResource);
            if (text == null)
            {
                Debug.LogError($"[TaxiKmer] Monde introuvable : Resources/{worldResource}");
                return false;
            }
            Data = JsonUtility.FromJson<WorldData>(text.text);
            return true;
        }

        /// <summary>Exécute les étapes de construction tant que l'image a du temps (frameBudgetMs), puis rend la main.</summary>
        private IEnumerator BuildOverFrames()
        {
            _building = true;
            var steps = Build(Data);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (steps.MoveNext())
            {
                if (clock.Elapsed.TotalMilliseconds < frameBudgetMs) continue;
                yield return null;
                clock.Restart();
            }
        }

        /// <summary>Construction du monde en petites étapes (chaque « yield » est un point où l'on peut passer à l'image suivante).</summary>
        private IEnumerator Build(WorldData data)
        {
            var roads = new Dictionary<Vector2Int, MeshData>();
            var mainRoads = new Dictionary<Vector2Int, MeshData>();
            var buildings = new Dictionary<Vector2Int, MeshData>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            var startX = data.route?.xz != null && data.route.xz.Length >= 2 ? data.route.xz[0] : 0f;
            var startZ = data.route?.xz != null && data.route.xz.Length >= 2 ? data.route.xz[1] : 0f;
            var work = new WorldBuildProgress(data.roads.Length + data.buildings.Length);
            var root = transform;
            var terrain = applyTerrain && data.terrain != null && data.terrain.HasData ? data.terrain : null;

            foreach (var r in data.roads)
            {
                work.Done(1);
                Progress = work.Fraction;
                yield return null;
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

            // Le sol d'abord : le taxi doit pouvoir s'y poser pendant que le reste se construit.
            if (terrain != null) BuildTerrainGround(root, terrain);
            else
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Sol";
                ground.transform.SetParent(root, false);
                const float margin = 300f;
                ground.transform.position = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
                // Un Plane Unity mesure 10 × 10 m.
                ground.transform.localScale = new Vector3((maxX - minX + 2 * margin) / 10f, 1f, (maxZ - minZ + 2 * margin) / 10f);
                if (groundMaterial != null) ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            }
            yield return null;

            foreach (var b in data.buildings)
            {
                work.Done(1);
                Progress = work.Fraction;
                yield return null;
                if (b.xz.Length < 6) continue;
                var target = Get(buildings, Cell(b.xz[0], b.xz[1]));
                WorldGeometry.AddBuilding(target, b.xz, Mathf.Max(1, b.levels) * WorldGeometry.LevelHeight);
            }

            // Murets centraux et lampadaires doubles (références photo de Kevin : RN1 en 2 × 2 voies).
            var medians = new Dictionary<Vector2Int, MeshData>();
            var lamps = new Dictionary<Vector2Int, MeshData>();
            var lampHeads = new Dictionary<Vector2Int, MeshData>();
            if (data.traffic?.medians != null)
                foreach (var m in data.traffic.medians)
                {
                    if (m.xz == null || m.xz.Length < 4) continue;
                    WorldGeometry.AddWallStrip(Get(medians, Cell(m.xz[0], m.xz[1])), m.xz, medianWidth, medianHeight);
                    AddLamps(lamps, lampHeads, m.xz);
                }
            yield return null;

            if (terrain != null)
                foreach (var set in new[] { roads, mainRoads, buildings, medians, lamps, lampHeads })
                    foreach (var mesh in set.Values) { Lift(mesh, terrain); yield return null; }

            // Groupes créés tout de suite, cases remplies de la plus proche du départ à la plus lointaine.
            var jobs = new List<(Transform group, Vector2Int cell, MeshData mesh, Material material, bool collider, string name)>();
            void Group(string name, Dictionary<Vector2Int, MeshData> chunks, Material material, bool collider, out Transform group)
            {
                group = new GameObject(name).transform;
                group.SetParent(root, false);
                foreach (var kv in chunks) jobs.Add((group, kv.Key, kv.Value, material, collider, name));
            }
            Group("Routes", roads, roadMaterial, false, out _);
            Group("Murets centraux", medians, buildingMaterial, true, out _);
            Group("Lampadaires", lamps, buildingMaterial, false, out _);
            // Têtes de lampe à part : le cycle jour / nuit les allume (idée n° 3, DayLightView).
            Group("Têtes de lampadaires", lampHeads, buildingMaterial, false, out var heads);
            LampHeads = heads;
            Group("Grands axes", mainRoads, mainRoadMaterial != null ? mainRoadMaterial : roadMaterial, false, out _);
            Group("Bâtiments", buildings, buildingMaterial, buildingColliders, out _);

            var centers = new float[jobs.Count * 2];
            for (var i = 0; i < jobs.Count; i++)
            {
                centers[2 * i] = (jobs[i].cell.x + 0.5f) * chunkSize;
                centers[2 * i + 1] = (jobs[i].cell.y + 0.5f) * chunkSize;
            }
            var order = WorldBuildPlan.NearestFirst(centers, startX, startZ);
            work.Add(jobs.Count);
            foreach (var i in order)
            {
                var job = jobs[i];
                EmitChunk(job.group, $"{job.name} {job.cell.x},{job.cell.y}", job.mesh, job.material, job.collider);
                work.Done(1);
                Progress = work.Fraction;
                yield return null;
            }

            Ready = true;
            Progress = 1f;
            Debug.Log($"[TaxiKmer] Monde construit : {data.roads.Length} routes, {data.buildings.Length} bâtiments, " +
                      $"{roads.Count + mainRoads.Count + buildings.Count} blocs de maillage.");
        }

        /// <summary>Monte chaque sommet à l'altitude du sol sous lui (idée n° 8).</summary>
        private static void Lift(MeshData mesh, TerrainGrid terrain)
        {
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var v = mesh.Vertices[i];
                v.Y += terrain.HeightAt(v.X, v.Z);
                mesh.Vertices[i] = v;
            }
        }

        /// <summary>Sol en relief : une grille de la taille du MNT, avec collider (le taxi roule dessus).</summary>
        private void BuildTerrainGround(Transform root, TerrainGrid t)
        {
            var verts = new Vector3[t.width * t.height];
            for (var j = 0; j < t.height; j++)
            for (var i = 0; i < t.width; i++)
                verts[j * t.width + i] = new Vector3(t.originX + i * t.cellMeters, t.heights[j * t.width + i] - 0.02f, t.originZ + j * t.cellMeters);
            var tris = new int[(t.width - 1) * (t.height - 1) * 6];
            var k = 0;
            for (var j = 0; j < t.height - 1; j++)
            for (var i = 0; i < t.width - 1; i++)
            {
                var a = j * t.width + i;
                tris[k++] = a; tris[k++] = a + t.width; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = a + t.width; tris[k++] = a + t.width + 1;
            }
            var mesh = new Mesh { name = "Sol en relief", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Sol");
            go.transform.SetParent(root, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Lampadaire double tous les <see cref="lampSpacing"/> m : mât au milieu du muret, deux bras vers les chaussées.</summary>
        private void AddLamps(Dictionary<Vector2Int, MeshData> lamps, Dictionary<Vector2Int, MeshData> heads, float[] xz)
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
                    WorldGeometry.AddBox(Get(heads, Cell(x, z)), x + rx * side * 2.4f, lampHeight - 0.45f, z + rz * side * 2.4f, 0.25f, 0.15f, 0.6f, rx, rz);
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

        private static void EmitChunk(Transform group, string name, MeshData data, Material material, bool collider)
        {
            if (data.Vertices.Count == 0) return;
            var mesh = new Mesh
            {
                name = name,
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
