using System;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Sur le taxi du joueur : signale quand il touche un piéton (zone de contact des piétons, voir TrafficView),
    /// avec la vitesse du taxi au moment du choc (km/h).
    /// </summary>
    public sealed class TaxiHitDetector : MonoBehaviour
    {
        public TaxiCar taxi;

        /// <summary>(identifiant du piéton, vitesse au choc en km/h).</summary>
        public event Action<int, float> PedestrianHit;

        private void OnTriggerEnter(Collider other)
        {
            var marker = other.GetComponentInParent<PedestrianMarker>();
            if (marker == null || taxi == null) return;
            PedestrianHit?.Invoke(marker.Id, taxi.SpeedKmh);
        }
    }
}
