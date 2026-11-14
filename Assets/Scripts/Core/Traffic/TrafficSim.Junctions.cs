using System;

namespace TaxiKmer.Core.Traffic
{
    /// <summary>
    /// Carrefours avec les rues latérales (tâche 2.6) : des véhicules de l'axe tournent à droite dans une rue latérale
    /// et disparaissent au bout de son début (40 m) ; d'autres sortent d'une rue latérale, s'arrêtent à la ligne
    /// d'arrêt et entrent sur l'axe en tournant à droite quand il y a un trou (priorité au grand axe).
    /// </summary>
    public sealed partial class TrafficSim
    {
        private float _peakDensity = -1f;

        private void UpdateJunctions(float dt, float hour, PlayerCar player)
        {
            if (_peakDensity < 0f)
            {
                _peakDensity = 0f;
                if (_config.vehiclesPerKmByHour != null)
                    foreach (var d in _config.vehiclesPerKmByHour) _peakDensity = Math.Max(_peakDensity, d);
            }

            DecideExits();
            SpawnFromSideRoads(dt, hour, player);
        }

        /// <summary>Un véhicule qui roule décide (une fois par carrefour) s'il tourne dans la prochaine rue à droite.</summary>
        private void DecideExits()
        {
            foreach (var v in _vehicles)
            {
                if (v.State != VehicleState.Driving) continue;
                foreach (var j in _paths[v.PathIndex].Junctions)
                {
                    var ahead = j.Along - v.Along;
                    if (ahead < 0f) continue;
                    if (ahead > 45f) break; // carrefours triés par distance
                    if (!j.CanExit || !v.Considered.Add(j)) continue;
                    if (ahead < 15f) continue; // trop tard pour tourner proprement
                    if (_random.NextDouble() >= _config.exitChance) continue;
                    v.State = VehicleState.Exiting;
                    v.Junction = j;
                    break;
                }
            }
        }

        private void TurnIntoSideRoad(TrafficVehicle v)
        {
            v.State = VehicleState.LeavingOnSideRoad;
            v.SideDistance = 0f;
            v.Speed = Math.Min(v.Speed, _config.turnSpeedKmh / 3.6f);
        }

        /// <summary>Apparitions dans les rues latérales proches du joueur, au rythme de la densité de l'heure.</summary>
        private void SpawnFromSideRoads(float dt, float hour, PlayerCar player)
        {
            if (_peakDensity <= 0f || _config.junctionEntriesPerMinuteAtPeak <= 0f) return;
            var level = _config.DensityAt(hour) / _peakDensity * DensityScale;
            for (var p = 0; p < 2; p++)
                foreach (var j in _paths[p].Junctions)
                {
                    if (!j.CanEnter || Math.Abs(j.Along - _playerAlong[p]) > _config.activeRadiusMeters) continue;
                    var perMinute = _config.junctionEntriesPerMinuteAtPeak * level * (j.IsService ? _config.serviceRoadFactor : 1f);
                    if (_random.NextDouble() >= perMinute / 60f * dt) continue;
                    if (QueueLength(j) >= _config.maxQueuePerJunction) continue;

                    // Au bout du morceau de rue gardé, pas sous les yeux du joueur.
                    var start = j.Side.Length;
                    j.Side.Sample(start, 0f, out var sx, out var sz, out _, out _);
                    var dx = sx - player.X;
                    var dz = sz - player.Z;
                    if (dx * dx + dz * dz < 40f * 40f) continue;
                    SpawnOnSideRoad(p, j, start);
                }
        }

        private int QueueLength(Junction j)
        {
            var n = 0;
            foreach (var v in _vehicles)
                if (v.State == VehicleState.EnteringFromSideRoad && v.Junction == j) n++;
            return n;
        }

        private void SpawnOnSideRoad(int path, Junction j, float sideDistance)
        {
            var v = TrySpawnDetached(path, j.Along);
            if (v == null) return;
            v.State = VehicleState.EnteringFromSideRoad;
            v.Junction = j;
            v.SideDistance = sideDistance;
            v.Speed = _config.sideRoadSpeedKmh / 3.6f * 0.8f;
            UpdateWorldPosition(v);
        }

