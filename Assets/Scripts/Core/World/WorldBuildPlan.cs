using System;

namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Construction progressive du monde (idée n° 21) : les cases de maillage se construisent de la plus proche du
    /// départ du taxi à la plus lointaine, pour que l'écran de chargement s'efface sur un décor déjà complet autour de lui.
    /// </summary>
    public static class WorldBuildPlan
    {
        /// <summary>Indices des centres (x, z à la suite) triés du plus proche au plus lointain du point ; ordre stable à égalité.</summary>
        public static int[] NearestFirst(float[] centersXz, float x, float z)
        {
            var n = centersXz == null ? 0 : centersXz.Length / 2;
            var order = new int[n];
            var keys = new float[n];
            for (var i = 0; i < n; i++)
            {
                order[i] = i;
                float dx = centersXz[2 * i] - x, dz = centersXz[2 * i + 1] - z;
                keys[i] = dx * dx + dz * dz;
            }
            // Tri stable : Array.Sort ne l'est pas, on départage par l'indice.
            Array.Sort(order, (a, b) => keys[a] != keys[b] ? keys[a].CompareTo(keys[b]) : a.CompareTo(b));
            return order;
        }

        /// <summary>Texte de l'écran de chargement.</summary>
        public static string LoadingText(float progress) =>
            $"Chargement de Yaoundé… {(int)Math.Floor(Math.Max(0f, Math.Min(1f, progress)) * 100f)} %";
    }

    /// <summary>Avancement de la construction : unités de travail faites sur le total connu (qui peut grandir).</summary>
    public sealed class WorldBuildProgress
    {
        private int _total, _done;

        public WorldBuildProgress(int total) => _total = Math.Max(0, total);

        public void Add(int units) => _total += Math.Max(0, units);
        public void Done(int units) => _done = Math.Min(_total, _done + Math.Max(0, units));

        /// <summary>De 0 à 1 ; ne recule jamais quand le total grandit (plafonné à 0,99 tant que tout n'est pas fait).</summary>
        public float Fraction
        {
            get
            {
                if (_total == 0) return 1f;
                var f = _done / (float)_total;
                _shown = Math.Max(_shown, _done >= _total ? f : Math.Min(f, 0.99f));
                return _shown;
            }
        }

        private float _shown;
    }
}
