using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TaxiKmer.EditorTools
{
    /// <summary>
    /// Build Android. Menu « Taxi Kmer > Build Android (APK) » ou « (AAB, Play Store) », ou en ligne de commande :
    /// Unity -batchmode -projectPath . -buildTarget Android -executeMethod TaxiKmer.EditorTools.BuildAndroid.Run -quit
    /// Unity -batchmode -projectPath . -buildTarget Android -executeMethod TaxiKmer.EditorTools.BuildAndroid.RunBundle -quit
    /// Sorties : build/android/TaxiKmer.apk (essais sur téléphone) et build/android/TaxiKmer.aab (Play Store, idée n° 41),
    /// dossier ignoré par git. Les deux compressent les textures en ASTC (tous les téléphones visés, Android 8+ ARM64,
    /// le lisent ; plus petit et plus net qu'ETC2). La signature de l'AAB pour le Play Store se règle dans Player
    /// Settings avec la clé de Kevin (jamais dans le dépôt).
    /// </summary>
    public static class BuildAndroid
    {
        public const string OutputPath = "build/android/TaxiKmer.apk";
        public const string BundlePath = "build/android/TaxiKmer.aab";

        [MenuItem("Taxi Kmer/Build Android (APK)")]
        public static void Run() => Build(OutputPath, false);

        [MenuItem("Taxi Kmer/Build Android (AAB, Play Store)")]
        public static void RunBundle() => Build(BundlePath, true);

        private static void Build(string path, bool appBundle)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            var scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            var mb = File.Exists(path) ? new FileInfo(path).Length / (1024f * 1024f) : 0f;
            Debug.Log($"[TaxiKmer] Build Android {(appBundle ? "AAB" : "APK")} : {summary.result}, {mb:0.0} Mo, {summary.totalTime}.");
            EditorUserBuildSettings.buildAppBundle = false; // l'APK reste le build par défaut des essais
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
