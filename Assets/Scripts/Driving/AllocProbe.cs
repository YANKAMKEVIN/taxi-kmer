using System.Collections.Generic;
using System.Text;
using UnityEngine.Profiling;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Mémoire allouée par section de <see cref="StreetView"/> (croissance du tas entre deux repères), pour trouver ce qui
    /// alloue à chaque image. Coupé en jeu : seul le banc de performance l'allume (une comparaison par repère sinon).
    /// </summary>
    public static class AllocProbe
    {
        public static bool Enabled;
        private static readonly Dictionary<string, long> Bytes = new Dictionary<string, long>();
        private static long s_last;
        private static int s_frames;

        public static void BeginFrame()
        {
            if (!Enabled) return;
            s_frames++;
            s_last = Profiler.GetMonoUsedSizeLong();
        }

        public static void Mark(string section)
        {
            if (!Enabled) return;
            var now = Profiler.GetMonoUsedSizeLong();
            var delta = now - s_last;
            if (delta > 0) Bytes[section] = (Bytes.TryGetValue(section, out var b) ? b : 0) + delta; // un ramassage donne un écart négatif : ignoré
            s_last = Profiler.GetMonoUsedSizeLong();
        }

        public static void Reset()
        {
            Bytes.Clear();
            s_frames = 0;
        }

        /// <summary>« Street.Update 1 200 o/image ; City.Update 800 o/image ; … », du plus gros au plus petit.</summary>
        public static string Report()
        {
            var list = new List<KeyValuePair<string, long>>(Bytes);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder();
            foreach (var kv in list)
                if (s_frames > 0 && kv.Value / s_frames > 0) sb.Append($"{kv.Key} {kv.Value / s_frames} o ; ");
            return sb.ToString();
        }
    }
}
