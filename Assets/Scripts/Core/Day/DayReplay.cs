using System;
using System.Collections.Generic;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Day
{
    /// <summary>Un geste du joueur à une image donnée (klaxon, bouton d'action, choix…).</summary>
    [Serializable]
    public class ReplayEventData
    {
        public int frame;
        /// <summary>Voir <see cref="ReplayEvents"/>.</summary>
        public string kind = "";
        public string arg = "";
    }

    public static class ReplayEvents
    {
        public const string Honk = "klaxon";
        public const string Action = "action";
        /// <summary>Choix au contrôle de police (arg = ControlChoice), rejoué par la présentation.</summary>
        public const string Police = "police";
        /// <summary>Réponse à un passager (arg = id de la réponse), rejouée par la présentation.</summary>
        public const string Reply = "reponse";
    }

    /// <summary>
    /// Journée enregistrée (idée n° 37) : graine, et pour chaque image le pas de temps, la position du taxi sur
    /// l'itinéraire et les commandes ; plus les gestes du joueur. Rejouée dans la logique (<see cref="DayReplayer"/>),
    /// elle reproduit à l'identique une journée signalée par Kevin ; la physique ne se rejoue que dans Unity (commandes).
    /// Champs publics simples pour JsonUtility.
    /// </summary>
    [Serializable]
    public class ReplayData
    {
        public int version = 1;
        public int seed;
        public int dayNumber;
        public float[] dt = Array.Empty<float>();
        public float[] along = Array.Empty<float>();
        public float[] lateral = Array.Empty<float>();
        public float[] speedKmh = Array.Empty<float>();
        /// <summary>Sens (+1 / -1) et commandes (<see cref="ReplayInput"/>) dans un seul entier par image.</summary>
        public int[] packed = Array.Empty<int>();
        public ReplayEventData[] events = Array.Empty<ReplayEventData>();

        public int Frames => dt?.Length ?? 0;
    }

    /// <summary>Commandes de conduite en bits (pour rejouer la physique dans Unity).</summary>
    public static class ReplayInput
    {
        /// <summary>Bit « l'horloge de la journée n'a pas avancé à cette image ».</summary>
        public const int ClockStopped = 256;

        public static int Pack(DriveInput i, int direction)
        {
            var bits = 0;
            if (i.Left) bits |= 1;
            if (i.Right) bits |= 2;
            if (i.Accelerate) bits |= 4;
            if (i.Brake) bits |= 8;
            if (i.Horn) bits |= 16;
            if (i.Action) bits |= 32;
            if (i.Handbrake) bits |= 64;
            if (direction < 0) bits |= 128;
            return bits;
        }

        public static DriveInput Unpack(int bits, out int direction)
        {
            direction = (bits & 128) != 0 ? -1 : 1;
            return new DriveInput
            {
                Left = (bits & 1) != 0, Right = (bits & 2) != 0, Accelerate = (bits & 4) != 0, Brake = (bits & 8) != 0,
                Horn = (bits & 16) != 0, Action = (bits & 32) != 0, Handbrake = (bits & 64) != 0,
            };
        }
    }

    /// <summary>Enregistre une journée image par image.</summary>
    public sealed class DayRecorder
    {
        private readonly List<float> _dt = new List<float>(), _along = new List<float>(), _lateral = new List<float>(), _speed = new List<float>();
        private readonly List<int> _packed = new List<int>();
        private readonly List<ReplayEventData> _events = new List<ReplayEventData>();
        private readonly int _seed, _day;

        public DayRecorder(int seed, int dayNumber) { _seed = seed; _day = dayNumber; }

        public int Frames => _dt.Count;

        /// <param name="clockRuns">L'horloge de la journée a avancé à cette image (faux sur l'écran titre, dans un menu…).</param>
        public void Frame(float dt, TaxiOnRoute taxi, DriveInput input, bool clockRuns = true)
        {
            _dt.Add(dt);
            _along.Add(taxi.Along);
            _lateral.Add(taxi.Lateral);
            _speed.Add(taxi.SpeedKmh);
            _packed.Add(ReplayInput.Pack(input, taxi.Direction) | (clockRuns ? 0 : ReplayInput.ClockStopped));
        }

        /// <summary>Geste du joueur, rattaché à la dernière image enregistrée.</summary>
        public void Event(string kind, string arg = "") =>
            _events.Add(new ReplayEventData { frame = Math.Max(0, _dt.Count - 1), kind = kind, arg = arg ?? "" });

        public ReplayData Data() => new ReplayData
        {
            seed = _seed, dayNumber = _day,
            dt = _dt.ToArray(), along = _along.ToArray(), lateral = _lateral.ToArray(), speedKmh = _speed.ToArray(),
            packed = _packed.ToArray(), events = _events.ToArray(),
        };
    }

    /// <summary>Relit une journée enregistrée.</summary>
    public sealed class DayReplayer
    {
        private readonly ReplayData _data;
        private int _eventIndex;

        public int Frame { get; private set; } = -1;
        /// <summary>L'horloge de la journée avançait à l'image courante.</summary>
        public bool ClockRuns => Frame >= 0 && (_data.packed[Frame] & ReplayInput.ClockStopped) == 0;
        public bool Done => Frame + 1 >= _data.Frames;

        public DayReplayer(ReplayData data) => _data = data ?? throw new ArgumentNullException(nameof(data));

        /// <summary>Image suivante : pas de temps, état du taxi, commandes, et gestes du joueur à cette image.</summary>
        public bool Next(out float dt, out TaxiOnRoute taxi, out DriveInput input, List<ReplayEventData> events)
        {
            events?.Clear();
            dt = 0f; taxi = default; input = default;
            if (Done) return false;
            Frame++;
            dt = _data.dt[Frame];
            input = ReplayInput.Unpack(_data.packed[Frame], out var direction);
            taxi = new TaxiOnRoute { Along = _data.along[Frame], Lateral = _data.lateral[Frame], SpeedKmh = _data.speedKmh[Frame], Direction = direction };
            while (_eventIndex < _data.events.Length && _data.events[_eventIndex].frame <= Frame)
            {
                if (_data.events[_eventIndex].frame == Frame) events?.Add(_data.events[_eventIndex]);
                _eventIndex++;
            }
            return true;
        }

        /// <summary>
        /// Rejoue la journée sur une rue neuve créée avec la même graine : chaque image fait avancer l'horloge et la rue,
        /// puis les gestes « klaxon » et « action » ; les autres gestes sont rendus pour que l'appelant les rejoue.
        /// </summary>
        public static List<ReplayEventData> RunStreet(ReplayData data, Street street)
        {
            var others = new List<ReplayEventData>();
            var replayer = new DayReplayer(data);
            var events = new List<ReplayEventData>();
            while (replayer.Next(out var dt, out var taxi, out _, events))
            {
                if (!street.DayFinished && replayer.ClockRuns)
                {
                    street.Shift.Clock.Advance(dt);
                    street.Shift.Drive(Math.Abs(taxi.SpeedKmh) / 3.6f * dt);
                }
                street.Update(dt, taxi);
                foreach (var e in events)
                {
                    if (e.kind == ReplayEvents.Honk) street.Honk(taxi, out _);
                    else if (e.kind == ReplayEvents.Action) street.Perform(taxi);
                    else others.Add(e);
                }
            }
            return others;
        }
    }
}
