using System.IO;
using TaxiKmer.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaxiKmer.EditorTools
{
    /// <summary>
    /// Scène « Monde » (tâche 1.2) : monde OSM construit au lancement, caméra qui suit l'itinéraire, crédit OSM.
    /// Menu « Taxi Kmer > Créer la scène Monde », ou :
    /// Unity -batchmode -projectPath . -executeMethod TaxiKmer.EditorTools.WorldSceneSetup.Run -quit
    /// </summary>
    public static class WorldSceneSetup
    {
        private const string MaterialsDir = "Assets/World/Materials";
        public const string ScenePath = "Assets/Scenes/Monde.unity";

        [MenuItem("Taxi Kmer/Créer la scène Monde")]
        public static void Run()
        {
            Directory.CreateDirectory(MaterialsDir);
            // Couleurs provisoires (blocs gris) : on valide le fonctionnel, pas le rendu final.
            var road = MakeMaterial("Route", new Color(0.23f, 0.23f, 0.24f), 0.15f);
            var mainRoad = MakeMaterial("Grand axe", new Color(0.18f, 0.18f, 0.19f), 0.2f);
            var building = MakeMaterial("Bâtiment", new Color(0.72f, 0.71f, 0.69f), 0.05f);
            var ground = MakeMaterial("Sol", new Color(0.55f, 0.42f, 0.30f), 0.0f); // latérite

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Soleil").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.82f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.28f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.85f, 0.9f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 600f;

            var worldGo = new GameObject("Monde");
            var builder = worldGo.AddComponent<WorldBuilder>();
            builder.roadMaterial = road;
            builder.mainRoadMaterial = mainRoad;
            builder.buildingMaterial = building;
            builder.groundMaterial = ground;

            var camGo = new GameObject("Caméra");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 800f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var fly = camGo.AddComponent<RouteFlyCamera>();
            fly.world = builder;
            camGo.AddComponent<OsmCredit>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[TaxiKmer] Scène Monde créée : " + ScenePath);
        }

        private static Material MakeMaterial(string name, Color color, float smoothness)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
