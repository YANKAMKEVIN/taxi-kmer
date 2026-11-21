using System;

namespace TaxiKmer.Core.Traffic
{
    public enum LightColor
    {
        Green,
        Amber,
        Red,
    }

    /// <summary>
    /// Feux tricolores (jalon 2, décision de Kevin : il y en a sur le tronçon). Cycle à deux temps : l'axe (les deux
    /// sens ensemble) puis les rues latérales. Le trafic de l'axe s'arrête à la ligne d'arrêt au rouge, et à l'orange
    /// s'il peut s'arrêter sans freiner fort ; il ne double pas devant un feu rouge ; les rues latérales proches d'un
    /// feu n'entrent que quand l'axe est au rouge. Certains mototaxis grillent le feu.
    /// </summary>
    public sealed partial class TrafficSim
    {
        /// <summary>Temps de simulation écoulé (s), qui règle les feux.</summary>
        public float Time { get; private set; }

        /// <summary>Couleur du feu <paramref name="lightId"/> pour l'axe, à l'instant présent.</summary>
        public LightColor LightFor(int lightId) => LightAt(lightId, Time);

        /// <summary>Couleur pour l'axe à l'instant <paramref name="time"/> ; chaque feu est décalé pour ne pas tous changer ensemble.</summary>
        public LightColor LightAt(int lightId, float time)
        {
            var g = Math.Max(0f, _config.lightGreenSeconds);
            var a = Math.Max(0f, _config.lightAmberSeconds);
            var r = Math.Max(0f, _config.lightRedSeconds);
            var cycle = g + a + r;
            if (cycle <= 0f) return LightColor.Green;
            var t = (time + lightId * 11f) % cycle;
            if (t < 0f) t += cycle;
            if (t < g) return LightColor.Green;
            return t < g + a ? LightColor.Amber : LightColor.Red;
        }

        /// <summary>
        /// Ligne d'arrêt du feu : <see cref="TrafficConfig.lightStopLineMeters"/> avant le feu, ou avant la rue latérale
        /// du carrefour si elle débouche plus tôt (la file d'attente ne doit pas boucher la rue).
        /// </summary>
        public float StopLine(int pathIndex, PathLight light)
        {
            var start = light.Along;
            foreach (var j in _paths[pathIndex].Junctions)
                if (Math.Abs(j.Along - light.Along) <= _config.lightJunctionRadiusMeters) start = Math.Min(start, j.Along);
            return start - _config.lightStopLineMeters;
        }

        /// <summary>Prochain feu devant le véhicule sur sa trajectoire, à moins de <paramref name="range"/> m (null sinon).</summary>
        private PathLight NextLight(TrafficVehicle v, float range)
        {
            foreach (var l in _paths[v.PathIndex].Lights)
            {
                var ahead = StopLine(v.PathIndex, l) - v.Along;
                if (ahead < -1f) continue; // déjà passé la ligne : on dégage le carrefour
                return ahead <= range ? l : null;
            }
            return null;
        }

        /// <summary>
        /// Le véhicule doit s'arrêter au prochain feu : rouge, ou orange s'il a la place de s'arrêter. Rend la distance
        /// jusqu'à la ligne d'arrêt (depuis l'avant du véhicule).
        /// </summary>
        private bool MustStopAtLight(TrafficVehicle v, out float toLine)
        {
            toLine = float.MaxValue;
            var light = NextLight(v, 120f);
            if (light == null) return false;
            var color = LightFor(light.Id);
            if (color == LightColor.Green) return false;
            toLine = StopLine(v.PathIndex, light) - v.Along - v.Length / 2f;

            if (v.Kind == VehicleKind.Moto)
            {
                if (v.Considered.Add(light) && _random.NextDouble() < _config.motoRunsRedChance) v.RunsLight = light;
                if (v.RunsLight == light) return false;
            }
            if (color == LightColor.Amber)
            {
                var k = _config.Kind(v.Kind);
                var stopping = v.Speed * v.Speed / (2f * k.comfortDecel);
                if (toLine < stopping) return false; // trop près pour s'arrêter : il passe
            }
            return toLine > -0.5f;
        }

        /// <summary>Un feu rouge ou orange devant, à moins de … m : on ne double pas.</summary>
        private bool LightBlocksOvertake(TrafficVehicle v)
        {
            var light = NextLight(v, 70f);
            return light != null && LightFor(light.Id) != LightColor.Green;
        }

        /// <summary>Une rue latérale près d'un feu n'entre sur l'axe que quand l'axe est au rouge.</summary>
        private bool JunctionHeldByLight(TrafficVehicle v)
        {
            foreach (var l in _paths[v.PathIndex].Lights)
                if (Math.Abs(l.Along - v.Junction.Along) <= _config.lightJunctionRadiusMeters && LightFor(l.Id) != LightColor.Red)
                    return true;
            return false;
        }
    }
}
