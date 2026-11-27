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
            // « alloc » dans banc.txt : seulement la mesure des allocations (plus court).
            var allocOnly = File.ReadAllText(Path.Combine(Application.persistentDataPath, FlagFile)).Contains("alloc");
            for (var level = 0; level < config.levels.Length && !allocOnly; level++)
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
            yield return Allocations(report);
            var autopilot = view != null ? view.GetComponent<Autopilot>() : null;
            if (autopilot != null) Line(report, "Parcours : " + autopilot.Summary());
            Line(report, "FIN");
            File.WriteAllText(Path.Combine(Application.persistentDataPath, ResultFile), report.ToString());
        }

        /// <summary>
        /// Mémoire allouée par image (croissance du tas entre deux images sans ramassage), puis la même chose en coupant
        /// tour à tour chaque type de composant : le type qui fait le plus baisser le chiffre est celui qui alloue.
        /// </summary>
        private IEnumerator Allocations(StringBuilder report)
        {
            var result = new long[1];
            yield return HeapGrowth(240, result);
            var baseline = result[0];
            Line(report, $"Allocations : {baseline} o par image en conduite");
            AllocProbe.Reset();
            AllocProbe.Enabled = true;
            yield return HeapGrowth(240, result);
            AllocProbe.Enabled = false;
            Line(report, "  par section de StreetView : " + AllocProbe.Report());
            var types = new System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.List<MonoBehaviour>>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!mb.enabled || mb == this || mb is Autopilot || (mb.GetType().Namespace ?? "").StartsWith("UnityEngine")) continue; // interface d'Unity : pas d'Update
                if (!types.TryGetValue(mb.GetType(), out var list)) types[mb.GetType()] = list = new System.Collections.Generic.List<MonoBehaviour>();
                list.Add(mb);
            }
            foreach (var kv in types)
            {
                foreach (var mb in kv.Value) if (mb != null) mb.enabled = false;
                yield return null;
                yield return HeapGrowth(90, result);
                foreach (var mb in kv.Value) if (mb != null) mb.enabled = true;
                Line(report, $"  sans {kv.Key.Name} : {result[0]} o par image ({result[0] - baseline:+0;-0} o)");
                yield return null;
            }
        }

        private static IEnumerator HeapGrowth(int frames, long[] result)
        {
            long total = 0;
            var counted = 0;
            var last = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var gc = System.GC.CollectionCount(0);
            for (var i = 0; i < frames; i++)
            {
                yield return null;
                var now = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                var gcNow = System.GC.CollectionCount(0);
                if (gcNow == gc && now >= last) { total += now - last; counted++; } // image sans ramassage
                last = now;
                gc = gcNow;
            }
            result[0] = counted > 0 ? total / counted : 0;
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
