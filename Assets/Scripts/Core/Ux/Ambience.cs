using System;
using System.Collections.Generic;
using TaxiKmer.Core.Traffic;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Ambiance sonore de la rue (jalon 5 ; données : Assets/Resources/Data/ambience.json). Valeurs par défaut de
    /// Claude, à régler à l'écoute par Kevin ; sons provisoires synthétisés (<see cref="SoundSynth"/>) en attendant de
    /// vrais enregistrements.
    /// </summary>
    [Serializable]
    public class AmbienceConfig
    {
        public bool enabled = true;
        /// <summary>Volume du fond de ville (rumeur), partout sur la portion.</summary>
        public float murmurVolume = 0.12f;
        /// <summary>Lieux à marché (ids de stops_messassi_etoudi.json) : la rumeur du marché monte quand on s'en approche.</summary>
        public string[] marketStops = { "marche-messassi" };
        /// <summary>Le marché s'entend jusqu'à cette distance (m, le long de l'itinéraire), plus fort au centre.</summary>
        public float marketRadiusMeters = 250f;
        /// <summary>Volume du marché au centre, un jour ordinaire.</summary>
        public float marketVolume = 0.35f;
        /// <summary>Volume du marché au centre, un jour de marché (calendar.json).</summary>
        public float marketDayVolume = 0.7f;
        /// <summary>Véhicules comptés pour les klaxons s'ils sont à moins de cette distance du taxi (m).</summary>
        public float hornRadiusMeters = 80f;
        /// <summary>Klaxons par minute pour chaque véhicule proche qui roule.</summary>
        public float hornsPerMinutePerVehicle = 0.4f;
        /// <summary>Un véhicule proche à l'arrêt ou au pas (embouteillage) klaxonne autant de fois plus.</summary>
        public float jamHornFactor = 3f;
        /// <summary>Sous cette vitesse (km/h), un véhicule compte comme bloqué.</summary>
        public float jamKmh = 8f;
        /// <summary>Plafond de klaxons par minute, pour ne pas saturer.</summary>
        public float maxHornsPerMinute = 10f;
        /// <summary>Un client qui attend appelle (« Emana, 600 ! ») quand le taxi passe à moins de cette distance (m).</summary>
        public float clientCallMeters = 60f;
        /// <summary>Volume de l'ambiance pendant qu'un passager parle (comme la radio).</summary>
        public float duckVolume = 0.5f;
    }

    /// <summary>Ce qui se passe dans l'ambiance à cette image : un klaxon au loin, des clients qui appellent.</summary>
    public struct AmbienceEvents
    {
        public bool Horn;
        /// <summary>Clients qui appellent le taxi à cette image (ids), une seule fois chacun.</summary>
        public List<string> Calls;
    }

    /// <summary>
    /// Ambiance sonore (jalon 5 : klaxons, appels des clients, marché) : la présentation donne la position du taxi, les
    /// véhicules proches et les clients qui attendent ; la logique rend les volumes des fonds (rumeur, marché) et les sons
    /// ponctuels à jouer. Coupée par <see cref="AmbienceConfig.enabled"/>.
    /// </summary>
    public sealed class AmbienceMix
    {
        private readonly AmbienceConfig _config;
        private readonly Random _random;
        private readonly float[] _marketAlongs;
        private readonly HashSet<string> _called = new HashSet<string>();

        public float MurmurVolume { get; private set; }
        public float MarketVolume { get; private set; }

        /// <param name="marketAlongs">Position des lieux à marché le long de l'itinéraire (m), résolue par la présentation.</param>
        public AmbienceMix(AmbienceConfig config, float[] marketAlongs, Random random)
        {
            _config = config ?? new AmbienceConfig();
            _marketAlongs = marketAlongs ?? new float[0];
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool Enabled => _config.enabled;

        /// <summary>Klaxons par minute attendus avec ces véhicules proches (roulant / bloqués), plafonnés.</summary>
        public float HornsPerMinute(int moving, int jammed)
        {
            var rate = _config.hornsPerMinutePerVehicle * (Math.Max(0, moving) + Math.Max(0f, _config.jamHornFactor) * Math.Max(0, jammed));
            return Math.Max(0f, Math.Min(_config.maxHornsPerMinute, rate));
        }

        /// <summary>Force du marché (0 à 1) à cette position : 1 au lieu, 0 à <see cref="AmbienceConfig.marketRadiusMeters"/> et au-delà.</summary>
        public float MarketCloseness(float along)
        {
            var radius = Math.Max(1f, _config.marketRadiusMeters);
            var best = 0f;
            foreach (var m in _marketAlongs) best = Math.Max(best, 1f - Math.Abs(along - m) / radius);
            return Math.Max(0f, best);
        }

        /// <summary>Compte les véhicules du trafic proches du taxi, qui roulent et qui sont bloqués.</summary>
        public void CountNear(IReadOnlyList<TrafficVehicle> vehicles, float x, float z, out int moving, out int jammed)
        {
            moving = jammed = 0;
            if (vehicles == null) return;
            var r2 = _config.hornRadiusMeters * _config.hornRadiusMeters;
            foreach (var v in vehicles)
            {
                var dx = v.X - x;
                var dz = v.Z - z;
                if (dx * dx + dz * dz > r2) continue;
                if (v.Speed * 3.6f < _config.jamKmh) jammed++;
                else moving++;
            }
        }

        /// <summary>
        /// À chaque image. <paramref name="waiting"/> : clients qui appellent le taxi (id et position le long de
        /// l'itinéraire) ; <paramref name="talking"/> : un passager parle (l'ambiance baisse, comme la radio) ;
        /// <paramref name="audible"/> : faux pendant la pause ou le bilan (tout se tait).
        /// </summary>
        public AmbienceEvents Update(float dt, float along, int moving, int jammed, IEnumerable<KeyValuePair<string, float>> waiting,
            bool marketDay, bool talking, bool audible = true)
        {
            var events = new AmbienceEvents { Calls = new List<string>() };
            if (!_config.enabled || !audible)
            {
                MurmurVolume = MarketVolume = 0f;
                return events;
            }
            var duck = talking ? Math.Max(0f, Math.Min(1f, _config.duckVolume)) : 1f;
            MurmurVolume = Math.Max(0f, _config.murmurVolume) * duck;
            MarketVolume = MarketCloseness(along) * Math.Max(0f, marketDay ? _config.marketDayVolume : _config.marketVolume) * duck;

            var chance = HornsPerMinute(moving, jammed) * Math.Max(0f, dt) / 60f;
            events.Horn = chance > 0f && _random.NextDouble() < chance;

            if (waiting != null)
                foreach (var w in waiting)
                {
                    if (string.IsNullOrEmpty(w.Key) || _called.Contains(w.Key)) continue;
                    if (Math.Abs(w.Value - along) > _config.clientCallMeters) continue;
                    _called.Add(w.Key);
                    events.Calls.Add(w.Key);
                }
            return events;
        }
    }
}
