using System.IO;
using UnityEditor;
using UnityEngine;

namespace TaxiKmer.EditorTools
{
    /// <summary>
    /// Matériaux du monde habillé (planche d'ambiance v1) : réglages d'import des textures d'Assets/Art/Textures (normales,
    /// cartes métal/lissé en linéaire, 1024 px au plus) et matériaux URP Lit dans Resources/Art/Materials, un par
    /// emplacement de <see cref="Core.World.CitySlot"/> (noms lus par World/CityMaterials) plus le sol.
    /// Unity -batchmode -nographics -projectPath . -executeMethod TaxiKmer.EditorTools.CityArtSetup.Run -quit
    /// </summary>
    public static class CityArtSetup
    {
        private const string Textures = "Assets/Art/Textures";
        private const string Materials = "Assets/Resources/Art/Materials";

        // Nom du matériau, dossier de matière (ou atlas), teinte, lissé max.
        private static readonly (string name, string texture, Color tint, float smoothness)[] Specs =
        {
            ("RoadPaved", "asphalt_02", new Color(0.92f, 0.92f, 0.92f), 0.35f),
            ("RoadDirt", "dry_ground_01", new Color(1f, 0.78f, 0.66f), 0.15f),
            ("Median", "dirty_concrete", new Color(0.95f, 0.93f, 0.9f), 0.2f),
            ("Lamp", "grey_plaster_02", new Color(0.62f, 0.63f, 0.65f), 0.3f),
            ("Wall0", "painted_plaster_wall", new Color(0.98f, 0.8f, 0.52f), 0.2f),   // ocre
            ("Wall1", "beige_wall_002", new Color(1f, 0.96f, 0.88f), 0.2f),          // crème
            ("Wall2", "blue_plaster_weathered", new Color(1f, 1f, 1f), 0.2f),        // bleu passé
            ("Wall3", "clay_plaster", new Color(1f, 0.82f, 0.74f), 0.15f),           // rose terre
            ("Wall4", "grey_plaster_02", new Color(1f, 0.92f, 0.58f), 0.2f),         // jaune
            ("Wall5", "damaged_plaster", new Color(1f, 1f, 1f), 0.15f),              // usé
            ("Wall6", "painted_plaster_wall", new Color(0.72f, 0.9f, 0.76f), 0.2f),  // vert d'eau
            ("Wall7", "grey_plaster_02", new Color(0.92f, 0.92f, 0.92f), 0.2f),      // béton brut (chantier)
            ("RoofTin", "corrugated_iron", new Color(0.95f, 0.95f, 0.95f), 0.6f),
            ("RoofRust", "green_metal_rust", new Color(0.78f, 0.48f, 0.32f), 0.35f),
            ("RoofConcrete", "dirty_concrete", new Color(0.85f, 0.84f, 0.82f), 0.15f),
            ("Windows", "fenetres_atlas", Color.white, 0.55f),
            ("Signs", "enseignes_atlas", Color.white, 0.3f),
            ("Ground", "cracked_red_ground", new Color(1f, 0.92f, 0.86f), 0.1f),
            ("Pole", "bark_brown_02", new Color(0.7f, 0.6f, 0.5f), 0.1f),
            ("Wire", "dirty_concrete", new Color(0.08f, 0.08f, 0.08f), 0.3f),
            ("Trunk", "palm_tree_bark", new Color(0.9f, 0.85f, 0.8f), 0.1f),
            ("Foliage", "forest_leaves_03", new Color(0.45f, 0.75f, 0.35f), 0.15f),
            ("Stall", "weathered_planks", new Color(0.95f, 0.88f, 0.8f), 0.15f),
            ("Umbrella0", "grey_plaster_02", new Color(0.15f, 0.35f, 0.85f), 0.3f),
            ("Umbrella1", "grey_plaster_02", new Color(0.9f, 0.2f, 0.15f), 0.3f),
            ("Umbrella2", "grey_plaster_02", new Color(1f, 0.8f, 0.15f), 0.3f),
            ("Goods", "enseignes_atlas", Color.white, 0.3f),
        };

        [MenuItem("Taxi Kmer/Matériaux du monde")]
        public static void Run()
        {
            ConfigureTextures();
            Directory.CreateDirectory(Materials);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (name, texture, tint, smoothness) in Specs)
            {
                var path = $"{Materials}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                var atlas = texture.EndsWith("_atlas");
                var albedo = Load(atlas ? $"{Textures}/{texture}.png" : $"{Textures}/{texture}/{texture}_albedo.jpg");
                mat.SetTexture("_BaseMap", albedo);
                mat.SetColor("_BaseColor", tint);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", 0f);
                if (!atlas)
                {
                    mat.SetTexture("_BumpMap", Load($"{Textures}/{texture}/{texture}_normal.jpg"));
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_MetallicGlossMap", Load($"{Textures}/{texture}/{texture}_ms.png"));
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    mat.SetFloat("_Smoothness", 1f); // multiplie le lissé de la carte
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                mat.enableInstancing = false; // SRP Batcher
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TaxiKmer] Matériaux du monde : {Specs.Length} dans {Materials}.");
        }

        private static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogError($"[TaxiKmer] Texture introuvable : {path}");
            return t;
        }

        private static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Textures }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                var normal = path.EndsWith("_normal.jpg");
                var mask = path.EndsWith("_ms.png");
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.textureType = type;
                imp.sRGBTexture = !mask && !normal;
                imp.maxTextureSize = 1024;
                imp.mipmapEnabled = true;
                imp.anisoLevel = path.Contains("asphalt") || path.Contains("ground") ? 4 : 1; // sols vus en biais
                imp.wrapMode = TextureWrapMode.Repeat;
                if (path.Contains("_atlas")) imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
        }
    }
}
