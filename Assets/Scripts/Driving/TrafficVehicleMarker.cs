using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>Repère posé sur chaque véhicule du trafic affiché (voir TrafficView) : son identifiant dans la simulation.</summary>
    public sealed class TrafficVehicleMarker : MonoBehaviour
    {
        public int Id;
    }
}
