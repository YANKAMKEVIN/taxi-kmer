using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Traffic
{
    public enum PedestrianState
    {
        Walking,
        Crossing,
    }

    /// <summary>Un piéton sur le bas-côté d'une trajectoire (côté droit = latéral positif).</summary>
    public sealed class Pedestrian
    {
        public int Id { get; internal set; }
        public int PathIndex { get; internal set; }
        public float Along { get; internal set; }
        /// <summary>Écart à droite de l'axe de la chaussée (m) : ± trottoir, ou entre les deux pendant une traversée.</summary>
        public float Lateral { get; internal set; }
        /// <summary>+1 s'il marche dans le sens de la trajectoire, -1 sinon.</summary>
        public int WalkDirection { get; internal set; }
        public float Speed { get; internal set; }
        public PedestrianState State { get; internal set; }
        public float X { get; internal set; }
        public float Z { get; internal set; }
        /// <summary>Direction où il regarde (unitaire).</summary>
        public float FaceX { get; internal set; }
        public float FaceZ { get; internal set; }
        /// <summary>Variante d'apparence (couleur de vêtements…), tirée à la création.</summary>
        public int Look { get; internal set; }

        internal float CrossTo;
    }

    /// <summary>
    /// Piétons des bas-côtés (jalon 2) : ils marchent sur le trottoir, et traversent de temps en temps quand aucun
    /// véhicule (joueur compris) n'arrive trop près. Pendant la traversée, le trafic s'arrête devant eux.
    /// Densité selon l'heure, seulement autour du joueur.
    /// </summary>
    public sealed class PedestrianSim
    {
        private readonly PedestrianConfig _config;
        private readonly TrafficPath[] _paths;
        private readonly Random _random;
        private readonly List<Pedestrian> _people = new List<Pedestrian>();
        private readonly List<PedestrianObstacle> _onRoad = new List<PedestrianObstacle>();
        private readonly float[] _playerAlong = new float[2];
        private int _nextId = 1;

        public IReadOnlyList<Pedestrian> People => _people;
        /// <summary>Piétons en train de traverser (à donner au trafic comme obstacles).</summary>
        public IReadOnlyList<PedestrianObstacle> OnRoad => _onRoad;
        public float DensityScale { get; set; } = 1f;

        public PedestrianSim(PedestrianConfig config, TrafficPath forward, TrafficPath backward, int seed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _paths = new[] { forward, backward };
            _random = new Random(seed);
        }

        public void Populate(float hour, PlayerCar player)
        {
            Locate(player);
            var target = TargetCount(hour);
            for (var p = 0; p < 2; p++)
                for (var tries = 0; tries < target * 8 && CountNear(p) < target * 2; tries++)
                    Spawn(p, _playerAlong[p] + (float)(_random.NextDouble() * 2 - 1) * _config.activeRadiusMeters);
            UpdateWorld();
        }

        /// <summary>Un pas. <paramref name="vehicles"/> : le trafic (pour savoir quand traverser), peut être vide.</summary>
        public void Update(float dt, float hour, PlayerCar player, IReadOnlyList<TrafficVehicle> vehicles)
        {
            if (dt <= 0f) return;
            Locate(player);
            var crossChance = 1f - (float)Math.Pow(1f - Math.Min(1f, _config.crossChancePerMinute), dt / 60f);
            foreach (var ped in _people)
            {
                if (ped.State == PedestrianState.Walking)
                {
                    ped.Along += ped.WalkDirection * ped.Speed * dt;
                    if (_random.NextDouble() < crossChance && SafeToCross(ped, player, vehicles))
                    {
                        ped.State = PedestrianState.Crossing;
                        ped.CrossTo = -Math.Sign(ped.Lateral) * _config.sidewalkOffsetMeters;
                    }
                }
                else
                {
                    var d = ped.CrossTo - ped.Lateral;
                    var step = ped.Speed * dt;
                    if (Math.Abs(d) <= step)
                    {
                        ped.Lateral = ped.CrossTo;
                        ped.State = PedestrianState.Walking;
                    }
                    else ped.Lateral += Math.Sign(d) * step;
                }
            }

            for (var i = _people.Count - 1; i >= 0; i--)
            {
                var ped = _people[i];
                var len = _paths[ped.PathIndex].Length;
                var far = Math.Abs(ped.Along - _playerAlong[ped.PathIndex]) > _config.activeRadiusMeters + 30f;
                if ((far && ped.State == PedestrianState.Walking) || ped.Along < 0f || ped.Along > len) _people.RemoveAt(i);
            }

            var target = TargetCount(hour);
            var spawned = 0;
            for (var p = 0; p < 2 && spawned < _config.maxSpawnsPerUpdate; p++)
            {
                if (CountNear(p) >= target * 2) continue; // deux côtés
                var min = _config.minSpawnDistanceMeters;
                var dist = min + (float)_random.NextDouble() * Math.Max(0f, _config.activeRadiusMeters - min);
                var sign = _random.NextDouble() < 0.5 ? -1f : 1f;
                if (Spawn(p, _playerAlong[p] + sign * dist) != null) spawned++;
            }
            UpdateWorld();
        }

        /// <summary>
        /// Peut-il traverser ? Les véhicules du trafic doivent pouvoir s'arrêter avant lui sans freiner fort
        /// (distance de freinage + 1 s de réaction + marge) : ils le voient et le laissent passer, comme en ville.
        /// Le taxi du joueur, lui, doit être assez loin pour qu'il ait fini de traverser avant son arrivée :
        /// un piéton ne se jette pas devant le joueur.
        /// </summary>
        public bool SafeToCross(Pedestrian ped, PlayerCar player, IReadOnlyList<TrafficVehicle> vehicles)
        {
            if (vehicles != null)
                foreach (var v in vehicles)
                {
                    var stopping = v.Speed * v.Speed / (2f * BrakingDecel) + v.Speed * 1f + 5f;
                    if (Threatens(ped.X, ped.Z, v.X, v.Z, v.DirX, v.DirZ, stopping, 8f)) return false;
                }
            var crossSeconds = 2f * _config.sidewalkOffsetMeters / Math.Max(0.3f, ped.Speed) + _config.crossSafetySeconds;
            var sign = Math.Sign(player.Speed);
            return !Threatens(ped.X, ped.Z, player.X, player.Z, player.DirX * sign, player.DirZ * sign,
                Math.Abs(player.Speed) * crossSeconds + 5f, _config.crossMinClearMeters);
        }

        /// <summary>Freinage que le trafic accepte pour laisser passer un piéton (m/s²).</summary>
        private const float BrakingDecel = 3f;

        /// <summary>Un véhicule arrive vers lui à moins de <paramref name="reach"/> m, ou est à moins de <paramref name="minClear"/> m.</summary>
        private static bool Threatens(float px, float pz, float vx, float vz, float dirX, float dirZ, float reach, float minClear)
        {
            var dx = px - vx;
            var dz = pz - vz;
            var side = Math.Abs(dx * dirZ - dz * dirX);
            if (side > 15f) return false; // sur une autre route
            var ahead = dx * dirX + dz * dirZ; // > 0 : le véhicule vient vers lui
            if (Math.Sqrt(dx * dx + dz * dz) < minClear && ahead > -3f) return true;
            return ahead > 0f && ahead < reach;
        }

        private void Locate(PlayerCar player)
        {
            for (var p = 0; p < 2; p++)
            {
                _paths[p].Project(player.X, player.Z, out var along, out _);
                _playerAlong[p] = along;
            }
        }

        private int TargetCount(float hour) =>
            (int)Math.Round(_config.DensityAt(hour) * DensityScale * 2f * _config.activeRadiusMeters / 1000f);

        private int CountNear(int path)
        {
            var n = 0;
            foreach (var ped in _people)
                if (ped.PathIndex == path && Math.Abs(ped.Along - _playerAlong[path]) <= _config.activeRadiusMeters) n++;
            return n;
        }

        public Pedestrian Spawn(int path, float along)
        {
            if (along < 2f || along > _paths[path].Length - 2f) return null;
            // Sur un double sens, les deux trajectoires sont la même chaussée : ses piétons sont ceux du sens 0.
            if (path == 1 && _paths[1].IsTwoWay(along)) return null;
            var side = _random.NextDouble() < 0.5 ? 1f : -1f;
            var ped = new Pedestrian
            {
                Id = _nextId++,
                PathIndex = path,
                Along = along,
                Lateral = side * (_config.sidewalkOffsetMeters + (float)_random.NextDouble() * 0.8f),
                WalkDirection = _random.NextDouble() < 0.5 ? 1 : -1,
                Speed = _config.walkSpeedMin + (float)_random.NextDouble() * Math.Max(0f, _config.walkSpeedMax - _config.walkSpeedMin),
                Look = _random.Next(0, 1000),
            };
            _people.Add(ped);
            return ped;
        }

        private void UpdateWorld()
        {
            _onRoad.Clear();
            foreach (var ped in _people)
            {
                _paths[ped.PathIndex].Sample(ped.Along, ped.Lateral, out var x, out var z, out var dx, out var dz);
                ped.X = x;
                ped.Z = z;
                if (ped.State == PedestrianState.Crossing)
                {
                    // Il regarde vers où il traverse : à droite de la trajectoire = (dz, -dx).
                    var s = Math.Sign(ped.CrossTo - ped.Lateral);
                    ped.FaceX = dz * s;
                    ped.FaceZ = -dx * s;
                    _onRoad.Add(new PedestrianObstacle { X = x, Z = z });
                }
                else
                {
                    ped.FaceX = dx * ped.WalkDirection;
                    ped.FaceZ = dz * ped.WalkDirection;
                }
            }
        }
    }
}
