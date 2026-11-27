using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Messages au centre de l'écran, empilés sur quelques lignes au lieu de s'écraser : le plus récent en haut, chacun
    /// avec sa durée ; un message identique encore affiché est simplement prolongé.
    /// </summary>
    public sealed class MessageStack
    {
        private readonly int _capacity;
        private readonly List<(string text, float until)> _items = new List<(string, float)>();

        public MessageStack(int capacity = 2)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public void Push(string text, float seconds, float now)
        {
            Expire(now);
            _items.RemoveAll(m => m.text == text);
            _items.Insert(0, (text, now + seconds));
            if (_items.Count > _capacity) _items.RemoveAt(_items.Count - 1);
        }

        /// <summary>Messages encore affichés, le plus récent d'abord.</summary>
        public IReadOnlyList<string> Visible(float now)
        {
            Expire(now);
            var list = new List<string>(_items.Count);
            foreach (var m in _items) list.Add(m.text);
            return list;
        }

        /// <summary>Comme <see cref="Visible"/>, dans une liste fournie (sans allocation, pour chaque image).</summary>
        public void CopyVisible(float now, List<string> into)
        {
            Expire(now);
            into.Clear();
            foreach (var m in _items) into.Add(m.text);
        }

        public void Clear() => _items.Clear();

        private void Expire(float now) => _items.RemoveAll(m => m.until <= now);
    }

    /// <summary>Compteur qui défile jusqu'à sa nouvelle valeur (recette du HUD) au lieu de sauter.</summary>
    public sealed class RollingCounter
    {
        /// <summary>Durée du défilement, quel que soit l'écart (secondes).</summary>
        public const float RollSeconds = 0.8f;

        private float _shown;
        private float _from;
        private float _elapsed = RollSeconds;

        public int Target { get; private set; }
        public int Shown => (int)Math.Round(_shown);
        public bool Rolling => _elapsed < RollSeconds;

        public RollingCounter(int value)
        {
            Target = value;
            _shown = _from = value;
        }

        public void SetTarget(int value)
        {
            if (value == Target) return;
            _from = _shown;
            Target = value;
            _elapsed = 0f;
        }

        public void Step(float dt)
        {
            if (!Rolling) return;
            _elapsed = Math.Min(RollSeconds, _elapsed + Math.Max(0f, dt));
            var t = _elapsed / RollSeconds;
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            _shown = _from + (Target - _from) * eased;
            if (!Rolling) _shown = Target;
        }
    }

    /// <summary>
    /// Animation du montant payé qui jaillit du taxi : il grossit, marque un temps, puis file vers le compteur de
    /// recette où il arrive à <see cref="Duration"/> (le compteur défile alors).
    /// </summary>
    public static class PaymentPop
    {
        public const float Duration = 1.1f;
        private const float GrowEnd = 0.25f;
        private const float FlyStart = 0.5f;

        /// <param name="t">Temps depuis le paiement (s).</param>
        /// <param name="scale">Taille du texte (1 = normale).</param>
        /// <param name="travel">Avancée vers le compteur, de 0 (au-dessus du taxi) à 1 (sur le compteur).</param>
        public static void Sample(float t, out float scale, out float travel)
        {
            t = Math.Max(0f, Math.Min(Duration, t));
            if (t < GrowEnd)
            {
                var g = t / GrowEnd;
                scale = 0.5f + 0.9f * (1f - (1f - g) * (1f - g));
                travel = 0f;
                return;
            }
            if (t < FlyStart)
            {
                scale = 1.4f - 0.4f * (t - GrowEnd) / (FlyStart - GrowEnd);
                travel = 0f;
                return;
            }
            var f = (t - FlyStart) / (Duration - FlyStart);
            travel = f * f;
            scale = 1f - 0.45f * f;
        }

        public static bool Arrived(float t) => t >= Duration;
    }
}
