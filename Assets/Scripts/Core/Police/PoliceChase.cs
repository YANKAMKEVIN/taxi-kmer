using System;

namespace TaxiKmer.Core.Police
{
    /// <summary>
    /// Poursuite quand le chauffeur fuit un contrôle (idée n° 9, option B de Kevin : « fuir = poursuite »). Valeurs par
    /// défaut de Claude, comportement à valider par Kevin ; enabled = false revient au tirage au sort d'avant
    /// (<see cref="PoliceConfig.fleeOutcomes"/>).
    /// </summary>
    [Serializable]
    public class ChaseData
    {
        public bool enabled = true;
        /// <summary>La voiture de police démarre à cette distance derrière le taxi (m).</summary>
        public float startGapMeters = 35f;
        /// <summary>Vitesse de la voiture de police (km/h) ; elle met quelques secondes à l'atteindre.</summary>
        public float policeKmh = 60f;
        public float policeAccelerationSeconds = 4f;
        /// <summary>Rattrapé quand l'écart tombe sous cette distance (m) → fourrière.</summary>
        public float catchGapMeters = 6f;
        /// <summary>Échappé quand l'écart dépasse cette distance (m), ou après maxSeconds sans être rattrapé → recherché.</summary>
        public float escapeGapMeters = 220f;
        public float maxSeconds = 75f;
    }

    public enum ChaseState
    {
        Running,
        Caught,
        Escaped,
    }

    /// <summary>
    /// La voiture de police suit le taxi le long de l'itinéraire (repère <c>along</c>, comme le reste de la logique) :
    /// elle accélère jusqu'à sa vitesse et va vers le taxi, dans un sens ou dans l'autre. Pur, testable sans scène.
    /// </summary>
    public sealed class PoliceChase
    {
        private readonly ChaseData _data;
        private float _speed;

        public float PoliceAlong { get; private set; }
        public float Elapsed { get; private set; }
        public ChaseState State { get; private set; } = ChaseState.Running;
        /// <summary>Écart entre la police et le taxi (m) à la dernière mise à jour.</summary>
        public float Gap { get; private set; }

        /// <param name="direction">Sens de marche du taxi (+1 vers Étoudi, -1 vers Messassi) : la police part derrière.</param>
        public PoliceChase(ChaseData data, float taxiAlong, int direction)
        {
            _data = data ?? new ChaseData();
            PoliceAlong = taxiAlong - Math.Sign(direction == 0 ? 1 : direction) * _data.startGapMeters;
            Gap = _data.startGapMeters;
        }

        public ChaseState Update(float dt, float taxiAlong)
        {
            if (State != ChaseState.Running || dt <= 0f) return State;
            Elapsed += dt;
            var top = _data.policeKmh / 3.6f;
            _speed = Math.Min(top, _speed + top / Math.Max(0.1f, _data.policeAccelerationSeconds) * dt);
            var delta = taxiAlong - PoliceAlong;
            var step = Math.Min(Math.Abs(delta), _speed * dt);
            PoliceAlong += Math.Sign(delta) * step;
            Gap = Math.Abs(taxiAlong - PoliceAlong);
            if (Gap <= _data.catchGapMeters) State = ChaseState.Caught;
            else if (Gap >= _data.escapeGapMeters || Elapsed >= _data.maxSeconds) State = ChaseState.Escaped;
            return State;
        }

        /// <summary>Texte du bandeau pendant la poursuite.</summary>
        public static string Banner(float gap) => $"Poursuite ! Police à {Math.Max(0, (int)Math.Round(gap))} m";

        /// <summary>Texte du bandeau quand le chauffeur est recherché (le prochain poste l'arrête).</summary>
        public const string WantedBanner = "Recherché : le prochain contrôle vous arrêtera";
    }
}
