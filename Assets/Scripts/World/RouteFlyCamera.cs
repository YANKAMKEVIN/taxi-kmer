using TaxiKmer.Core.World;
using UnityEngine;

namespace TaxiKmer.World
{
    /// <summary>
    /// Caméra provisoire (tâche 1.2) : parcourt l'itinéraire du tronçon à vitesse de taxi pour vérifier le monde,
    /// en attendant la conduite (tâche 1.3). Fait des allers-retours.
    /// </summary>
    public sealed class RouteFlyCamera : MonoBehaviour
    {
        public WorldBuilder world;
        [Tooltip("km/h")] public float speedKmh = 40f;
        public float height = 5f;
        public float behind = 12f;
        public float lookAhead = 25f;

        private float[] _xz;
        private float[] _cum;
        private float _distance;
        private int _direction = 1;

        private void Start()
        {
            if (world == null || world.Data == null) { enabled = false; return; }
            _xz = world.Data.route.xz;
            _cum = WorldGeometry.CumulativeLengths(_xz);
            _distance = behind;
        }

        private void LateUpdate()
        {
            var total = _cum[_cum.Length - 1];
            _distance += _direction * speedKmh / 3.6f * Time.deltaTime;
            if (_distance > total) { _distance = total; _direction = -1; }
            else if (_distance < 0) { _distance = 0; _direction = 1; }

            WorldGeometry.SampleAlong(_xz, _cum, _distance - _direction * behind, out var cx, out var cz, out _, out _);
            WorldGeometry.SampleAlong(_xz, _cum, _distance + _direction * lookAhead, out var tx, out var tz, out _, out _);
            var target = new Vector3(tx, 1.5f, tz);
            var pos = new Vector3(cx, height, cz);
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-4f * Time.deltaTime));
            var look = Quaternion.LookRotation(target - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
