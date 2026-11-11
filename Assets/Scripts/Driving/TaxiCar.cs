using System;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.World;
using TaxiKmer.World;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Physique du taxi (WheelCollider, traction avant) : applique les consignes de <see cref="DriveLogic"/>,
    /// ajoute antiroulis et appui, et remet le taxi sur la route s'il se retourne.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TaxiCar : MonoBehaviour
    {
        [Serializable]
        public struct Wheel
        {
            public WheelCollider collider;
            public Transform visual;
            public bool front;
            public bool left;
        }

        public static readonly Quaternion WheelVisualRotation = Quaternion.Euler(0f, 0f, 90f);

        public Wheel[] wheels = Array.Empty<Wheel>();
        [Tooltip("Monde OSM : sert à placer le taxi au départ et à le remettre sur la route.")]
        public WorldBuilder world;
        [Tooltip("Distance de départ le long de l'itinéraire (m) ; 0 = dispensaire de Messassi.")]
        public float startDistance = 0f;
        [Tooltip("Décalage vers la droite de l'axe de la route (m) : on roule à droite au Cameroun.")]
        public float laneOffset = 1.8f;

        /// <summary>Commandes du joueur, posées chaque image par <see cref="DriverControls"/> (ou par un test).</summary>
        public DriveInput Controls { get; set; }
        public DriveCommand Command { get; private set; }
        public float ForwardSpeedMs { get; private set; }
        public float SpeedKmh => Mathf.Abs(ForwardSpeedMs) * 3.6f;
        public DriveConfig Config { get; private set; }

        private Rigidbody _rb;
        private DriveLogic _logic;
        private float _flippedFor;

        private void Awake()
        {
            Config = DriveData.Load();
            _rb = GetComponent<Rigidbody>();
            _logic = new DriveLogic(Config);
        }

        private void Start()
        {
            if (world != null && world.Data != null && world.Data.route.xz.Length >= 4) PlaceOnRoute(startDistance);
        }

        /// <summary>Pose le taxi à l'arrêt sur la voie de droite, à la distance d de l'itinéraire, tourné dans son sens.</summary>
        public void PlaceOnRoute(float d)
        {
            var xz = world.Data.route.xz;
            var cum = WorldGeometry.CumulativeLengths(xz);
            WorldGeometry.SampleAlong(xz, cum, d, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var pos = new Vector3(x, 0.15f, z) + right * laneOffset;
            var rot = Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up);
            if (_rb == null) _rb = GetComponent<Rigidbody>(); // aussi appelé hors jeu (captures de l'éditeur)
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = pos;
            _rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            _flippedFor = 0f;
        }

        /// <summary>Remet le taxi sur l'itinéraire, au point le plus proche.</summary>
        public void ResetToRoute()
        {
            if (world == null || world.Data == null) return;
            var xz = world.Data.route.xz;
            var cum = WorldGeometry.CumulativeLengths(xz);
            var p = transform.position;
            PlaceOnRoute(WorldGeometry.DistanceAlong(xz, cum, p.x, p.z));
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            ForwardSpeedMs = Vector3.Dot(_rb.linearVelocity, transform.forward);
            var cmd = _logic.Step(Controls, ForwardSpeedMs, dt);
            Command = cmd;
            var car = Config.car;

            foreach (var w in wheels)
            {
                var wc = w.collider;
                if (w.front)
                {
                    wc.steerAngle = cmd.SteerAngleDeg;
                    wc.motorTorque = cmd.Throttle * car.motorTorqueNm * 0.5f;
                }
                var brake = cmd.Brake * car.brakeTorqueNm * (w.front ? 0.6f : 0.4f);
                if (cmd.Throttle == 0f && cmd.Brake == 0f) brake = car.coastBrakeTorqueNm;
                wc.brakeTorque = brake;
            }

            AntiRoll(0, 1);
            AntiRoll(2, 3);
            _rb.AddForce(-transform.up * car.downforce * _rb.linearVelocity.sqrMagnitude);
            CheckFlipped(dt);
        }

        private void AntiRoll(int a, int b)
        {
            if (wheels.Length <= Math.Max(a, b)) return;
            var wa = wheels[a].collider;
            var wb = wheels[b].collider;
            var travelA = 1f;
            var travelB = 1f;
            var groundedA = wa.GetGroundHit(out var hitA);
            var groundedB = wb.GetGroundHit(out var hitB);
            if (groundedA) travelA = (-wa.transform.InverseTransformPoint(hitA.point).y - wa.radius) / wa.suspensionDistance;
            if (groundedB) travelB = (-wb.transform.InverseTransformPoint(hitB.point).y - wb.radius) / wb.suspensionDistance;
            var force = (travelA - travelB) * Config.car.antiRoll;
            if (groundedA) _rb.AddForceAtPosition(wa.transform.up * -force, wa.transform.position);
            if (groundedB) _rb.AddForceAtPosition(wb.transform.up * force, wb.transform.position);
        }

        private void CheckFlipped(float dt)
        {
            var upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.3f && SpeedKmh < 5f;
            var fell = transform.position.y < -5f;
            _flippedFor = upsideDown ? _flippedFor + dt : 0f;
            if (fell || _flippedFor > Config.car.flippedResetSeconds) ResetToRoute();
        }

        private void LateUpdate()
        {
            foreach (var w in wheels)
            {
                if (w.visual == null) continue;
                w.collider.GetWorldPose(out var pos, out var rot);
                w.visual.SetPositionAndRotation(pos, rot * WheelVisualRotation);
            }
        }
    }
}
