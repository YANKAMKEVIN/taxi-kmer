using System;
using System.Collections.Generic;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Pilote automatique (parcours de test et banc de performance sur téléphone, jamais en jeu normal) : il joue comme
    /// un joueur, par les mêmes touches (<see cref="BotInput"/>). Il conduit le long de l'itinéraire avec la vraie physique
    /// (aller puis retour), évite véhicules et piétons, klaxonne les clients qui appellent, s'arrête, les fait monter et
    /// descendre, fait le plein, répond à la police, aux accrochages, aux pannes, au marchandage et aux passagers, et
    /// change de station de radio. Il tient un journal et signale chaque situation nouvelle (<see cref="Situation"/>).
    /// </summary>
    public sealed class Autopilot : MonoBehaviour
    {
        public float cruiseKmh = 30f;
        public float laneOffset = 1.8f;

        /// <summary>Une situation vue pour la première fois (« police », « dialogue_… », « action_Faire »…).</summary>
        public event Action<string> Situation;

        public List<string> Journal { get; } = new List<string>();
        public HashSet<string> Seen { get; } = new HashSet<string>();
        public int Boards { get; private set; }
        public int Drops { get; private set; }

        private StreetView _view;
        private TaxiCar _taxi;
        private DriverControls _controls;
        private float[] _xz, _cum;
        private int _direction = 1;
        private float _nextReply, _nextRadio = 20f, _honkAt;
        private float _blockedFor, _blockedAt = -1f, _ignoreObstaclesUntil, _stuckFor, _lastAlong;
        private int _blockedTimes, _lastPax;
        private string _lastObstacle = "", _lastPendingKey = "";

        private void Start()
        {
            _view = FindFirstObjectByType<StreetView>();
            _taxi = FindFirstObjectByType<TaxiCar>();
            _controls = FindFirstObjectByType<DriverControls>();
            _xz = _taxi.world.Data.route.xz;
            _cum = WorldGeometry.CumulativeLengths(_xz);
        }

        private void OnDisable()
        {
            BotInput.Clear();
        }

        private void Update()
        {
            if (_view == null || _view.Street == null) return;
            var street = _view.Street;
            if (street.DayFinished)
            {
                Hold(false, false, false, false);
                return;
            }
            if (_view.MenuFrozen)
            {
                BotInput.Press(KeyAction.Action); // « Commencer la journée »
                return;
            }

            var s = _view.TaxiOnRouteNow();
            var pending = _view.Pending;
            LogPending(s, pending, street);

            // 1) Les choix qui attendent passent avant la conduite.
            if (pending.PoliceControl) Press(KeyAction.Police1, "police");
            else if (pending.Crash) Press(KeyAction.CrashYes, "accrochage");
            else if (pending.Haggle) Press(KeyAction.HaggleAccept, "marchandage");
            else if (pending.Refusal) Press(KeyAction.RefusalLetGo, "refus_de_payer");
            else if (pending.DryTank) Press(KeyAction.PushTaxi, "panne_seche");
            else if (pending.FlatTyre) Press(KeyAction.SpareWheel, "crevaison");
            else if (pending.BrokenDown) Press(KeyAction.CallMechanic, "panne");
            if (_view.TalkView != null && _view.TalkView.Speaking && Time.time > _nextReply)
            {
                Notice("dialogue_" + _view.TalkView.Director.Current.Line.id);
                BotInput.Press(KeyAction.Reply1);
                _nextReply = Time.time + 3f;
            }
            if (_view.RadioView != null && Time.time > _nextRadio)
            {
                BotInput.Press(KeyAction.Radio);
                _nextRadio = Time.time + 60f;
                Notice("radio");
            }

            // 2) Où s'arrêter : destination d'un passager, client accepté, station si le réservoir baisse.
            var stopAt = float.NaN;
            foreach (var p in street.Shift.Cabin.Passengers)
            {
                var d = street.FindStop(p.DestinationStopId).distanceAlongRouteMeters;
                if ((d - s.Along) * _direction > -5f) stopAt = Nearest(stopAt, d, s.Along);
            }
            foreach (var c in street.Waiting)
            {
                if (c.State == RoadsideState.Accepted && (c.Along - s.Along) * _direction > -5f && c.Direction == _direction)
                    stopAt = Nearest(stopAt, c.Along, s.Along);
                else if (c.Calling && Mathf.Abs(c.Along - s.Along) < 70f && c.Direction == _direction && _honkAt < Time.time)
                {
                    BotInput.Press(KeyAction.Horn);
                    _honkAt = Time.time + 1.5f;
                }
            }
            var tank = street.Shift.Tank;
            if (tank != null && tank.Fraction < 0.35f)
                foreach (var st in tank.Config.stations)
                    if ((st.distanceAlongRouteMeters - s.Along) * _direction > -5f) stopAt = Nearest(stopAt, st.distanceAlongRouteMeters, s.Along);

            // 3) Arrêté au bon endroit : le bouton contextuel.
            if (_controls.ActionVisible && _taxi.SpeedKmh < 1f && !pending.Any)
            {
                var label = _controls.ActionLabel;
                Press(KeyAction.Action, "action_" + label.Split(' ')[0]);
                if (label.StartsWith("Faire descendre")) Drops++;
                if (label.Contains("monter")) Boards++;
            }

            // 4) Conduite : direction vers un point devant, vitesse vers la cible.
            var target = cruiseKmh;
            if (!float.IsNaN(stopAt))
            {
                var dist = Mathf.Abs(stopAt - s.Along);
                target = dist < 8f ? 0f : Mathf.Min(cruiseKmh, 3f * Mathf.Sqrt(dist - 8f) + 2f);
            }
            if (_controls.Locked || pending.Any) target = 0f;
            var obstacle = Time.time < _ignoreObstaclesUntil ? float.PositiveInfinity : ObstacleAhead();
            _blockedFor = obstacle < 25f && _taxi.SpeedKmh < 1f ? _blockedFor + Time.deltaTime : 0f;
            var length = _view.RouteLength;
            if (_blockedFor > 10f)
            {
                _blockedTimes = Mathf.Abs(s.Along - _blockedAt) < 3f ? _blockedTimes + 1 : 1;
                _blockedAt = s.Along;
                if (_blockedTimes >= 2)
                {
                    // Coincé pour de bon (un véhicule du trafic collé au taxi, le muret) : le pilote « contourne » en
                    // reposant le taxi un peu plus loin. Noté au journal : c'est peut-être un défaut du trafic.
                    Log(street, $"à {s.Along:0} m : coincé ({_lastObstacle}), taxi reposé 20 m plus loin");
                    _taxi.PlaceOnRoute(Mathf.Clamp(s.Along + _direction * 20f, 50f, length - 50f), _direction);
                    _blockedTimes = 0;
                }
                else
                {
                    Log(street, $"à {s.Along:0} m : obstacle devant depuis 10 s ({_lastObstacle}), on avance quand même");
                    _ignoreObstaclesUntil = Time.time + 3f;
                }
                _blockedFor = 0f;
            }
            if (Mathf.Abs(s.Lateral) > 7f && _taxi.SpeedKmh < 2f && _stuckFor > 3f) BotInput.Press(KeyAction.ResetCar); // sorti de la route
            if (obstacle < 25f) target = Mathf.Min(target, Mathf.Max(0f, (obstacle - 5f) * 1.5f));
            Steer(s.Along, target);

            // 5) Bout de l'itinéraire : demi-tour (le taxi est reposé dans l'autre sens).
            if ((_direction > 0 && s.Along > length - 40f) || (_direction < 0 && s.Along < 40f))
                if (street.Shift.Cabin.PassengerCount == 0 || _stuckFor > 20f)
                {
                    _direction = -_direction;
                    _taxi.PlaceOnRoute(Mathf.Clamp(s.Along, 50f, length - 50f), _direction);
                    Log(street, $"demi-tour → sens {_direction}");
                }

            // Bloqué : on remet le taxi sur la route, comme le bouton du joueur.
            _stuckFor = Mathf.Abs(s.Along - _lastAlong) < 0.05f && (target > 0f || Mathf.Abs(s.Lateral) > 7f) ? _stuckFor + Time.deltaTime : 0f;
            _lastAlong = s.Along;
            if (_stuckFor > 8f)
            {
                BotInput.Press(KeyAction.ResetCar);
                Log(street, $"bloqué à {s.Along:0} m : taxi remis sur la route");
                _stuckFor = 0f;
            }
            if (street.Shift.Cabin.PassengerCount != _lastPax)
            {
                Log(street, $"à {s.Along:0} m : {street.Shift.Cabin.PassengerCount} passager(s), recette {street.Shift.Ledger.Revenue}");
                _lastPax = street.Shift.Cabin.PassengerCount;
            }
        }

        /// <summary>Résumé d'une ligne : montées, descentes, recette, distance, situations vues.</summary>
        public string Summary()
        {
            var street = _view != null ? _view.Street : null;
            if (street == null) return "";
            var seen = new List<string>(Seen);
            seen.Sort(StringComparer.Ordinal);
            return $"Montées {Boards}, descentes {Drops}, recette {street.Shift.Ledger.Revenue} FCFA, " +
                   $"{street.Shift.Ledger.MetersDriven / 1000f:0.0} km, situations vues : {string.Join(", ", seen)}";
        }

        private void Log(Street street, string text) => Journal.Add($"{street.Shift.Clock.Display} {text}");

        private void Notice(string situation)
        {
            if (Seen.Add(situation)) Situation?.Invoke(situation);
        }

        private void Press(KeyAction action, string situation)
        {
            BotInput.Press(action);
            Notice(situation);
        }

        private static float Nearest(float current, float candidate, float along) =>
            float.IsNaN(current) || Mathf.Abs(candidate - along) < Mathf.Abs(current - along) ? candidate : current;

        private static void Hold(bool left, bool right, bool accelerate, bool brake)
        {
            BotInput.Hold(KeyAction.Left, left);
            BotInput.Hold(KeyAction.Right, right);
            BotInput.Hold(KeyAction.Accelerate, accelerate);
            BotInput.Hold(KeyAction.Brake, brake);
        }

        /// <summary>Distance (m) du premier obstacle dans la voie du taxi (véhicule, piéton, mur), l'infini sinon.</summary>
        private float ObstacleAhead()
        {
            var t = _taxi.transform;
            var f = t.forward;
            f.y = 0f;
            f.Normalize();
            var origin = t.position + Vector3.up * 1.2f + f * 2.6f;
            var hits = Physics.SphereCastAll(origin, 1.5f, f, 30f, ~0, QueryTriggerInteraction.Collide);
            var best = float.PositiveInfinity;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(t) || h.normal.y > 0.7f) continue; // le taxi lui-même, le sol
                if (h.distance <= 0f && h.collider.bounds.size.magnitude > 15f) continue; // sol, trottoir, bâtiment déjà touché
                var pedestrian = h.collider.GetComponentInParent<PedestrianMarker>() != null;
                if (h.collider.isTrigger && !pedestrian) continue; // seuls les piétons comptent parmi les déclencheurs
                var local = t.InverseTransformPoint(h.point == Vector3.zero ? h.collider.bounds.center : h.point);
                if (Mathf.Abs(local.x) > (pedestrian ? 3f : 1.4f) || local.z < 0f) continue; // hors de la voie (marge pour un piéton)
                if (h.distance < best)
                {
                    best = h.distance;
                    _lastObstacle = $"{h.collider.name} ({h.collider.transform.root.name}) à {h.distance:0} m";
                }
            }
            return best;
        }

        private void Steer(float along, float targetKmh)
        {
            var ahead = along + _direction * Mathf.Max(12f, _taxi.SpeedKmh * 0.6f);
            WorldGeometry.SampleAlong(_xz, _cum, ahead, out var x, out var z, out var dx, out var dz);
            if (_direction < 0) { dx = -dx; dz = -dz; }
            var aim = new Vector3(x + dz * laneOffset, 0f, z - dx * laneOffset);
            var to = aim - _taxi.transform.position;
            to.y = 0f;
            var f = _taxi.transform.forward;
            f.y = 0f;
            var angle = Vector3.SignedAngle(f, to, Vector3.up);
            var speed = _taxi.SpeedKmh;
            // Commande tout ou rien : par petits coups (une image sur deux) quand l'écart est faible, pour ne pas zigzaguer.
            var pulse = Mathf.Abs(angle) > 6f || Time.frameCount % 2 == 0;
            Hold(angle < -1.5f && pulse, angle > 1.5f && pulse,
                speed < targetKmh - 2f && targetKmh > 0f, speed > targetKmh + 3f || (targetKmh == 0f && speed > 0.5f));
        }

        private void LogPending(TaxiOnRoute s, PendingChoices p, Street street)
        {
            var key = $"{p.PoliceControl}{p.Crash}{p.Haggle}{p.Refusal}{p.DryTank}{p.FlatTyre}{p.BrokenDown}";
            if (key == _lastPendingKey) return;
            _lastPendingKey = key;
            if (!p.Any) return;
            Log(street, $"à {s.Along:0} m : police={p.PoliceControl} accrochage={p.Crash} marchandage={p.Haggle} " +
                        $"refus={p.Refusal} sec={p.DryTank} crevé={p.FlatTyre} panne={p.BrokenDown}");
        }
    }
}