        /// <summary>
        /// Rue latérale : le véhicule qui sort s'éloigne librement ; celui qui entre s'arrête derrière la file ou à la
        /// ligne d'arrêt, puis avance jusqu'à l'axe quand il y a un trou.
        /// </summary>
        private float SideRoadAcceleration(TrafficVehicle v)
        {
            var k = _config.Kind(v.Kind);
            var sideSpeed = Math.Max(1f, _config.sideRoadSpeedKmh / 3.6f);
            if (v.State == VehicleState.LeavingOnSideRoad) return Idm(k, v.Speed, sideSpeed, float.MaxValue, 0f);

            // Distance à parcourir vers le carrefour (SideDistance diminue).
            var gap = float.MaxValue;
            var leaderSpeed = 0f;
            foreach (var o in _vehicles)
            {
                if (o == v || o.Junction != v.Junction || o.State != VehicleState.EnteringFromSideRoad) continue;
                var d = v.SideDistance - o.SideDistance; // o est devant (plus près du carrefour)
                if (d <= 0f) continue;
                var g = d - (v.Length + o.Length) / 2f;
                if (g < gap) { gap = g; leaderSpeed = o.Speed; }
            }
            if (!CanEnter(v))
            {
                var toLine = v.SideDistance - _config.stopLineMeters - v.Length / 2f;
                if (toLine + k.minGap < gap) { gap = Math.Max(0.1f, toLine + k.minGap); leaderSpeed = 0f; }
            }
            return Idm(k, v.Speed, Math.Min(sideSpeed, _config.turnSpeedKmh / 3.6f + 1f), gap, leaderSpeed);
        }

        /// <summary>
        /// Trou suffisant sur la voie de droite de l'axe : aucun véhicule (ni le joueur) n'arrive au carrefour avant
        /// <see cref="TrafficConfig.enterGapSeconds"/>, et la place juste après le carrefour est libre.
        /// </summary>
        private bool CanEnter(TrafficVehicle v)
        {
            if (v.SideDistance > _config.stopLineMeters + v.Length / 2f + 3f) return false; // pas encore à la ligne
            if (JunctionHeldByLight(v)) return false;
            var j = v.Junction;
            var lane = _config.laneOffsetMeters;
            // Après une longue attente, il force un peu le passage (le suivant freine) : personne ne reste coincé.
            var gapSeconds = v.BlockedSeconds > _config.forceInAfterSeconds ? 1.5f : _config.enterGapSeconds;
            foreach (var o in _vehicles)
            {
                if (o.PathIndex != v.PathIndex || o.OnSideRoad) continue;
                if (!Overlap(lane, v.Width, o.Lateral, o.Width) && o.Kind != VehicleKind.Moto) continue;
                var behind = j.Along - o.Along; // > 0 : il arrive vers le carrefour
                if (behind < -(o.Length / 2f + v.Length + 4f)) continue; // déjà loin devant
                if (behind < 6f) return false; // sur le carrefour ou juste après
                if (behind < o.Speed * gapSeconds + 6f) return false;
            }
            foreach (var o in _obstacles[v.PathIndex])
            {
                if (o.Pedestrian || !Overlap(lane, v.Width, o.Lateral, o.HalfWidth * 2f)) continue;
                var behind = j.Along - o.Along;
                if (behind < -(o.Length / 2f + v.Length + 4f)) continue;
                if (behind < 6f) return false;
                if (behind < Math.Max(0f, o.Speed) * _config.enterGapSeconds + 6f) return false; // jamais devant le joueur
            }
            return true;
        }

        private void AdvanceOnSideRoad(TrafficVehicle v, float accel, float dt)
        {
            v.Speed = Math.Max(0f, v.Speed + accel * dt);
            if (v.State == VehicleState.LeavingOnSideRoad)
            {
                v.SideDistance += v.Speed * dt;
                if (v.SideDistance >= v.Junction.Side.Length) v.Along = -1f; // disparaît (Despawn)
                return;
            }
            v.SideDistance -= v.Speed * dt;
            v.BlockedSeconds = v.Speed < 0.5f ? v.BlockedSeconds + dt : v.BlockedSeconds;
            if (v.SideDistance > 0f) return;
            // Arrivé sur l'axe : il tourne à droite et prend la voie de droite.
            v.State = VehicleState.Driving;
            v.Along = v.Junction.Along + 1f;
            v.Lateral = v.TargetLateral = v.HomeLateral;
            v.Speed = Math.Max(v.Speed, 2f);
            v.Considered.Add(v.Junction);
            v.Junction = null;
            v.BlockedSeconds = 0f;
        }

        private void SideRoadWorldPosition(TrafficVehicle v)
        {
            var side = v.Junction.Side;
            var leaving = v.State == VehicleState.LeavingOnSideRoad;
            // La rue est décrite depuis le carrefour : on y roule à droite, dans un sens ou dans l'autre.
            side.Sample(Math.Max(0f, v.SideDistance), leaving ? _config.laneOffsetMeters : -_config.laneOffsetMeters,
                out var x, out var z, out var dx, out var dz);
            v.X = x;
            v.Z = z;
            v.DirX = leaving ? dx : -dx;
            v.DirZ = leaving ? dz : -dz;
        }

        /// <summary>Crée un véhicule sans contrôle de place sur l'axe (il est dans une rue latérale).</summary>
        private TrafficVehicle TrySpawnDetached(int path, float along)
        {
            var kind = PickKind();
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
                Passengers = InitialPassengers(kind),
            };
            _vehicles.Add(v);
            return v;
        }
    }
}
