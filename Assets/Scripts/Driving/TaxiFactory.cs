using TaxiKmer.Core.Driving;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>Matériaux du taxi provisoire (créés par l'éditeur, voir WorldSceneSetup).</summary>
    [System.Serializable]
    public class TaxiMaterials
    {
        public Material body;
        public Material glass;
        public Material tire;
        public Material trim;
        public Material headlight;
        public Material taillight;
    }

    /// <summary>
    /// Construit le taxi provisoire en blocs (berline jaune, comme les taxis de Yaoundé) : caisse, vitres, roues,
    /// WheelCollider et Rigidbody. Origine de l'objet au niveau du sol, entre les quatre roues ; avant = +z.
    /// </summary>
    public static class TaxiFactory
    {
        public const float Length = 4.4f;
        public const float Width = 1.72f;
        public const float WheelBase = 2.6f;
        public const float Track = 1.48f;

        public static TaxiCar Build(string name, TaxiMaterials materials, DriveConfig config)
        {
            var root = new GameObject(name);
            var car = config.car;

            // Caisse : une partie basse, l'habitacle vitré, le toit. Les blocs n'ont pas de collider propre.
            Block(root.transform, "Caisse", new Vector3(0f, 0.72f, 0f), new Vector3(Width, 0.62f, Length), materials.body);
            Block(root.transform, "Vitres", new Vector3(0f, 1.24f, -0.2f), new Vector3(Width - 0.16f, 0.44f, 2.1f), materials.glass);
            Block(root.transform, "Toit", new Vector3(0f, 1.49f, -0.25f), new Vector3(Width - 0.14f, 0.07f, 1.85f), materials.body);
            Block(root.transform, "Pare-chocs avant", new Vector3(0f, 0.5f, Length / 2f), new Vector3(Width + 0.02f, 0.18f, 0.12f), materials.trim);
            Block(root.transform, "Pare-chocs arrière", new Vector3(0f, 0.5f, -Length / 2f), new Vector3(Width + 0.02f, 0.18f, 0.12f), materials.trim);
            foreach (var side in new[] { -1f, 1f })
            {
                Block(root.transform, "Phare", new Vector3(side * 0.62f, 0.82f, Length / 2f + 0.005f), new Vector3(0.32f, 0.14f, 0.04f), materials.headlight);
                Block(root.transform, "Feu arrière", new Vector3(side * 0.66f, 0.84f, -Length / 2f - 0.005f), new Vector3(0.26f, 0.14f, 0.04f), materials.taillight);
            }

            // Colliders de la caisse (sans la partie roues, pour que seuls les WheelCollider touchent le sol).
            var bodyCol = root.AddComponent<BoxCollider>();
            bodyCol.center = new Vector3(0f, 0.78f, 0f);
            bodyCol.size = new Vector3(Width, 0.7f, Length);
            var cabinCol = root.AddComponent<BoxCollider>();
            cabinCol.center = new Vector3(0f, 1.3f, -0.2f);
            cabinCol.size = new Vector3(Width - 0.16f, 0.4f, 2.1f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = car.massKg;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.centerOfMass = new Vector3(0f, car.centerOfMassHeight, 0.1f);

            var wheels = new TaxiCar.Wheel[4];
            var i = 0;
            foreach (var front in new[] { true, false })
            foreach (var side in new[] { -1f, 1f })
            {
                var z = front ? WheelBase / 2f + 0.05f : -WheelBase / 2f + 0.05f;
                var pos = new Vector3(side * Track / 2f, car.wheelRadius + car.suspensionDistance * 0.6f, z);
                var label = (front ? "avant " : "arrière ") + (side < 0 ? "gauche" : "droite");

                var wcGo = new GameObject("Roue " + label);
                wcGo.transform.SetParent(root.transform, false);
                wcGo.transform.localPosition = pos;
                var wc = wcGo.AddComponent<WheelCollider>();
                ConfigureWheel(wc, car);

                var visual = Block(root.transform, "Pneu " + label, pos, Vector3.one, materials.tire, PrimitiveType.Cylinder);
                visual.localScale = new Vector3(car.wheelRadius * 2f, 0.11f, car.wheelRadius * 2f); // cylindre de 2 m de haut
                visual.localRotation = TaxiCar.WheelVisualRotation;
                wheels[i++] = new TaxiCar.Wheel { collider = wc, visual = visual, front = front, left = side < 0 };
            }
            wheels[0].collider.ConfigureVehicleSubsteps(5f, 12, 15);

            var taxi = root.AddComponent<TaxiCar>();
            taxi.wheels = wheels;
            return taxi;
        }

        public static void ConfigureWheel(WheelCollider wc, CarConfig car)
        {
            wc.mass = 20f;
            wc.radius = car.wheelRadius;
            wc.suspensionDistance = car.suspensionDistance;
            wc.forceAppPointDistance = 0.1f;
            wc.suspensionSpring = new JointSpring { spring = car.springStrength, damper = car.damper, targetPosition = 0.5f };
            var fwd = wc.forwardFriction;
            fwd.stiffness = car.forwardStiffness;
            wc.forwardFriction = fwd;
            var side = wc.sidewaysFriction;
            side.stiffness = car.sidewaysStiffness;
            wc.sidewaysFriction = side;
        }

        private static Transform Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
            PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
