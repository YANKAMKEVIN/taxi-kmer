using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Quality
{
    /// <summary>
    /// Statistiques de durées d'image (banc de performance sur téléphone) : médiane, 95ᵉ et 99ᵉ centiles, images par
    /// seconde moyennes et part des images qui dépassent le budget (une image en retard se voit, une moyenne la cache).
    /// </summary>
    public sealed class FrameStats
    {
        private readonly List<float> _ms = new List<float>();

        public int Count => _ms.Count;

        /// <summary>Ajoute une image de <paramref name="seconds"/> secondes (les durées nulles ou négatives sont ignorées).</summary>
        public void Add(float seconds)
        {
            if (seconds > 0f && !float.IsInfinity(seconds) && !float.IsNaN(seconds)) _ms.Add(seconds * 1000f);
        }

        public void Clear() => _ms.Clear();

        /// <summary>Centile (0 à 100) des durées d'image, en millisecondes (0 sans image).</summary>
        public float PercentileMs(float percentile)
        {
            if (_ms.Count == 0) return 0f;
            var sorted = new List<float>(_ms);
            sorted.Sort();
            var rank = Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(percentile / 100f * sorted.Count) - 1));
            return sorted[rank];
        }

        /// <summary>Images par seconde moyennes sur toute la mesure.</summary>
        public float AverageFps
        {
            get
            {
                if (_ms.Count == 0) return 0f;
                var total = 0.0;
                foreach (var m in _ms) total += m;
                return (float)(_ms.Count * 1000.0 / total);
            }
        }

        /// <summary>Part (0 à 1) des images plus longues que le budget de <paramref name="targetFps"/> (marge de 10 %).</summary>
        public float LateShare(int targetFps)
        {
            if (_ms.Count == 0 || targetFps <= 0) return 0f;
            var budget = 1000f / targetFps * 1.1f;
            var late = 0;
            foreach (var m in _ms) if (m > budget) late++;
            return late / (float)_ms.Count;
        }

        /// <summary>« 30,0 i/s, médiane 33,3 ms, 95 % 35,1 ms, 99 % 48,0 ms, 2 % en retard ».</summary>
        public string Describe(int targetFps) =>
            $"{AverageFps:0.0} i/s, médiane {PercentileMs(50):0.0} ms, 95 % {PercentileMs(95):0.0} ms, 99 % {PercentileMs(99):0.0} ms, " +
            $"{LateShare(targetFps) * 100f:0} % en retard sur {targetFps} i/s ({Count} images)";
    }
}
