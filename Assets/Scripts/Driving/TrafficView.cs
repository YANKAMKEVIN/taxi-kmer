using System.Collections.Generic;
using TaxiKmer.Core.Traffic;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Affichage du trafic et des piétons (jalon 2) : blocs provisoires (voitures de couleurs variées, taxis jaunes,
    /// mototaxis avec leur conducteur, piétons en silhouettes), placés chaque image d'après <see cref="CityLife"/>.
    /// Les objets sont réutilisés (réserve par type) ; les véhicules ont un collider cinématique pour que le taxi du
    /// joueur les heurte. Les piétons n'en ont pas encore (question 3 du jalon 2 pour Kevin : peut-on les renverser ?).
    /// </summary>
    public sealed class TrafficView : MonoBehaviour
    {
        /// <summary>Matériau blanc de base ; une copie par couleur est créée et gardée (compatible avec le SRP Batcher).</summary>
        public Material baseMaterial;

        public CityLife City { get; set; }

        private readonly Dictionary<int, Body> _vehicles = new Dictionary<int, Body>();
        private readonly Dictionary<int, Body> _people = new Dictionary<int, Body>();
        private readonly Dictionary<VehicleKind, Stack<Body>> _freeVehicles = new Dictionary<VehicleKind, Stack<Body>>();
        private readonly Stack<Body> _freePeople = new Stack<Body>();
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();

        private sealed class Body
        {
            public GameObject Root;
            public Renderer Paint;
            public Renderer Shirt;
            public VehicleKind Kind;
        }

        private static readonly Color[] CarPaints =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.65f, 0.67f, 0.7f), new Color(0.1f, 0.1f, 0.11f),
            new Color(0.6f, 0.08f, 0.08f), new Color(0.12f, 0.22f, 0.45f), new Color(0.35f, 0.36f, 0.38f),
            new Color(0.75f, 0.72f, 0.62f), new Color(0.2f, 0.35f, 0.25f),
        };

        private static readonly Color[] Shirts =
        {
            new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.45f, 0.85f), new Color(0.15f, 0.6f, 0.3f),
            new Color(0.95f, 0.6f, 0.1f), new Color(0.55f, 0.25f, 0.6f), new Color(0.9f, 0.9f, 0.85f),
            new Color(0.1f, 0.1f, 0.12f), new Color(0.0f, 0.55f, 0.6f), new Color(0.8f, 0.5f, 0.6f),
        };

        private static readonly Color TaxiYellow = new Color(0.98f, 0.78f, 0.05f);
        private static readonly Color Glass = new Color(0.08f, 0.1f, 0.12f);
        private static readonly Color Rubber = new Color(0.06f, 0.06f, 0.06f);
        private static readonly Color Skin = new Color(0.35f, 0.22f, 0.14f);

        private void LateUpdate()
        {
            if (City == null) return;
            SyncVehicles();
            SyncPeople();
        }

        // ---------- Véhicules ----------

        private void SyncVehicles()
        {
            _seen.Clear();
            foreach (var v in City.Traffic.Vehicles)
            {
                _seen.Add(v.Id);
                if (!_vehicles.TryGetValue(v.Id, out var body))
                {
                    body = TakeVehicle(v.Kind);
                    _vehicles[v.Id] = body;
                    var paint = v.Kind == VehicleKind.Taxi ? TaxiYellow : CarPaints[v.Id % CarPaints.Length];
                    if (body.Paint != null) body.Paint.sharedMaterial = MaterialFor(paint);
                    if (body.Shirt != null) body.Shirt.sharedMaterial = MaterialFor(Shirts[v.Id % Shirts.Length]);
                }
                var dir = new Vector3(v.DirX, 0f, v.DirZ);
                body.Root.transform.SetPositionAndRotation(new Vector3(v.X, 0f, v.Z),
                    dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir, Vector3.up) : body.Root.transform.rotation);
            }
            _gone.Clear();
            foreach (var kv in _vehicles) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                var body = _vehicles[id];
                _vehicles.Remove(id);
                body.Root.SetActive(false);
                Free(body.Kind).Push(body);
            }
        }

        private Stack<Body> Free(VehicleKind kind)
        {
            if (!_freeVehicles.TryGetValue(kind, out var stack)) _freeVehicles[kind] = stack = new Stack<Body>();
            return stack;
        }

        private Body TakeVehicle(VehicleKind kind)
        {
            var free = Free(kind);
            if (free.Count > 0)
            {
                var b = free.Pop();
                b.Root.SetActive(true);
                return b;
            }
            return kind == VehicleKind.Moto ? BuildMoto() : BuildCar(kind);
        }

        /// <summary>Berline en blocs : caisse peinte, habitacle vitré, bas de caisse et roues sombres. Origine au sol, avant = +z.</summary>
        private Body BuildCar(VehicleKind kind)
        {
            var root = new GameObject(kind == VehicleKind.Taxi ? "Taxi concurrent" : "Voiture");
            root.transform.SetParent(transform, false);
            var paint = Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), new Vector3(1.74f, 0.6f, 4.3f), Color.white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.22f, -0.2f), new Vector3(1.58f, 0.44f, 2.1f), Glass);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(1.66f, 0.3f, 3.9f), Rubber);
            if (kind == VehicleKind.Taxi)
                Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.5f, -0.25f), new Vector3(1.6f, 0.07f, 1.85f), TaxiYellow);
            AddKinematicBox(root, new Vector3(0f, 0.75f, 0f), new Vector3(1.74f, 1.3f, 4.3f));
            return new Body { Root = root, Paint = paint, Kind = kind };
        }

        /// <summary>Mototaxi : moto sombre, conducteur assis (chemise de couleur). Origine au sol, avant = +z.</summary>
        private Body BuildMoto()
        {
            var root = new GameObject("Mototaxi");
            root.transform.SetParent(transform, false);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.32f, 0.45f, 1.7f), new Color(0.15f, 0.15f, 0.17f));
            foreach (var z in new[] { -0.65f, 0.65f })
            {
                var wheel = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.32f, z), new Vector3(0.64f, 0.06f, 0.64f), Rubber);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var shirt = Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.25f, -0.1f), new Vector3(0.45f, 0.45f, 0.35f), Color.white);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.85f, -0.05f), Vector3.one * 0.25f, Skin);
            AddKinematicBox(root, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 1.6f, 2f));
            return new Body { Root = root, Shirt = shirt, Kind = VehicleKind.Moto };
        }

        private static void AddKinematicBox(GameObject root, Vector3 center, Vector3 size)
        {
            var col = root.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        // ---------- Piétons ----------

        private void SyncPeople()
        {
            _seen.Clear();
            foreach (var p in City.Pedestrians.People)
            {
                _seen.Add(p.Id);
                if (!_people.TryGetValue(p.Id, out var body))
                {
                    body = TakePerson();
                    _people[p.Id] = body;
                    body.Shirt.sharedMaterial = MaterialFor(Shirts[p.Look % Shirts.Length]);
                    var width = 0.42f + (p.Look % 7) * 0.05f;
                    body.Shirt.transform.localScale = new Vector3(width, 0.8f, width * 0.75f);
                }
                var face = new Vector3(p.FaceX, 0f, p.FaceZ);
                body.Root.transform.SetPositionAndRotation(new Vector3(p.X, 0f, p.Z),
                    face.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(face, Vector3.up) : body.Root.transform.rotation);
            }
            _gone.Clear();
            foreach (var kv in _people) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                var body = _people[id];
                _people.Remove(id);
                body.Root.SetActive(false);
                _freePeople.Push(body);
            }
        }

        private Body TakePerson()
        {
            if (_freePeople.Count > 0)
            {
                var b = _freePeople.Pop();
                b.Root.SetActive(true);
                return b;
            }
            var root = new GameObject("Piéton");
            root.transform.SetParent(transform, false);
            var shirt = Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(0.56f, 0.8f, 0.42f), Color.white);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.26f, Skin);
            return new Body { Root = root, Shirt = shirt };
        }

        // ---------- Outils ----------

        private Renderer Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFor(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return r;
        }

        private Material MaterialFor(Color color)
        {
            if (_materials.TryGetValue(color, out var m)) return m;
            if (baseMaterial == null) return null;
            m = new Material(baseMaterial) { name = $"Trafic {ColorUtility.ToHtmlStringRGB(color)}" };
            m.SetColor("_BaseColor", color);
            _materials[color] = m;
            return m;
        }

        private void OnDestroy()
        {
            foreach (var m in _materials.Values) Destroy(m);
            _materials.Clear();
        }
    }
}
