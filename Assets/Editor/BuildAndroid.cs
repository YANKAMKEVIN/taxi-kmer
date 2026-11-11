using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TaxiKmer.EditorTools
{
    /// <summary>
    /// Build Android (APK). Menu « Taxi Kmer > Build Android (APK) », ou en ligne de commande :
    /// Unity -batchmode -projectPath . -buildTarget Android -executeMethod TaxiKmer.EditorTools.BuildAndroid.Run -quit
    /// Sortie : build/android/TaxiKmer.apk (dossier ignoré par git).
    /// </summary>
    public static class BuildAndroid
    {
        public const string OutputPath = "build/android/TaxiKmer.apk";

        [MenuItem("Taxi Kmer/Build Android (APK)")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            var apkMb = File.Exists(OutputPath) ? new FileInfo(OutputPath).Length / (1024 * 1024) : 0;
            Debug.Log($"[TaxiKmer] Build Android : {summary.result}, APK {apkMb} Mo, {summary.totalTime}.");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
