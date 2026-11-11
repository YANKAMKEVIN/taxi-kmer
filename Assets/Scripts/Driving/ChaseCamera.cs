using TaxiKmer.Core.Driving;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Caméra derrière le taxi. Le joueur la tourne en glissant le doigt (voir <see cref="DriverControls"/>) ;
    /// elle revient derrière le taxi après un moment, et se rapproche si un bâtiment la cache.
    /// </summary>
    public sealed class ChaseCamera : MonoBehaviour
    {
        public TaxiCar target;

        private ChaseCameraLogic _logic;
        private ChaseCameraConfig _config;
        private float _heading;
        private bool _dragging;

        private void Awake()
        {
            _config = DriveData.Load().camera;
            _logic = new ChaseCameraLogic(_config);
        }

        private void Start()
        {
            if (target != null) _heading = target.transform.eulerAngles.y;
            Snap();
        }

        /// <summary>Glissement du doigt pendant cette image, en fractions de la largeur d'écran.</summary>
        public void Drag(float dxScreens, float dyScreens)
        {
            _logic.Drag(dxScreens, dyScreens);
            _dragging = true;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            var dt = Time.deltaTime;
            _logic.Update(dt, _dragging);
            _dragging = false;
            _heading = Mathf.LerpAngle(_heading, target.transform.eulerAngles.y, 1f - Mathf.Exp(-_config.followSharpness * dt));
            Place();
        }

        private void Snap()
        {
            if (target == null) return;
            _heading = target.transform.eulerAngles.y;
            Place();
        }

        private void Place()
        {
            var pivot = target.transform.position + Vector3.up * _config.lookHeight;
            var rot = Quaternion.Euler(_logic.PitchDeg, _heading + _logic.YawOffsetDeg, 0f);
            var dir = rot * Vector3.back;
            var distance = _config.distance;
            // Ne pas passer derrière un mur : on se rapproche du taxi (on ignore les colliders du taxi lui-même).
            var hits = Physics.SphereCastAll(pivot, 0.3f, dir, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.rigidbody != null && h.rigidbody.gameObject == target.gameObject) continue;
                if (h.distance > 0f) distance = Mathf.Min(distance, Mathf.Max(1.5f, h.distance));
            }
            transform.position = pivot + dir * distance;
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }
    }
}
