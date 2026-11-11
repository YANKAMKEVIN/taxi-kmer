using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TaxiKmer.EditorTools
{
    /// <summary>
    /// Configuration du socle (tâche 1.1) : trois niveaux de qualité URP (bas / moyen / haut),
    /// réglages du lecteur Android et scène de démarrage. Rejouable sans risque.
    /// Menu « Taxi Kmer > Configurer le projet », ou en ligne de commande :
    /// Unity -batchmode -projectPath . -executeMethod TaxiKmer.EditorTools.ProjectSetup.Run -quit
    /// </summary>
    public static class ProjectSetup
    {
        private const string RenderingDir = "Assets/Settings/Rendering";
        private const string BootScene = "Assets/Scenes/Boot.unity";

        private struct Tier
        {
            public string Name;
            public float RenderScale;
            public bool Shadows;
            public bool SoftShadows;
            public float ShadowDistance;
            public int ShadowResolution;
            public int Msaa;
            public bool Hdr;
            public float LodBias;
        }

        // Valeurs par défaut (à ajuster à la tâche 1.7 sur téléphone).
        private static readonly Tier[] Tiers =
        {
            new Tier { Name = "Bas", RenderScale = 0.7f, Shadows = false, SoftShadows = false, ShadowDistance = 0f, ShadowResolution = 512, Msaa = 1, Hdr = false, LodBias = 0.5f },
            new Tier { Name = "Moyen", RenderScale = 0.85f, Shadows = true, SoftShadows = false, ShadowDistance = 35f, ShadowResolution = 1024, Msaa = 1, Hdr = false, LodBias = 1f },
            new Tier { Name = "Haut", RenderScale = 1f, Shadows = true, SoftShadows = true, ShadowDistance = 70f, ShadowResolution = 2048, Msaa = 4, Hdr = true, LodBias = 1.5f },
        };

        [MenuItem("Taxi Kmer/Configurer le projet")]
        public static void Run()
        {
            Directory.CreateDirectory(RenderingDir);
            var assets = new UniversalRenderPipelineAsset[Tiers.Length];
            for (var i = 0; i < Tiers.Length; i++) assets[i] = CreateOrUpdatePipeline(Tiers[i]);

            ConfigureQuality(assets);
            GraphicsSettings.defaultRenderPipeline = assets[1];
            ConfigurePlayer();
            CreateBootScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[TaxiKmer] Projet configuré : qualité Bas / Moyen / Haut, Android, scène Boot.");
        }

        private static UniversalRenderPipelineAsset CreateOrUpdatePipeline(Tier tier)
        {
            var path = $"{RenderingDir}/URP-{tier.Name}.asset";
            var rendererPath = $"{RenderingDir}/URP-{tier.Name}-Renderer.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            Set(so, "m_RenderScale", tier.RenderScale);
            Set(so, "m_MainLightShadowsSupported", tier.Shadows);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_SoftShadowsSupported", tier.SoftShadows);
            Set(so, "m_ShadowDistance", tier.ShadowDistance);
            Set(so, "m_MainLightShadowmapResolution", tier.ShadowResolution);
            Set(so, "m_MSAA", tier.Msaa);
            Set(so, "m_SupportsHDR", tier.Hdr);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void ConfigureQuality(UniversalRenderPipelineAsset[] assets)
        {
            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = Tiers.Length;
            for (var i = 0; i < Tiers.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = Tiers[i].Name;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                level.FindPropertyRelative("lodBias").floatValue = Tiers[i].LodBias;
                level.FindPropertyRelative("vSyncCount").intValue = 0;
                level.FindPropertyRelative("antiAliasing").intValue = 0; // l'anticrénelage vient de l'asset URP
                var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                if (excluded != null) excluded.arraySize = 0;
            }

            // Niveau par défaut : Moyen partout (le joueur pourra changer dans les réglages).
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
                for (var i = 0; i < perPlatform.arraySize; i++)
                    perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = 1;
            so.FindProperty("m_CurrentQuality").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Kevin Yankam";
            PlayerSettings.productName = "Taxi Kmer";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.yankamkevin.taxikmer");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Jeu de conduite : paysage uniquement.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static void CreateBootScene()
        {
            if (!File.Exists(BootScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Sol provisoire";
                ground.transform.localScale = new Vector3(5f, 1f, 5f);
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Taxi provisoire";
                box.transform.position = new Vector3(0f, 0.75f, 0f);
                box.transform.localScale = new Vector3(1.8f, 1.5f, 4.2f);
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.position = new Vector3(0f, 4f, -9f);
                    cam.transform.LookAt(box.transform);
                }
                EditorSceneManager.SaveScene(scene, BootScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
        }

        private static void Set(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[TaxiKmer] Propriété URP absente : {name}"); return; }
            p.floatValue = value;
        }

        private static void Set(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[TaxiKmer] Propriété URP absente : {name}"); return; }
            p.intValue = value;
        }

        private static void Set(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[TaxiKmer] Propriété URP absente : {name}"); return; }
            p.boolValue = value;
        }
    }
}
