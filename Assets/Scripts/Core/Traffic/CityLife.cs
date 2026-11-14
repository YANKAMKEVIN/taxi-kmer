using System;
using System.Collections.Generic;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Traffic
{
    /// <summary>
    /// La ville qui vit autour du taxi (jalon 2) : relie le trafic, les piétons et les clients au bord de la route.
    /// Place les clients sur le trottoir de la chaussée de leur sens (sur une double voie, la bonne chaussée),
    /// et laisse les taxis concurrents prendre les clients qui attendent.
    /// </summary>
    public sealed class CityLife
    {
        private readonly float[] _routeXz;
        private readonly float[] _routeCum;
        private readonly Street _street;
        private readonly float _clientCurbMeters;
        private readonly Dictionary<RoadsideClient, ClientPlace> _places = new Dictionary<RoadsideClient, ClientPlace>();
        private readonly List<ClientSpot> _spots = new List<ClientSpot>();
        private readonly List<RoadsideClient> _gone = new List<RoadsideClient>();

        public TrafficSim Traffic { get; }
        public PedestrianSim Pedestrians { get; }

        /// <summary>Position d'un client sur le trottoir de la chaussée de son sens.</summary>
        public struct ClientPlace
        {
            public float X;
            public float Z;
            /// <summary>Direction où il regarde (vers les taxis qui arrivent dans son sens).</summary>
            public float FaceX;
            public float FaceZ;
            /// <summary>Distance le long de la trajectoire du trafic de son sens.</summary>
            public float PathAlong;
        }

        /// <param name="clientCurbMeters">Distance des clients à l'axe de leur chaussée, à droite de leur sens (m).</param>
        public CityLife(CityConfig config, WorldData world, Street street, int seed, float clientCurbMeters = 5f)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (world == null) throw new ArgumentNullException(nameof(world));
            _street = street;
            _clientCurbMeters = clientCurbMeters;
            _routeXz = world.route.xz;
            _routeCum = WorldGeometry.CumulativeLengths(_routeXz);

            var forward = HasPath(world.traffic?.forward) ? TrafficPath.From(world.traffic.forward) : new TrafficPath(_routeXz);
            var backward = HasPath(world.traffic?.backward) ? TrafficPath.From(world.traffic.backward) : new TrafficPath(Reverse(_routeXz));
            Traffic = new TrafficSim(config.vehicles, forward, backward, seed);
            Pedestrians = new PedestrianSim(config.pedestrians, forward, backward, seed + 1);
        }

        public void Populate(float hour, PlayerCar player)
        {
            Traffic.Populate(hour, player);
            Pedestrians.Populate(hour, player);
        }

        /// <summary>Un pas : piétons, trafic, puis clients pris par les taxis concurrents (retirés de <see cref="Street"/>).</summary>
        public void Update(float dt, float hour, PlayerCar player)
        {
            _spots.Clear();
            if (_street != null && !_street.DayFinished)
                foreach (var c in _street.Waiting)
                    _spots.Add(new ClientSpot
                    {
                        Key = c,
                        PathIndex = PathIndexFor(c),
                        Along = Place(c).PathAlong,
                        AcceptedByPlayer = c.State == RoadsideState.Accepted,
                    });

            Pedestrians.Update(dt, hour, player, Traffic.Vehicles);
            Traffic.Update(dt, hour, player, _spots, Pedestrians.OnRoad);

            if (_street != null)
                foreach (var taken in Traffic.TakenClients)
                    if (taken.Key is RoadsideClient c) _street.TakeByOther(c, taken.ByMoto);

            // Oublie les places des clients partis.
            _gone.Clear();
            foreach (var kv in _places)
                if (_street == null || !Contains(_street.Waiting, kv.Key)) _gone.Add(kv.Key);
            foreach (var c in _gone) _places.Remove(c);
        }

        /// <summary>Où se tient ce client (calculé une fois, puis gardé).</summary>
        public ClientPlace Place(RoadsideClient c)
        {
            if (_places.TryGetValue(c, out var place)) return place;
            WorldGeometry.SampleAlong(_routeXz, _routeCum, c.Along, out var rx, out var rz, out _, out _);
            var path = Traffic.Paths[PathIndexFor(c)];
            path.Project(rx, rz, out var along, out _);
            path.Sample(along, _clientCurbMeters, out var x, out var z, out var dx, out var dz);
            place = new ClientPlace { X = x, Z = z, FaceX = -dx, FaceZ = -dz, PathAlong = along };
            _places[c] = place;
            return place;
        }

        private static int PathIndexFor(RoadsideClient c) => c.Direction >= 0 ? 0 : 1;

        private static bool Contains(IReadOnlyList<RoadsideClient> list, RoadsideClient c)
        {
            foreach (var x in list) if (x == c) return true;
            return false;
        }

        private static bool HasPath(TrafficPathData p) => p != null && p.xz != null && p.xz.Length >= 4;

        internal static float[] Reverse(float[] xz)
        {
            var n = xz.Length / 2;
            var r = new float[n * 2];
            for (var i = 0; i < n; i++)
            {
                r[i * 2] = xz[(n - 1 - i) * 2];
                r[i * 2 + 1] = xz[(n - 1 - i) * 2 + 1];
            }
            return r;
        }
    }
}
