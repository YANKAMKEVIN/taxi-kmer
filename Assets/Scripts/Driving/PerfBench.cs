using System.Collections;
using System.IO;
using System.Text;
using TaxiKmer.Core.Quality;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Banc de performance sur téléphone : lancé seulement si le fichier <c>banc.txt</c> est présent dans le dossier des
    /// données de l'appli (déposé par <c>tools/bench_android.sh</c> avec adb ; jamais chez un joueur). Le pilote
    /// automatique joue pendant que chaque niveau de qualité est mesuré deux fois : à sa cadence visée (ce que voit le
    /// joueur : images en retard) puis sans limite (marge). Résultats dans le journal Android (étiquette TAXIKMER_BANC)
    /// et dans <c>banc-resultats.txt</c>. Les sauvegardes du banc vont dans un dossier à part, effacé au départ.
    /// </summary>
    public sealed class PerfBench : MonoBehaviour
    {
        public const string FlagFile = "banc.txt";
        public const string ResultFile = "banc-resultats.txt";
        private const string Tag = "TAXIKMER_BANC";

        public float warmupSeconds = 10f;
        public float cappedSeconds = 40f;
        public float uncappedSeconds = 25f;

        private static bool s_started;

        public static bool Requested => File.Exists(Path.Combine(Application.persistentDataPath, FlagFile));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeScene()
        {
            if (!Requested) return;
            var dir = Path.Combine(Application.persistentDataPath, "banc-sauvegarde");
            if (!s_started && Directory.Exists(dir)) Directory.Delete(dir, true);
            SaveService.DirectoryForTests = dir;
            StreetView.TitleDone = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterScene()
        {
            if (!Requested || s_started || FindFirstObjectByType<StreetView>() == null) return;
            s_started = true;
            new GameObject("Banc de performance").AddComponent<PerfBench>();
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            var view = FindFirstObjectByType<StreetView>();
            var quality = FindFirstObjectByType<QualityView>();
            view.gameObject.AddComponent<Autopilot>();
            var report = new StringBuilder();
            Line(report, $"Appareil : {SystemInfo.deviceModel}, {SystemInfo.processorType} ({SystemInfo.processorCount} cœurs), " +
                         $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {SystemInfo.systemMemorySize} Mo, " +
                         $"écran {Screen.width}x{Screen.height}, Unity {Application.unityVersion}");
            yield return new WaitForSecondsRealtime(warmupSeconds);

            var config = QualityView.LoadConfig();
            var stats = new FrameStats();
            for (var level = 0; level < config.levels.Length; level++)
            {
                quality.Apply(level, false);
                var target = config.levels[level].targetFrameRate;
                yield return Measure(stats, cappedSeconds);
                var gcBefore = System.GC.CollectionCount(0);
                Line(report, $"{config.levels[level].name} à {target} i/s : {stats.Describe(target)}");
                Application.targetFrameRate = 120;
                yield return Measure(stats, uncappedSeconds);
                Line(report, $"{config.levels[level].name} sans limite : {stats.Describe(60)} ; " +
                             $"mémoire {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)} Mo, " +
                             $"{System.GC.CollectionCount(0) - gcBefore} ramassages de mémoire");
            }
            quality.Apply(config.defaultLevel, false);
            var autopilot = view != null ? view.GetComponent<Autopilot>() : null;
            if (autopilot != null) Line(report, "Parcours : " + autopilot.Summary());
            Line(report, "FIN");
            File.WriteAllText(Path.Combine(Application.persistentDataPath, ResultFile), report.ToString());
        }

        private static IEnumerator Measure(FrameStats stats, float seconds)
        {
            stats.Clear();
            yield return null; // l'image du changement de réglage ne compte pas
            for (var end = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < end;)
            {
                yield return null;
                stats.Add(Time.unscaledDeltaTime);
            }
        }

        private static void Line(StringBuilder report, string text)
        {
            report.AppendLine(text);
            Debug.Log($"{Tag} {text}");
        }
    }
}
