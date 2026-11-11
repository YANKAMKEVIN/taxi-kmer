using System.Collections;
using NUnit.Framework;
using TaxiKmer.Core.Driving;
using TaxiKmer.Driving;
using UnityEngine;
using UnityEngine.TestTools;

namespace TaxiKmer.PlayModeTests
{
    /// <summary>Le taxi WheelCollider roule, freine, recule et tourne sur un sol plat (physique réelle, temps accéléré).</summary>
    public class TaxiDrivingPlayTests
    {
        private GameObject _ground;
        private TaxiCar _taxi;
        private float _timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 4f;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = new Vector3(200f, 1f, 200f);
            _taxi = TaxiFactory.Build("Taxi test", new TaxiMaterials(), DriveData.Load());
            _taxi.transform.position = new Vector3(0f, 0.2f, 0f);
            yield return Seconds(1f); // laisser la suspension se poser
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _timeScale;
            Object.Destroy(_taxi.gameObject);
            Object.Destroy(_ground);
            yield return null;
        }

        private static IEnumerator Seconds(float s)
        {
            var end = Time.time + s;
            while (Time.time < end) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator SettlesUpright_OnItsWheels()
        {
            Assert.Greater(Vector3.Dot(_taxi.transform.up, Vector3.up), 0.99f);
            Assert.Less(_taxi.SpeedKmh, 1f);
            Assert.That(_taxi.transform.position.y, Is.InRange(-0.05f, 0.3f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Accelerates_Brakes_ThenReverses()
        {
            _taxi.Controls = new DriveInput { Accelerate = true };
            yield return Seconds(6f);
            var speed = _taxi.ForwardSpeedMs * 3.6f;
            Debug.Log($"[TaxiKmer] Vitesse après 6 s d'accélération : {speed:F1} km/h");
            Assert.Greater(speed, 40f, "Démarrage trop mou");
            Assert.LessOrEqual(speed, _taxi.Config.pedals.maxForwardKmh + 3f, "Limiteur");
            Assert.Greater(_taxi.transform.position.z, 20f, "Le taxi avance vers l'avant (+z)");

            _taxi.Controls = new DriveInput { Brake = true };
            var t0 = Time.time;
            while (_taxi.SpeedKmh > 1f && Time.time - t0 < 6f) yield return new WaitForFixedUpdate();
            Debug.Log($"[TaxiKmer] Arrêt en {Time.time - t0:F1} s");
            Assert.Less(Time.time - t0, 4f, "Freinage trop long");

            yield return Seconds(3f); // frein maintenu à l'arrêt → marche arrière
            Assert.AreEqual(Gear.Reverse, _taxi.Command.Gear);
            Assert.Less(_taxi.ForwardSpeedMs, -1f, "Le taxi recule");
            Assert.GreaterOrEqual(_taxi.ForwardSpeedMs * 3.6f, -_taxi.Config.pedals.maxReverseKmh - 3f);
        }

        [UnityTest]
        public IEnumerator Turns_WithoutRolling()
        {
            _taxi.Controls = new DriveInput { Accelerate = true };
            yield return Seconds(3f);
            var yaw0 = _taxi.transform.eulerAngles.y;
            _taxi.Controls = new DriveInput { Accelerate = true, Right = true };
            yield return Seconds(2f);
            var turned = Mathf.DeltaAngle(yaw0, _taxi.transform.eulerAngles.y);
            Debug.Log($"[TaxiKmer] Virage à droite : {turned:F0}° en 2 s");
            Assert.Greater(turned, 20f, "Flèche droite = tourner à droite");
            Assert.Greater(Vector3.Dot(_taxi.transform.up, Vector3.up), 0.9f, "Ne se renverse pas");
        }
    }
}
