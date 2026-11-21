using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Police
{
    /// <summary>Un contrôle (poste fixe ou volant) le long de l'itinéraire.</summary>
    public sealed class ControlPost
    {
        public string Id { get; internal set; } = "";
        public string Name { get; internal set; } = "";
        public float Along { get; internal set; }
        public bool Flying { get; internal set; }
        /// <summary>Volant : en place de … à … (minutes de jeu depuis minuit) ; fixe : toute la journée.</summary>
        public int FromMinute { get; internal set; }
        public int ToMinute { get; internal set; } = int.MaxValue;
        internal int LastStopMinute = int.MinValue;
        public bool ActiveAt(int minute) => minute >= FromMinute && minute < ToMinute;
    }

    /// <summary>
    /// Où et quand la police contrôle dans la journée : postes fixes des données et contrôles volants tirés au hasard
    /// (position, heure). <see cref="Update"/> dit si le taxi vient de passer devant un contrôle qui l'arrête.
    /// </summary>
    public sealed class PoliceSchedule
    {
        private readonly PoliceConfig _config;
        private readonly Random _random;
        private readonly List<ControlPost> _posts = new List<ControlPost>();

        public IReadOnlyList<ControlPost> Posts => _posts;

        /// <param name="startMinute">Début de la journée (minutes depuis minuit).</param>
        /// <param name="endMinute">Fin de la journée.</param>
        public PoliceSchedule(PoliceConfig config, float routeLength, int startMinute, int endMinute, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (!config.enabled) return;
            foreach (var c in config.checkpoints ?? Array.Empty<CheckpointData>())
                _posts.Add(new ControlPost { Id = c.id, Name = c.name, Along = c.distanceAlongRouteMeters });
            var span = Math.Max(1, endMinute - startMinute);
            var duration = Math.Max(1, config.flyingDurationMinutes);
            for (var i = 0; i < config.flyingChecksPerDay; i++)
            {
                var from = startMinute + (int)(random.NextDouble() * Math.Max(1, span - duration));
                _posts.Add(new ControlPost
                {
                    Id = $"volant-{i + 1}",
                    Name = "Contrôle volant",
                    Along = (float)(random.NextDouble() * routeLength),
                    Flying = true,
                    FromMinute = from,
                    ToMinute = from + duration,
                });
            }
        }

        /// <summary>
        /// Le taxi est passé de <paramref name="fromAlong"/> à <paramref name="toAlong"/> à cette minute : rend le contrôle
        /// en place qui l'arrête (tiré selon la chance d'arrêt, pas deux fois au même endroit trop vite), sinon null.
        /// </summary>
        public ControlPost Update(float fromAlong, float toAlong, int minute)
        {
            if (!_config.enabled) return null;
            var lo = Math.Min(fromAlong, toAlong);
            var hi = Math.Max(fromAlong, toAlong);
            if (hi <= lo) return null;
            foreach (var p in _posts)
            {
                if (!p.ActiveAt(minute) || p.Along < lo || p.Along >= hi) continue;
                if (p.LastStopMinute != int.MinValue && minute - p.LastStopMinute < _config.checkpointCooldownMinutes) continue;
                var chance = p.Flying ? _config.flyingStopChance : _config.checkpointStopChance;
                if (_random.NextDouble() >= chance) continue;
                p.LastStopMinute = minute;
                return p;
            }
            return null;
        }
    }
}
