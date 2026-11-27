using System.Collections.Generic;
using TaxiKmer.Core.Traffic;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Affichage du trafic et des piétons (jalon 2) : blocs provisoires (voitures de couleurs variées, taxis jaunes,
    /// mototaxis avec leur conducteur, piétons en silhouettes), placés chaque image d'après <see cref="CityLife"/>.
    /// Les objets sont réutilisés (réserve par type) ; les véhicules ont un collider cinématique pour que le taxi du
    /// joueur les heurte. Les piétons ont une zone de contact (déclencheur) : le taxi qui les touche les renverse
    /// (voir <see cref="TaxiHitDetector"/> et la règle de Kevin dans Core/Incidents). Chaque véhicule porte un
    /// <see cref="TrafficVehicleMarker"/> : un choc avec le taxi devient un accrochage (jalon 6, Core/Road/TrafficCrash).
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
        private readonly List<LightHead> _lights = new List<LightHead>();
        private int _decorated;
        private static readonly Color PlateOrange = new Color(0.95f, 0.52f, 0.08f);
        private static readonly Color Trim = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color RoundelBlue = new Color(0.1f, 0.25f, 0.6f);
        private static readonly Color RoundelWhite = new Color(0.95f, 0.95f, 0.95f);
        private bool _lightsBuilt;

        /// <summary>Tête de feu tricolore : trois lampes, allumées selon la couleur pour l'axe.</summary>
        private sealed class LightHead
        {
            public int Id;
            public Renderer Red, Amber, Green;
            public LightColor? Shown;
        }

        private static readonly Color RedOn = new Color(1f, 0.08f, 0.05f);
        private static readonly Color AmberOn = new Color(1f, 0.65f, 0.05f);
        private static readonly Color GreenOn = new Color(0.1f, 1f, 0.3f);
        private static readonly Color LampOff = new Color(0.12f, 0.12f, 0.12f);

        private sealed class Body
        {
            public GameObject Root;
            public Renderer Paint;
            public Renderer Shirt;
            public VehicleKind Kind;
            /// <summary>Mototaxi : passager assis derrière le conducteur (affiché s'il en a un).</summary>
            public GameObject Passenger;
            /// <summary>Piéton : silhouette habillée, jambes qui se balancent.</summary>
            public PersonParts Person;
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
            if (!_lightsBuilt) BuildLights();
            SyncLights();
            SyncVehicles();
            SyncPeople();
        }

        // ---------- Feux tricolores ----------

        /// <summary>Un poteau par sens et par feu, à droite de la chaussée, à la ligne d'arrêt, tourné vers le trafic.</summary>
        private void BuildLights()
        {
            _lightsBuilt = true;
            var paths = City.Traffic.Paths;
            for (var p = 0; p < paths.Count; p++)
                foreach (var l in paths[p].Lights)
                {
                    var along = City.Traffic.StopLine(p, l) + 1f;
                    paths[p].Sample(along, 4.6f, out var x, out var z, out var dx, out var dz);
                    var root = new GameObject($"Feu {l.Id}");
                    root.transform.SetParent(transform, false);
                    root.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.LookRotation(new Vector3(-dx, 0f, -dz), Vector3.up));
                    Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.14f, 1.6f, 0.14f), new Color(0.2f, 0.2f, 0.2f));
                    Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 3.6f, 0f), new Vector3(0.4f, 1.15f, 0.3f), new Color(0.05f, 0.05f, 0.05f));
                    var head = new LightHead
                    {
                        Id = l.Id,
                        Red = Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 4.0f, 0.16f), Vector3.one * 0.26f, LampOff),
                        Amber = Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 3.6f, 0.16f), Vector3.one * 0.26f, LampOff),
                        Green = Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 3.2f, 0.16f), Vector3.one * 0.26f, LampOff),
                    };
                    foreach (var r in new[] { head.Red, head.Amber, head.Green })
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _lights.Add(head);
                }
        }

        private void SyncLights()
        {
            foreach (var h in _lights)
            {
                var c = City.Traffic.LightFor(h.Id);
                if (h.Shown == c) continue;
                h.Shown = c;
                h.Red.sharedMaterial = MaterialFor(c == LightColor.Red ? RedOn : LampOff);
                h.Amber.sharedMaterial = MaterialFor(c == LightColor.Amber ? AmberOn : LampOff);
                h.Green.sharedMaterial = MaterialFor(c == LightColor.Green ? GreenOn : LampOff);
            }
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
                    body.Root.GetComponent<TrafficVehicleMarker>().Id = v.Id;
                    var paint = v.Kind == VehicleKind.Taxi ? TaxiYellow : CarPaints[v.Id % CarPaints.Length];
                    if (body.Paint != null)
                    {
                        // La peinture est le premier matériau de la carrosserie (les autres : vitres, garnitures…).
                        var mats = body.Paint.sharedMaterials;
                        mats[0] = MaterialFor(paint);
                        body.Paint.sharedMaterials = mats;
                    }
                    if (body.Shirt != null)
                    {
                        var mats = body.Shirt.sharedMaterials; // peau, haut, bas, cheveux : on change le haut
                        mats[1] = MaterialFor(Shirts[v.Id % Shirts.Length]);
                        body.Shirt.sharedMaterials = mats;
                    }
                }
                if (body.Passenger != null && body.Passenger.activeSelf != v.Passengers > 0) body.Passenger.SetActive(v.Passengers > 0);
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
            // Carrosserie de berline partagée (Core/World/CarBodyBuilder) : peinture, vitres, garnitures, phares, feux, pneus.
            var shape = new GameObject("Carrosserie");
            shape.transform.SetParent(root.transform, false);
            shape.AddComponent<MeshFilter>().sharedMesh = VehicleMeshes.TrafficSedan;
            var paint = shape.AddComponent<MeshRenderer>();
            paint.sharedMaterials = new[]
            {
                MaterialFor(Color.white), MaterialFor(Glass), MaterialFor(new Color(0.08f, 0.08f, 0.09f)),
                MaterialFor(new Color(0.95f, 0.93f, 0.85f)), MaterialFor(new Color(0.7f, 0.08f, 0.06f)), MaterialFor(Rubber),
            };
            // Taxis de Yaoundé (photos de référence) : pas d'enseigne sur le toit, mais baguette sombre et pastille numérotée.
            Decorate(root.transform, 4.3f, 1.74f, kind == VehicleKind.Taxi, _decorated++);
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
            // Conducteur et passager assis : silhouettes habillées, jambes repliées vers l'avant.
            var driver = Seated(root.transform, "Conducteur", -0.05f, _decorated++);
            var shirt = driver.Body;
            var passengerParts = Seated(root.transform, "Passager", -0.55f, _decorated++);
            var passenger = passengerParts.Root;
            passenger.SetActive(false);
            AddKinematicBox(root, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 1.6f, 2f));
            return new Body { Root = root, Shirt = shirt, Kind = VehicleKind.Moto, Passenger = passenger };
        }

        /// <summary>Personne assise sur la moto : bassin sur la selle, jambes à l'horizontale vers l'avant.</summary>
        private PersonParts Seated(Transform parent, string name, float z, int look)
        {
            var p = PeopleMeshes.Build(parent, name);
            PeopleMeshes.Dress(p, 1, look * 3 + 1, Shirts[look % Shirts.Length], _materialFor ??= MaterialFor); // pas de pagne sur la moto
            p.Root.transform.localPosition = new Vector3(0f, 0.85f - Core.World.PersonBuilder.HipHeight, z);
            p.LegL.localRotation = p.LegR.localRotation = Quaternion.Euler(-75f, 0f, 0f);
            return p;
        }

        private static void AddKinematicBox(GameObject root, Vector3 center, Vector3 size)
        {
            root.AddComponent<TrafficVehicleMarker>(); // le taxi qui le heurte sait lequel (accrochages, jalon 6)
            var col = root.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>
        /// Détails d'après les photos de référence (taxis de Yaoundé, CC BY-SA, docs/references/photos/Lot07) : plaques
        /// orange devant et derrière pour toutes les voitures ; pour un taxi, baguette sombre sur les flancs et pastille
        /// bleu et blanc (son numéro) sur les portières avant. Origine du véhicule au sol, avant = +z.
        /// </summary>
        public void Decorate(Transform root, float length, float width, bool taxi, int seed)
        {
            foreach (var end in new[] { -1f, 1f })
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.52f, end * (length / 2f + 0.1f)), new Vector3(0.36f, 0.11f, 0.02f), PlateOrange) // sur le pare-chocs
                    .shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!taxi) return;
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (width / 2f + 0.006f);
                Part(root, PrimitiveType.Cube, new Vector3(x, 0.62f, -0.1f), new Vector3(0.012f, 0.05f, length * 0.78f), Trim)
                    .shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Pastille : disque bleu, cœur blanc (un cylindre aplati, tourné vers le côté).
                var outer = Part(root, PrimitiveType.Cylinder, new Vector3(x + side * 0.004f, 0.86f, 0.45f), new Vector3(0.3f, 0.004f, 0.3f), RoundelBlue);
                outer.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                outer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var inner = Part(root, PrimitiveType.Cylinder, new Vector3(x + side * 0.009f, 0.86f, 0.45f), new Vector3(0.2f, 0.004f, 0.2f), RoundelWhite);
                inner.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                inner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
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
                    body.Root.GetComponent<PedestrianMarker>().Id = p.Id;
                    var corpulence = p.Look % 7 < 2 ? 0 : p.Look % 7 > 4 ? 2 : 1;
                    PeopleMeshes.Dress(body.Person, corpulence, p.Look, Shirts[p.Look % Shirts.Length], _materialFor ??= MaterialFor);
                    body.Root.transform.position = new Vector3(p.X, 0f, p.Z);
                }
                var face = new Vector3(p.FaceX, 0f, p.FaceZ);
                var at = new Vector3(p.X, 0f, p.Z);
                PeopleMeshes.Walk(body.Person, (at - body.Root.transform.position).magnitude, p.Speed);
                body.Root.transform.SetPositionAndRotation(at,
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
            root.AddComponent<PedestrianMarker>();
            var contact = root.AddComponent<CapsuleCollider>();
            contact.isTrigger = true;
            contact.center = new Vector3(0f, 0.9f, 0f);
            contact.radius = 0.3f;
            contact.height = 1.8f;
            var person = PeopleMeshes.Build(root.transform, "Silhouette");
            return new Body { Root = root, Person = person };
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

        private System.Func<Color, Material> _materialFor;

        /// <summary>Matériau de cette couleur (partagé, créé à la demande) : aussi pour les clients de la scène.</summary>
        public Material Paint(Color color) => MaterialFor(color);

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
