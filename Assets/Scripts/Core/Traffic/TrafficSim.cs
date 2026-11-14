using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Traffic
{
    public enum VehicleState
    {
        Driving,
        /// <summary>Sur la voie de gauche pour dépasser un véhicule arrêté ou lent.</summary>
        Overtaking,
        /// <summary>Taxi concurrent qui ralentit pour prendre un client.</summary>
        StoppingForClient,
        /// <summary>Taxi concurrent arrêté : le client monte.</summary>
        Boarding,
    }

    /// <summary>Un véhicule du trafic, repéré sur la trajectoire de son sens.</summary>
    public sealed class TrafficVehicle
    {
        public int Id { get; internal set; }
        public VehicleKind Kind { get; internal set; }
        /// <summary>0 = vers Étoudi (forward), 1 = vers le dispensaire (backward).</summary>
        public int PathIndex { get; internal set; }
        public float Along { get; internal set; }
        /// <summary>Écart à droite de l'axe de la chaussée (m).</summary>
        public float Lateral { get; internal set; }
        /// <summary>Vitesse (m/s, toujours ≥ 0, dans le sens de la trajectoire).</summary>
        public float Speed { get; internal set; }
        public float DesiredSpeed { get; internal set; }
        public VehicleState State { get; internal set; }
        /// <summary>Taxi concurrent : passagers à bord.</summary>
        public int Passengers { get; internal set; }
        public float Length { get; internal set; }
        public float Width { get; internal set; }
        /// <summary>Position dans le monde (x est, z nord) et direction de marche, mises à jour à chaque pas.</summary>
        public float X { get; internal set; }
        public float Z { get; internal set; }
        public float DirX { get; internal set; }
        public float DirZ { get; internal set; }

        internal float HomeLateral;
        internal float TargetLateral;
        internal float BlockedSeconds;
        internal float StopAlong;
        internal float StopTimer;
        internal object TargetClient;
        internal readonly HashSet<object> Considered = new HashSet<object>();
    }

    /// <summary>Le taxi du joueur, en coordonnées du monde (pour que le trafic le voie comme un obstacle).</summary>
    public struct PlayerCar
    {
        public float X;
        public float Z;
        /// <summary>Direction du capot (unitaire).</summary>
        public float DirX;
        public float DirZ;
        /// <summary>Vitesse (m/s, négative en marche arrière).</summary>
        public float Speed;
        public float Length;
        public float Width;
    }

    /// <summary>Un client qui attend au bord de la route et qu'un taxi concurrent peut prendre.</summary>
    public struct ClientSpot
    {
        /// <summary>Identifiant fourni par l'appelant (rendu tel quel quand le client est pris).</summary>
        public object Key;
        /// <summary>Trajectoire de son sens (0 ou 1).</summary>
        public int PathIndex;
        public float Along;
    }

    /// <summary>Obstacle ponctuel sur une trajectoire (joueur, piéton qui traverse, véhicule qui double en face).</summary>
    internal struct Obstacle
    {
        public float Along;
        public float Lateral;
        public float HalfWidth;
        public float Length;
        /// <summary>Vitesse dans le sens de la trajectoire (m/s).</summary>
        public float Speed;
        public TrafficVehicle Vehicle;
        /// <summary>Piéton : on attend qu'il ait traversé, on ne le contourne pas.</summary>
        public bool Pedestrian;
    }

    /// <summary>
    /// Trafic de l'axe principal (jalon 2) : voitures, taxis concurrents et mototaxis sur une trajectoire par sens,
    /// modèle de suivi IDM (chacun adapte sa vitesse au véhicule de devant), dépassement d'un véhicule arrêté,
    /// mototaxis qui longent les voitures par la droite, taxis concurrents qui prennent les clients au bord de la route.
    /// Le trafic n'existe qu'autour du joueur et sa densité suit l'heure (embouteillages aux heures de pointe).
    /// </summary>
    public sealed class TrafficSim
    {
        private readonly TrafficConfig _config;
        private readonly TrafficPath[] _paths;
        private readonly Random _random;
        private readonly List<TrafficVehicle> _vehicles = new List<TrafficVehicle>();
        private readonly List<Obstacle>[] _obstacles;
        private readonly float[] _playerAlong;
        private readonly bool[] _playerNear;
        private readonly List<object> _taken = new List<object>();
        private int _nextId = 1;

        public IReadOnlyList<TrafficVehicle> Vehicles => _vehicles;
        public IReadOnlyList<TrafficPath> Paths => _paths;
        /// <summary>Clients pris par un taxi concurrent pendant le dernier pas (clés des <see cref="ClientSpot"/>).</summary>
        public IReadOnlyList<object> TakenClients => _taken;
        /// <summary>Multiplicateur de densité (réglage de qualité, 1 par défaut).</summary>
        public float DensityScale { get; set; } = 1f;

        public TrafficSim(TrafficConfig config, TrafficPath forward, TrafficPath backward, int seed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _paths = new[] { forward ?? throw new ArgumentNullException(nameof(forward)), backward ?? throw new ArgumentNullException(nameof(backward)) };
            _random = new Random(seed);
            _obstacles = new[] { new List<Obstacle>(), new List<Obstacle>() };
            _playerAlong = new float[2];
            _playerNear = new bool[2];
        }

        /// <summary>Remplit d'un coup la zone active (au démarrage), sans tenir compte de la distance minimale d'apparition.</summary>
        public void Populate(float hour, PlayerCar player)
        {
            LocatePlayer(player);
            for (var p = 0; p < 2; p++)
            {
                var target = TargetCount(hour);
                for (var tries = 0; tries < target * 4 && CountNear(p) < target; tries++)
                {
                    var along = _playerAlong[p] + (float)(_random.NextDouble() * 2 - 1) * _config.activeRadiusMeters;
                    if (Math.Abs(along - _playerAlong[p]) < 25f) continue; // pas sur le joueur
                    TrySpawn(p, along);
                }
            }
            UpdateWorldPositions();
        }

        /// <summary>Un pas de simulation. <paramref name="hour"/> : heure du jeu (6,5 = 6 h 30).</summary>
        public void Update(float dt, float hour, PlayerCar player, IReadOnlyList<ClientSpot> clients = null,
            IReadOnlyList<PedestrianObstacle> pedestrians = null)
        {
            _taken.Clear();
            if (dt <= 0f) return;
            LocatePlayer(player);
            BuildObstacles(player, pedestrians);
            DecideClientStops(clients);

            // Accélérations calculées sur l'état courant, puis appliquées (pas de dépendance à l'ordre).
            var accel = new float[_vehicles.Count];
            for (var i = 0; i < _vehicles.Count; i++) accel[i] = Acceleration(_vehicles[i], i);
            for (var i = 0; i < _vehicles.Count; i++) Advance(_vehicles[i], accel[i], dt, clients);

            Despawn();
            SpawnToDensity(hour);
            UpdateWorldPositions();
        }

        // ---------- Joueur et obstacles ----------

        private void LocatePlayer(PlayerCar player)
        {
            for (var p = 0; p < 2; p++)
            {
                _paths[p].Project(player.X, player.Z, out var along, out var lateral);
                _playerAlong[p] = along;
                _playerNear[p] = Math.Abs(lateral) < 8f;
            }
        }

        private void BuildObstacles(PlayerCar player, IReadOnlyList<PedestrianObstacle> pedestrians)
        {
            for (var p = 0; p < 2; p++)
            {
                var list = _obstacles[p];
                list.Clear();
                var path = _paths[p];

                path.Project(player.X, player.Z, out var pa, out var pl);
                if (Math.Abs(pl) < 6f)
                {
                    path.Direction(pa, out var dx, out var dz);
                    var along = (player.DirX * dx + player.DirZ * dz) * player.Speed;
                    list.Add(new Obstacle { Along = pa, Lateral = pl, HalfWidth = player.Width / 2f, Length = player.Length, Speed = along });
                }

                if (pedestrians != null)
                    foreach (var ped in pedestrians)
                    {
                        path.Project(ped.X, ped.Z, out var a, out var l);
                        if (Math.Abs(l) < 4.5f) list.Add(new Obstacle { Along = a, Lateral = l, HalfWidth = 0.6f, Length = 0.6f, Speed = 0f, Pedestrian = true });
                    }

                // Véhicules de l'autre sens sur la même chaussée (double sens) : ils sont sur la voie de gauche, où ils
                // ne gênent que ceux qui doublent ; s'ils doublent eux-mêmes, ils empiètent sur notre voie.
                foreach (var v in _vehicles)
                {
                    if (v.PathIndex == p) continue;
                    path.Project(v.X, v.Z, out var a, out var l);
                    if (Math.Abs(l) > 4f) continue;
                    list.Add(new Obstacle { Along = a, Lateral = l, HalfWidth = v.Width / 2f, Length = v.Length, Speed = -v.Speed, Vehicle = v });
                }
            }
        }

        // ---------- Clients des taxis concurrents ----------

        private void DecideClientStops(IReadOnlyList<ClientSpot> clients)
        {
            if (clients == null) return;
            foreach (var v in _vehicles)
            {
                if (!TakesClients(v)) continue;
                if (v.State == VehicleState.StoppingForClient || v.State == VehicleState.Boarding)
                {
                    if (!Contains(clients, v.TargetClient)) Resume(v); // le joueur l'a pris, ou il est parti
                    continue;
                }
                if (v.State != VehicleState.Driving || v.Passengers >= _config.competitorCapacity) continue;
                foreach (var c in clients)
                {
                    if (c.PathIndex != v.PathIndex || v.Considered.Contains(c.Key)) continue;
                    var ahead = c.Along - v.Along;
                    if (ahead < 15f || ahead > 70f) continue;
                    v.Considered.Add(c.Key);
                    if (_random.NextDouble() >= _config.competitorPickupChance) continue;
                    v.State = VehicleState.StoppingForClient;
                    v.TargetClient = c.Key;
                    v.StopAlong = c.Along;
                    v.TargetLateral = v.HomeLateral + 0.8f; // se range vers le bord
                    break;
                }
            }
        }

        private bool TakesClients(TrafficVehicle v) =>
            v.Kind == VehicleKind.Taxi || (v.Kind == VehicleKind.Moto && _config.motoTakesClients);

        private static bool Contains(IReadOnlyList<ClientSpot> clients, object key)
        {
            foreach (var c in clients) if (Equals(c.Key, key)) return true;
            return false;
        }

        private static void Resume(TrafficVehicle v)
        {
            v.State = VehicleState.Driving;
            v.TargetClient = null;
            v.StopTimer = 0f;
            v.TargetLateral = v.HomeLateral;
        }

        // ---------- Suivi (IDM) ----------

        private float Acceleration(TrafficVehicle v, int index)
        {
            var k = _config.Kind(v.Kind);
            var desired = v.DesiredSpeed;

            // Mototaxi qui longe une voiture : il ne la dépasse pas trop vite.
            if (v.Kind == VehicleKind.Moto)
                foreach (var o in _vehicles)
                {
                    if (o == v || o.PathIndex != v.PathIndex || o.Kind == VehicleKind.Moto) continue;
                    if (Math.Abs(o.Along - v.Along) < 8f)
                        desired = Math.Min(desired, o.Speed + _config.motoPassingDeltaKmh / 3.6f);
                }

            var gap = float.MaxValue;
            var leaderSpeed = 0f;
            FindLeader(v, ref gap, ref leaderSpeed, out _);

            if (v.State == VehicleState.StoppingForClient)
            {
                var stopGap = v.StopAlong - v.Along - v.Length / 2f;
                if (stopGap < gap) { gap = Math.Max(0.1f, stopGap + k.minGap); leaderSpeed = 0f; }
            }
            if (v.State == VehicleState.Boarding) return -k.comfortDecel * 2f;

            // Fin de la trajectoire : on ne s'arrête pas (le véhicule disparaît en sortant de la zone).
            return Idm(k, v.Speed, Math.Max(desired, 0.1f), gap, leaderSpeed);
        }

        public static float Idm(VehicleKindConfig k, float v, float desired, float gap, float leaderSpeed)
        {
            var free = 1f - (float)Math.Pow(v / desired, 4);
            if (gap >= float.MaxValue / 2f) return k.maxAccel * free;
            var dv = v - leaderSpeed;
            var sStar = k.minGap + Math.Max(0f, v * k.timeHeadway + v * dv / (2f * (float)Math.Sqrt(k.maxAccel * k.comfortDecel)));
            var g = Math.Max(gap, 0.1f);
            var a = k.maxAccel * (free - (sStar / g) * (sStar / g));
            return Math.Max(a, -9f);
        }

        private void FindLeader(TrafficVehicle v, ref float gap, ref float leaderSpeed, out bool pedestrian)
        {
            pedestrian = false;
            foreach (var o in _vehicles)
            {
                if (o == v || o.PathIndex != v.PathIndex) continue;
                var d = o.Along - v.Along;
                if (d <= 0f || !Overlap(v.Lateral, v.Width, o.Lateral, o.Width)) continue;
                var g = d - (v.Length + o.Length) / 2f;
                if (g < gap) { gap = g; leaderSpeed = o.Speed; pedestrian = false; }
            }
            foreach (var o in _obstacles[v.PathIndex])
            {
                var d = o.Along - v.Along;
                if (d <= -0.5f || !Overlap(v.Lateral, v.Width, o.Lateral, o.HalfWidth * 2f)) continue;
                var g = d - (v.Length + o.Length) / 2f;
                if (g < gap) { gap = g; leaderSpeed = Math.Max(0f, o.Speed); pedestrian = o.Pedestrian; }
            }
        }

        private static bool Overlap(float latA, float widthA, float latB, float widthB) =>
            Math.Abs(latA - latB) < (widthA + widthB) / 2f + 0.05f;

        private void Advance(TrafficVehicle v, float accel, float dt, IReadOnlyList<ClientSpot> clients)
        {
            v.Speed = Math.Max(0f, v.Speed + accel * dt);
            v.Along += v.Speed * dt;

            // Changement de voie progressif.
            var dl = v.TargetLateral - v.Lateral;
            var step = _config.lateralSpeed * dt;
            v.Lateral += Math.Abs(dl) <= step ? dl : Math.Sign(dl) * step;

            switch (v.State)
            {
                case VehicleState.StoppingForClient:
                    if (v.Speed < 0.6f && Math.Abs(v.StopAlong - v.Along) < 9f) { v.State = VehicleState.Boarding; v.StopTimer = 0f; }
                    else if (v.Along > v.StopAlong + 5f) Resume(v); // raté
                    break;
                case VehicleState.Boarding:
                    v.StopTimer += dt;
                    if (v.StopTimer >= _config.competitorStopSeconds)
                    {
                        if (clients != null && Contains(clients, v.TargetClient))
                        {
                            _taken.Add(v.TargetClient);
                            v.Passengers++;
                        }
                        Resume(v);
                    }
                    break;
                case VehicleState.Driving:
                    TryStartOvertake(v, dt);
                    break;
                case VehicleState.Overtaking:
                    TryEndOvertake(v);
                    break;
            }
        }

        private void TryStartOvertake(TrafficVehicle v, float dt)
        {
            if (v.Kind == VehicleKind.Moto) return; // les motos se faufilent déjà par la droite
            var gap = float.MaxValue;
            var leaderSpeed = 0f;
            FindLeader(v, ref gap, ref leaderSpeed, out var pedestrian);
            var blocked = gap < 12f && leaderSpeed < 1.5f && !pedestrian;
            v.BlockedSeconds = blocked ? v.BlockedSeconds + dt : 0f;
            if (v.BlockedSeconds < _config.overtakeWaitSeconds) return;

            var lane = _config.overtakeOffsetMeters;
            if (!LaneClear(v, lane, -8f, 25f)) return;
            if (_paths[v.PathIndex].IsTwoWay(v.Along) && !OncomingClear(v)) return;
            v.State = VehicleState.Overtaking;
            v.TargetLateral = lane;
            v.BlockedSeconds = 0f;
        }

        private void TryEndOvertake(TrafficVehicle v)
        {
            // Revient à droite dès que sa voie est libre autour de lui.
            if (!LaneClear(v, v.HomeLateral, -6f, 10f)) return;
            v.State = VehicleState.Driving;
            v.TargetLateral = v.HomeLateral;
        }

        /// <summary>Aucun véhicule ni obstacle de la trajectoire dans [along + from, along + to] sur la voie donnée.</summary>
        private bool LaneClear(TrafficVehicle v, float lateral, float from, float to)
        {
            foreach (var o in _vehicles)
            {
                if (o == v || o.PathIndex != v.PathIndex) continue;
                var d = o.Along - v.Along;
                if (d >= from && d <= to && Overlap(lateral, v.Width, o.Lateral, o.Width)) return false;
            }
            foreach (var o in _obstacles[v.PathIndex])
            {
                var d = o.Along - v.Along;
                if (d >= from && d <= to && Overlap(lateral, v.Width, o.Lateral, o.HalfWidth * 2f)) return false;
            }
            return true;
        }

        /// <summary>Personne n'arrive en face sur <see cref="TrafficConfig.overtakeClearMeters"/> (double sens).</summary>
        private bool OncomingClear(TrafficVehicle v)
        {
            var clear = _config.overtakeClearMeters;
            foreach (var o in _vehicles)
            {
                if (o.PathIndex == v.PathIndex) continue;
                var dx = o.X - v.X;
                var dz = o.Z - v.Z;
                var ahead = dx * v.DirX + dz * v.DirZ;
                var side = Math.Abs(dx * v.DirZ - dz * v.DirX);
                if (ahead > -5f && ahead < clear && side < 6f) return false;
            }
            return true;
        }

        // ---------- Apparition et disparition ----------

        private int TargetCount(float hour) =>
            (int)Math.Round(_config.DensityAt(hour) * DensityScale * 2f * _config.activeRadiusMeters / 1000f);

        private int CountNear(int path)
        {
            var n = 0;
            foreach (var v in _vehicles)
                if (v.PathIndex == path && Math.Abs(v.Along - _playerAlong[path]) <= _config.activeRadiusMeters) n++;
            return n;
        }

        private void Despawn()
        {
            for (var i = _vehicles.Count - 1; i >= 0; i--)
            {
                var v = _vehicles[i];
                var len = _paths[v.PathIndex].Length;
                var far = Math.Abs(v.Along - _playerAlong[v.PathIndex]) > _config.activeRadiusMeters + 40f;
                if (far || v.Along > len || v.Along < 0f) _vehicles.RemoveAt(i);
            }
        }

        private void SpawnToDensity(float hour)
        {
            var target = TargetCount(hour);
            var spawned = 0;
            for (var p = 0; p < 2 && spawned < _config.maxSpawnsPerUpdate; p++)
            {
                if (CountNear(p) >= target) continue;
                // Au bord de la zone active, devant ou derrière le joueur, au-delà de la distance minimale.
                var min = _config.minSpawnDistanceMeters;
                var max = _config.activeRadiusMeters;
                var dist = min + (float)_random.NextDouble() * Math.Max(0f, max - min);
                var sign = _random.NextDouble() < 0.5 ? -1f : 1f;
                if (TrySpawn(p, _playerAlong[p] + sign * dist) != null) spawned++;
            }
        }

        /// <summary>Fait apparaître un véhicule (type tiré au hasard si non précisé) ; null si la place est prise.</summary>
        public TrafficVehicle TrySpawn(int path, float along, VehicleKind? forcedKind = null)
        {
            var len = _paths[path].Length;
            if (along < 5f || along > len - 5f) return null;
            foreach (var o in _vehicles)
                if (o.PathIndex == path && Math.Abs(o.Along - along) < _config.minSpawnGapMeters) return null;

            var kind = forcedKind ?? PickKind();
            var k = _config.Kind(kind);
            var lateral = kind == VehicleKind.Moto ? _config.motoOffsetMeters : _config.laneOffsetMeters;
            var spread = ((float)_random.NextDouble() * 2f - 1f) * k.desiredKmhSpread;
            var v = new TrafficVehicle
            {
                Id = _nextId++,
                Kind = kind,
                PathIndex = path,
                Along = along,
                Lateral = lateral,
                HomeLateral = lateral,
                TargetLateral = lateral,
                DesiredSpeed = Math.Max(2f, (k.desiredKmh + spread) / 3.6f),
                Length = k.length,
                Width = k.width,
                Passengers = kind == VehicleKind.Taxi ? _random.Next(0, Math.Max(1, _config.competitorCapacity)) : 0,
            };
            v.Speed = v.DesiredSpeed * 0.7f;
            _vehicles.Add(v);
            UpdateWorldPosition(v);
            return v;
        }

        private VehicleKind PickKind()
        {
            var total = _config.car.share + _config.taxi.share + _config.moto.share;
            var r = _random.NextDouble() * Math.Max(total, 1e-6f);
            if (r < _config.car.share) return VehicleKind.Car;
            if (r < _config.car.share + _config.taxi.share) return VehicleKind.Taxi;
            return VehicleKind.Moto;
        }

        private void UpdateWorldPositions()
        {
            foreach (var v in _vehicles) UpdateWorldPosition(v);
        }

        private void UpdateWorldPosition(TrafficVehicle v)
        {
            _paths[v.PathIndex].Sample(v.Along, v.Lateral, out var x, out var z, out var dx, out var dz);
            v.X = x;
            v.Z = z;
            v.DirX = dx;
            v.DirZ = dz;
        }
    }

    /// <summary>Position d'un piéton sur la chaussée (pour que le trafic s'arrête devant lui).</summary>
    public struct PedestrianObstacle
    {
        public float X;
        public float Z;
    }
}
