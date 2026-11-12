using System;
using System.Collections.Generic;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Réglages des clients au bord de la route (données : Assets/Resources/Data/street.json).</summary>
    [Serializable]
    public class StreetConfig
    {
        /// <summary>Clients qui attendent au plus à chaque lieu (toutes directions).</summary>
        public int maxWaitingPerStop = 3;
        /// <summary>Un nouveau client arrive à un lieu incomplet toutes les … secondes réelles.</summary>
        public float spawnIntervalSeconds = 20f;
        /// <summary>Écart entre deux clients qui attendent au même lieu (m, le long de la route).</summary>
        public float clientSpacingMeters = 4f;
        /// <summary>Un client annonce sa destination quand le taxi arrive vers lui à moins de … m.</summary>
        public float announceDistanceMeters = 45f;
        /// <summary>Le taxi doit s'arrêter à moins de … m du client accepté pour le faire monter.</summary>
        public float boardDistanceMeters = 12f;
        /// <summary>Si le taxi dépasse le client accepté de plus de … m, le client n'est plus accepté.</summary>
        public float missedDistanceMeters = 25f;
        /// <summary>Zone de dépose autour d'un lieu (m, le long de la route).</summary>
        public float stopRadiusMeters = 30f;
        /// <summary>Au-delà de cet écart à l'itinéraire (m), le taxi n'est plus « sur la route » des clients.</summary>
        public float maxLateralMeters = 15f;
        /// <summary>En dessous de cette vitesse (km/h), le taxi est arrêté : montée et descente possibles.</summary>
        public float stoppedKmh = 3f;
    }

    /// <summary>Position du taxi par rapport à l'itinéraire.</summary>
    public struct TaxiOnRoute
    {
        /// <summary>Distance le long de l'itinéraire (m).</summary>
        public float Along;
        /// <summary>+1 en direction d'Étoudi (fin de l'itinéraire), -1 vers le dispensaire.</summary>
        public int Direction;
        /// <summary>Écart à l'axe de l'itinéraire (m).</summary>
        public float Lateral;
        public float SpeedKmh;
    }

    public enum RoadsideState
    {
        Waiting,
        Accepted,
    }

    /// <summary>Un client qui attend au bord de la route.</summary>
    public sealed class RoadsideClient
    {
        public Client Client { get; }
        public Stop Pickup { get; }
        /// <summary>Position le long de l'itinéraire (m).</summary>
        public float Along { get; }
        /// <summary>Sens où il veut aller : +1 vers Étoudi, -1 vers le dispensaire. Il attend du côté droit de ce sens.</summary>
        public int Direction { get; }
        internal int Slot { get; }
        public RoadsideState State { get; internal set; }
        /// <summary>Il annonce sa destination et son prix au taxi qui arrive (« Étoudi, 500 ! »).</summary>
        public bool Calling { get; internal set; }

        internal RoadsideClient(Client client, Stop pickup, float along, int direction, int slot)
        {
            Client = client;
            Pickup = pickup;
            Along = along;
            Direction = direction;
            Slot = slot;
        }
    }

    public enum HonkResult
    {
        /// <summary>Personne n'annonce sa destination devant le taxi.</summary>
        NobodyCalling,
        Accepted,
        /// <summary>Accepté, mais il faudra faire changer un passager de banquette pour le faire monter.</summary>
        AcceptedNeedsMove,
        /// <summary>Il ne rentre pas, même en faisant changer quelqu'un de place : il reste au bord de la route.</summary>
        NoRoom,
    }

    public enum StreetActionKind
    {
        None,
        Board,
        Drop,
        /// <summary>Après l'heure de fin, avec des passagers encore à bord : terminer quand même (ils ne paient pas).</summary>
        EndDay,
    }

    /// <summary>Action proposée par le bouton contextuel.</summary>
    public struct StreetAction
    {
        public StreetActionKind Kind;
        /// <summary>Board : le client accepté à faire monter.</summary>
        public RoadsideClient Client;
        /// <summary>Board : passager à faire changer de banquette d'abord (null si inutile).</summary>
        public Client MoveFirst;
        /// <summary>Drop : le lieu où l'on est.</summary>
        public Stop Stop;
        /// <summary>Drop : nombre de passagers qui descendent ici.</summary>
        public int Count;
    }

    public struct StreetOutcome
    {
        public StreetActionKind Kind;
        public BoardResult Board;
        public Client Boarded;
        public Client Moved;
        public int Paid;
        public List<Client> Dropped;
    }

    /// <summary>
    /// Les clients au bord de la route le long de l'itinéraire (tâche 1.5) : ils attendent aux lieux à clients,
    /// annoncent destination et prix au taxi qui arrive dans leur sens, sont acceptés d'un coup de klaxon,
    /// montent quand le taxi s'arrête près d'eux, et descendent (en payant) quand il s'arrête à leur destination.
    /// </summary>
    public sealed class Street
    {
        private readonly StreetConfig _config;
        private readonly StopsConfig _stops;
        private readonly ClientFactory _factory;
        private readonly Shift _shift;
        private readonly float _routeLength;
        private readonly List<RoadsideClient> _waiting = new List<RoadsideClient>();
        private readonly float[] _spawnTimers;

        public IReadOnlyList<RoadsideClient> Waiting => _waiting;
        public Shift Shift => _shift;

        /// <summary>
        /// La journée est terminée et le bilan est arrêté : à l'heure de fin dès que le taxi est vide,
        /// ou quand le joueur choisit de terminer avec des passagers à bord (<see cref="StreetActionKind.EndDay"/>).
        /// </summary>
        public bool DayFinished => Summary != null;

        /// <summary>Le bilan de la journée, figé quand elle se termine (null avant).</summary>
        public DaySummary Summary { get; private set; }

        public Street(StreetConfig config, StopsConfig stops, ClientFactory factory, Shift shift, float routeLength)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stops = stops ?? throw new ArgumentNullException(nameof(stops));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            _routeLength = routeLength;
            _spawnTimers = new float[stops.stops.Length];
            foreach (var stop in stops.stops)
                while (CountAt(stop) < config.maxWaitingPerStop) Spawn(stop);
        }

        public void Update(float dt, TaxiOnRoute taxi)
        {
            if (DayFinished) return;
            if (_shift.Clock.IsOver && _shift.Cabin.PassengerCount == 0) Finish();
            if (!_shift.Clock.IsOver)
            {
                for (var i = 0; i < _stops.stops.Length; i++)
                {
                    var stop = _stops.stops[i];
                    if (CountAt(stop) >= _config.maxWaitingPerStop) { _spawnTimers[i] = 0f; continue; }
                    _spawnTimers[i] += dt;
                    if (_spawnTimers[i] >= _config.spawnIntervalSeconds)
                    {
                        _spawnTimers[i] = 0f;
                        Spawn(stop);
                    }
                }
            }

            var onRoute = taxi.Lateral <= _config.maxLateralMeters;
            foreach (var c in _waiting)
            {
                var ahead = (c.Along - taxi.Along) * c.Direction;
                if (c.State == RoadsideState.Accepted && (!onRoute || -ahead > _config.missedDistanceMeters))
                    c.State = RoadsideState.Waiting; // le taxi est passé sans s'arrêter
                c.Calling = c.State == RoadsideState.Waiting && onRoute && !_shift.Clock.IsOver
                    && taxi.Direction == c.Direction
                    && ahead > -_config.boardDistanceMeters && ahead < _config.announceDistanceMeters;
            }
        }

        /// <summary>Coup de klaxon : accepte le client le plus proche du taxi qui lui annonce sa destination.</summary>
        public HonkResult Honk(TaxiOnRoute taxi, out RoadsideClient client)
        {
            client = null;
            var best = float.MaxValue;
            foreach (var c in _waiting)
            {
                if (!c.Calling) continue;
                var d = Math.Abs(c.Along - taxi.Along);
                if (d < best) { best = d; client = c; }
            }
            if (client == null) return HonkResult.NobodyCalling;
            var cabin = _shift.Cabin;
            if (cabin.CanBoard(client.Client)) { client.State = RoadsideState.Accepted; return HonkResult.Accepted; }
            if (cabin.FindMoveToFit(client.Client) != null) { client.State = RoadsideState.Accepted; return HonkResult.AcceptedNeedsMove; }
            return HonkResult.NoRoom;
        }

        /// <summary>Ce que propose le bouton contextuel à cet instant (descente prioritaire sur la montée).</summary>
        public StreetAction AvailableAction(TaxiOnRoute taxi)
        {
            var none = new StreetAction { Kind = StreetActionKind.None };
            if (DayFinished) return none;
            if (Math.Abs(taxi.SpeedKmh) > _config.stoppedKmh || taxi.Lateral > _config.maxLateralMeters) return none;

            foreach (var stop in _stops.stops)
            {
                if (Math.Abs(stop.distanceAlongRouteMeters - taxi.Along) > _config.stopRadiusMeters) continue;
                var count = _shift.Cabin.PassengersFor(stop.id).Count;
                if (count > 0) return new StreetAction { Kind = StreetActionKind.Drop, Stop = stop, Count = count };
            }

            if (_shift.Clock.IsOver)
                return _shift.Cabin.PassengerCount > 0
                    ? new StreetAction { Kind = StreetActionKind.EndDay, Count = _shift.Cabin.PassengerCount }
                    : none;

            RoadsideClient nearest = null;
            var best = float.MaxValue;
            foreach (var c in _waiting)
            {
                if (c.State != RoadsideState.Accepted) continue;
                var d = Math.Abs(c.Along - taxi.Along);
                if (d <= _config.boardDistanceMeters && d < best) { best = d; nearest = c; }
            }
            if (nearest == null) return none;
            var move = _shift.Cabin.CanBoard(nearest.Client) ? null : _shift.Cabin.FindMoveToFit(nearest.Client);
            return new StreetAction { Kind = StreetActionKind.Board, Client = nearest, MoveFirst = move };
        }

        /// <summary>Le joueur appuie sur le bouton contextuel.</summary>
        public StreetOutcome Perform(TaxiOnRoute taxi)
        {
            var action = AvailableAction(taxi);
            var outcome = new StreetOutcome { Kind = action.Kind };
            switch (action.Kind)
            {
                case StreetActionKind.Drop:
                    outcome.Dropped = _shift.Cabin.PassengersFor(action.Stop.id);
                    outcome.Paid = _shift.DropAllAt(action.Stop.id);
                    break;
                case StreetActionKind.Board:
                    var c = action.Client;
                    if (action.MoveFirst != null && _shift.MovePassenger(action.MoveFirst.Id)) outcome.Moved = action.MoveFirst;
                    outcome.Board = _shift.Board(c.Client, out _);
                    if (outcome.Board == BoardResult.Boarded)
                    {
                        _waiting.Remove(c);
                        outcome.Boarded = c.Client;
                    }
                    else c.State = RoadsideState.Waiting;
                    break;
                case StreetActionKind.EndDay:
                    Finish();
                    break;
            }
            if (!DayFinished && _shift.Clock.IsOver && _shift.Cabin.PassengerCount == 0) Finish();
            return outcome;
        }

        private void Finish()
        {
            var unpaid = 0;
            foreach (var p in _shift.Cabin.Passengers) unpaid += p.ProposedFare;
            Summary = DaySummary.From(_shift.Ledger, _shift.Cabin.PassengerCount, unpaid);
            foreach (var c in _waiting) c.Calling = false;
        }

        public Stop FindStop(string id) => _factory.FindStop(id);

        private int CountAt(Stop stop)
        {
            var n = 0;
            foreach (var c in _waiting) if (c.Pickup == stop) n++;
            return n;
        }

        private void Spawn(Stop stop)
        {
            var client = _factory.CreateAt(stop.id);
            var dest = _factory.FindStop(client.DestinationStopId);
            var dir = dest.distanceAlongRouteMeters >= stop.distanceAlongRouteMeters ? 1 : -1;
            var slot = 0;
            while (SlotTaken(stop, dir, slot)) slot++;
            var along = stop.distanceAlongRouteMeters + dir * _config.clientSpacingMeters * slot;
            along = Math.Max(0f, Math.Min(_routeLength, along));
            _waiting.Add(new RoadsideClient(client, stop, along, dir, slot));
        }

        private bool SlotTaken(Stop stop, int dir, int slot)
        {
            foreach (var c in _waiting)
                if (c.Pickup == stop && c.Direction == dir && c.Slot == slot) return true;
            return false;
        }
    }
}
