using System.Collections.Generic;
using System.Text;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.World;
using TaxiKmer.World;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Clients dans le monde (tâche 1.5) : silhouettes au bord de la route (largeur selon la carrure), bulles
    /// « Étoudi, 500 ! » quand le taxi arrive dans leur sens, klaxon pour accepter, bouton contextuel pour faire
    /// monter ou descendre, repères des destinations, heure, recette et passagers à l'écran.
    /// La logique est dans <see cref="Street"/> (Core) ; ce composant ne fait que l'afficher et la piloter.
    /// Jalon 2 : il fait aussi vivre la ville autour (<see cref="CityLife"/> : trafic, mototaxis, piétons, taxis
    /// concurrents qui prennent les clients), affichée par <see cref="TrafficView"/>.
    /// </summary>
    public sealed class StreetView : MonoBehaviour
    {
        public WorldBuilder world;
        public TaxiCar taxi;
        public DriverControls controls;
        public Material personMaterial;
        public Material markerMaterial;
        [Tooltip("Distance des clients à l'axe de la chaussée de leur sens (m), à sa droite.")]
        public float sideOffset = 5.5f;
        [Tooltip("Graine des clients ; 0 = différente à chaque partie.")]
        public int seed;

        public Street Street { get; private set; }
        /// <summary>Trafic, piétons et concurrence (jalon 2).</summary>
        public CityLife City { get; private set; }

        private float[] _xz;
        private float[] _cum;
        private readonly Dictionary<RoadsideClient, Person> _people = new Dictionary<RoadsideClient, Person>();
        private readonly Dictionary<string, GameObject> _stopMarkers = new Dictionary<string, GameObject>();
        private readonly List<Text> _bubbles = new List<Text>();
        private readonly List<Rect> _placed = new List<Rect>();
        private Text _status;
        private Text _toast;
        private float _toastUntil;
        private bool _dayOverShown;
        private DaySummaryView _summaryView;
        private MaterialPropertyBlock _block;
        private Camera _camera;
        private QualityView _quality;
        private bool _cityPopulated;
        private AccidentConfig _accidents;
        private System.Random _random;

        private sealed class Person
        {
            public GameObject Root;
            public GameObject AcceptedMark;
        }

        private static readonly Color[] Shirts =
        {
            new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.45f, 0.85f), new Color(0.15f, 0.6f, 0.3f),
            new Color(0.95f, 0.6f, 0.1f), new Color(0.55f, 0.25f, 0.6f), new Color(0.9f, 0.9f, 0.85f),
            new Color(0.1f, 0.1f, 0.12f), new Color(0.0f, 0.55f, 0.6f),
        };

        private void Start()
        {
            if (world == null || world.Data == null || taxi == null || controls == null)
            {
                Debug.LogError("[TaxiKmer] StreetView : monde, taxi ou commandes manquants.");
                enabled = false;
                return;
            }
            _xz = world.Data.route.xz;
            _cum = WorldGeometry.CumulativeLengths(_xz);
            _block = new MaterialPropertyBlock();
            _camera = Camera.main;

            var stops = Load("Data/stops_messassi_etoudi", new StopsConfig());
            var fares = Load("Data/fares", FareConfig.Default());
            var shift = new Shift(Load("Data/day", new DayConfig()), Load("Data/seating", new SeatingConfig()));
            var actualSeed = seed != 0 ? seed : System.Environment.TickCount;
            var factory = new ClientFactory(stops, fares, actualSeed);
            Street = new Street(Load("Data/street", new StreetConfig()), stops, factory, shift, _cum[_cum.Length - 1]);

            // La ville autour : trafic, piétons, taxis concurrents (jalon 2).
            City = new CityLife(Load("Data/traffic", new CityConfig()), world.Data, Street, actualSeed + 17, sideOffset);
            _quality = controls.GetComponent<QualityView>();
            ApplyTrafficScale();
            _accidents = Load("Data/accidents", new AccidentConfig());
            _random = new System.Random(actualSeed + 31);
            var detector = taxi.gameObject.AddComponent<TaxiHitDetector>();
            detector.taxi = taxi;
            detector.PedestrianHit += OnPedestrianHit;
            var trafficView = gameObject.AddComponent<TrafficView>();
            trafficView.baseMaterial = personMaterial;
            trafficView.City = City;

            foreach (var stop in stops.stops) _stopMarkers[stop.id] = MakeStopMarker(stop);
            BuildHud();
        }

        private static T Load<T>(string resource, T fallback)
        {
            var text = Resources.Load<TextAsset>(resource);
            if (text != null) return JsonUtility.FromJson<T>(text.text);
            Debug.LogWarning($"[TaxiKmer] Resources/{resource} introuvable : valeurs par défaut.");
            return fallback;
        }

        /// <summary>Heure du jeu (6,5 = 6 h 30).</summary>
        private float Hour => Street.Shift.Clock.GameMinutes / 60f;

        /// <summary>Le taxi du joueur, pour le trafic (obstacle) et les piétons (ils ne traversent pas devant lui).</summary>
        private PlayerCar PlayerState()
        {
            var t = taxi.transform;
            var f = t.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            return new PlayerCar
            {
                X = t.position.x,
                Z = t.position.z,
                DirX = f.x,
                DirZ = f.z,
                Speed = taxi.ForwardSpeedMs,
                Length = TaxiFactory.Length,
                Width = TaxiFactory.Width,
            };
        }

        private void ApplyTrafficScale()
        {
            var scale = _quality != null ? _quality.Current.trafficScale : 1f;
            City.Traffic.DensityScale = scale;
            City.Pedestrians.DensityScale = scale;
        }

        private TaxiOnRoute TaxiState()
        {
            var p = taxi.transform.position;
            WorldGeometry.Project(_xz, _cum, p.x, p.z, out var along, out _);
            WorldGeometry.SampleAlong(_xz, _cum, along, out var rx, out var rz, out var dx, out var dz);
            var f = taxi.transform.forward;
            return new TaxiOnRoute
            {
                Along = along,
                // Signé : positif à droite dans le sens vers Étoudi, dont la droite est (dz, -dx).
                Lateral = (p.x - rx) * dz - (p.z - rz) * dx,
                Direction = f.x * dx + f.z * dz >= 0f ? 1 : -1,
                SpeedKmh = taxi.SpeedKmh,
            };
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var shift = Street.Shift;
            if (!Street.DayFinished)
            {
                shift.Clock.Advance(dt);
                shift.Ledger.AddDistance(Mathf.Abs(taxi.SpeedKmh) / 3.6f * dt);
            }

            var state = TaxiState();
            Street.Update(dt, state);
            ApplyTrafficScale();
            if (!_cityPopulated)
            {
                // À la première image : le taxi est alors posé à son départ (TaxiCar.Start).
                _cityPopulated = true;
                City.Populate(Hour, PlayerState());
            }
            City.Update(dt, Hour, PlayerState());
            foreach (var lost in Street.Lost) OnClientLost(lost);

            if (controls.HornTriggered) OnHonk(state);
            var action = Street.AvailableAction(state);
            if (controls.ActionTriggered && action.Kind != StreetActionKind.None) OnAction(state);
            action = Street.AvailableAction(state);
            controls.ActionVisible = action.Kind != StreetActionKind.None;
            controls.ActionLabel = action.Kind switch
            {
                StreetActionKind.Drop => action.Count > 1 ? $"Faire descendre ({action.Count})" : "Faire descendre",
                StreetActionKind.Board => action.MoveFirst != null ? "Changer de place et monter" : "Faire monter",
                StreetActionKind.EndDay => "Terminer la journée",
                _ => "",
            };

            if (shift.Clock.IsOver && !_dayOverShown && !Street.DayFinished)
            {
                _dayOverShown = true;
                Toast("Fin de la journée : dépose tes passagers, ou arrête-toi pour terminer", 8f);
            }
            if (Street.DayFinished) ShowSummary();

            SyncPeople();
            SyncStopMarkers();
            UpdateHud();
        }

        private void OnHonk(TaxiOnRoute state)
        {
            var result = Street.Honk(state, out var client);
            switch (result)
            {
                case HonkResult.Accepted:
                    Toast($"OK pour {ShortName(client.Client.DestinationStopId)} ({Money(client.Client.ProposedFare)}) : arrête-toi");
                    break;
                case HonkResult.AcceptedNeedsMove:
                    Toast("OK, mais il faudra faire changer un passager de place");
                    break;
                case HonkResult.NoRoom:
                    Toast($"Pas de place pour ce client ({CorpulenceLabel(client.Client)})");
                    break;
            }
        }

        /// <summary>Le taxi a touché un piéton : règle de Kevin (hôpital et soins, ou gendarmerie).</summary>
        private void OnPedestrianHit(int pedestrianId, float impactKmh)
        {
            if (Street == null || Street.DayFinished) return;
            var outcome = Street.PedestrianHit(_accidents, impactKmh, _random);
            if (outcome.Severity == InjurySeverity.None) return; // simple bousculade
            City.Pedestrians.Remove(pedestrianId);
            if (outcome.Arrested) return; // garde à vue : le bilan s'affiche (journée terminée)
            var who = outcome.Severity == InjurySeverity.Light ? "blessé léger" : "blessé grave";
            var hours = outcome.LostGameMinutes >= 60 ? $"{outcome.LostGameMinutes / 60} h {outcome.LostGameMinutes % 60:00}" : $"{outcome.LostGameMinutes} min";
            Toast($"Piéton renversé ({who}) : hôpital et soins -{Money(outcome.CareCost)}, {hours} perdues, tes passagers sont partis sans payer", 9f);
        }

        /// <summary>Un client n'attend plus : on le dit au joueur si c'était le sien, ou s'il était tout près.</summary>
        private void OnClientLost(ClientLost lost)
        {
            if (lost.WasAccepted && lost.Reason == ClientLostReason.GaveUp)
            {
                Toast("Ton client en a eu marre d'attendre : il fait signe aux autres taxis");
                return;
            }
            if (lost.Reason != ClientLostReason.TookOtherTaxi) return;
            if (lost.WasAccepted)
            {
                Toast("Un bendskin a pris ton client avant que tu arrives !");
                return;
            }
            var place = City.Place(lost.Client);
            var d = new Vector2(place.X - taxi.transform.position.x, place.Z - taxi.transform.position.z).magnitude;
            var who = lost.ByMoto ? "Un bendskin" : "Un autre taxi";
            if (d < 120f) Toast($"{who} a pris le client pour {ShortName(lost.Client.Client.DestinationStopId)}");
        }

        private void OnAction(TaxiOnRoute state)
        {
            var outcome = Street.Perform(state);
            switch (outcome.Kind)
            {
                case StreetActionKind.Drop:
                    Toast($"+{Money(outcome.Paid)}");
                    foreach (var c in outcome.Dropped) SpawnLeavingPerson(c);
                    break;
                case StreetActionKind.Board:
                    if (outcome.Board == BoardResult.Boarded)
                    {
                        var moved = outcome.Moved != null ? "Un passager a changé de place. " : "";
                        Toast($"{moved}Monté : {ShortName(outcome.Boarded.DestinationStopId)}, {Money(outcome.Boarded.ProposedFare)}");
                    }
                    else if (outcome.Board == BoardResult.DayOver) Toast("La journée est finie");
                    else Toast("Il ne rentre pas");
                    break;
            }
        }

        private void ShowSummary()
        {
            if (_summaryView != null) return;
            controls.Locked = true;
            controls.ActionVisible = false;
            _toast.enabled = false;
            _toastUntil = 0f;
            _summaryView = gameObject.AddComponent<DaySummaryView>();
            _summaryView.Show(Street.Summary, Street.Shift.Clock.Display, controls.Font);
        }

        /// <summary>L'écran de bilan (null tant que la journée n'est pas terminée).</summary>
        public DaySummaryView SummaryView => _summaryView;

        // ---------- Silhouettes ----------

        private void SyncPeople()
        {
            var seen = new HashSet<RoadsideClient>(Street.Waiting);
            var gone = new List<RoadsideClient>();
            foreach (var kv in _people)
                if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var c in gone)
            {
                Destroy(_people[c].Root);
                _people.Remove(c);
            }

            var calling = new List<RoadsideClient>();
            foreach (var c in Street.Waiting)
            {
                if (!_people.TryGetValue(c, out var person)) _people[c] = person = MakePerson(c);
                person.AcceptedMark.SetActive(c.State == RoadsideState.Accepted);
                if (c.Calling) calling.Add(c);
            }
            // Le plus proche d'abord : sa bulle reste à sa place, les suivantes montent si elles la chevauchent.
            var taxiPos = taxi.transform.position;
            calling.Sort((a, b) => (_people[a].Root.transform.position - taxiPos).sqrMagnitude
                .CompareTo((_people[b].Root.transform.position - taxiPos).sqrMagnitude));
            _placed.Clear();
            for (var i = 0; i < calling.Count; i++)
            {
                var c = calling[i];
                ShowBubble(i, _people[c].Root.transform.position + Vector3.up * 2.4f,
                    $"{ShortName(c.Client.DestinationStopId)}, {c.Client.ProposedFare} !");
            }
            for (var i = calling.Count; i < _bubbles.Count; i++) _bubbles[i].enabled = false;
        }

        private Person MakePerson(RoadsideClient c)
        {
            // Sur le trottoir de la chaussée de son sens (CityLife), tourné vers les taxis qui arrivent.
            var place = City.Place(c);
            var pos = new Vector3(place.X, 0f, place.Z);
            var root = new GameObject("Client " + c.Client.Id);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(place.FaceX, 0f, place.FaceZ), Vector3.up));

            var width = c.Client.Corpulence switch
            {
                Core.Rides.Corpulence.Mince => 0.42f,
                Core.Rides.Corpulence.Costaud => 0.78f,
                _ => 0.56f,
            };
            var shirt = Shirts[(c.Client.Id.GetHashCode() & 0x7fffffff) % Shirts.Length];
            Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(width, 0.8f, width * 0.75f), shirt);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
            var mark = Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 2.5f, 0f), new Vector3(0.35f, 0.35f, 0.35f), new Color(1f, 0.8f, 0.05f));
            mark.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            return new Person { Root = root, AcceptedMark = mark };
        }

        private void SpawnLeavingPerson(Client c)
        {
            var t = taxi.transform;
            var go = new GameObject("Client déposé");
            go.transform.SetPositionAndRotation(t.position + t.right * 2.5f, Quaternion.LookRotation(t.right, Vector3.up));
            var shirt = Shirts[(c.Id.GetHashCode() & 0x7fffffff) % Shirts.Length];
            Part(go.transform, PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(0.56f, 0.8f, 0.42f), shirt);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
            Destroy(go, 5f);
        }

        private GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (personMaterial != null) r.sharedMaterial = personMaterial;
            _block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(_block);
            return go;
        }

        // ---------- Repères de destination ----------

        private GameObject MakeStopMarker(Stop stop)
        {
            WorldGeometry.SampleAlong(_xz, _cum, stop.distanceAlongRouteMeters, out var x, out var z, out _, out _);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Repère " + stop.name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, 6f, z);
            go.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
            if (markerMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
            go.SetActive(false);
            return go;
        }

        private void SyncStopMarkers()
        {
            foreach (var kv in _stopMarkers)
                kv.Value.SetActive(Street.Shift.Cabin.PassengersFor(kv.Key).Count > 0);
        }

        // ---------- Interface ----------

        private void BuildHud()
        {
            _status = NewText(TextAnchor.UpperLeft);
            var sr = _status.rectTransform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
            _toast = NewText(TextAnchor.MiddleCenter);
            var tr = _toast.rectTransform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
        }

        private Text NewText(TextAnchor anchor)
        {
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(controls.Overlay, false);
            var t = go.GetComponent<Text>();
            t.font = controls.Font;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            return t;
        }

        private void ShowBubble(int index, Vector3 world, string text)
        {
            while (_bubbles.Count <= index)
            {
                var b = NewText(TextAnchor.LowerCenter);
                b.rectTransform.anchorMin = b.rectTransform.anchorMax = Vector2.zero;
                b.rectTransform.pivot = new Vector2(0.5f, 0f);
                b.color = new Color(1f, 0.95f, 0.6f);
                _bubbles.Add(b);
            }
            var bubble = _bubbles[index];
            var screen = _camera != null ? _camera.WorldToScreenPoint(world) : Vector3.back;
            bubble.enabled = screen.z > 0f;
            if (!bubble.enabled) return;
            var h = Screen.height;
            bubble.fontSize = Mathf.RoundToInt(h * Mathf.Clamp(0.7f / screen.z, 0.028f, 0.045f));
            bubble.text = text;
            var size = new Vector2(bubble.preferredWidth + h * 0.02f, bubble.fontSize * 1.25f);
            var pos = new Vector2(screen.x, screen.y);
            for (var guard = 0; guard < 10; guard++)
            {
                var r = new Rect(pos.x - size.x / 2f, pos.y, size.x, size.y);
                var overlap = false;
                foreach (var p in _placed)
                {
                    if (!p.Overlaps(r)) continue;
                    overlap = true;
                    pos.y = p.yMax + 1f;
                }
                if (!overlap) { _placed.Add(r); break; }
            }
            bubble.rectTransform.sizeDelta = size;
            bubble.rectTransform.anchoredPosition = pos;
        }

        private void UpdateHud()
        {
            var h = Screen.height;
            var shift = Street.Shift;
            var sb = new StringBuilder();
            sb.Append(shift.Clock.Display).Append("   ").Append(Money(shift.Ledger.Revenue)).Append('\n');
            var versement = shift.Ledger.Versement;
            sb.Append("Versement ").Append(Thousands(System.Math.Min(shift.Ledger.Revenue, versement)))
              .Append(" / ").Append(Thousands(versement))
              .Append(shift.Ledger.Revenue >= versement ? " (atteint)" : "").Append('\n');
            sb.Append("À bord ").Append(shift.Cabin.PassengerCount).Append("/5");
            var first = true;
            foreach (var p in shift.Cabin.Passengers)
            {
                sb.Append(first ? " : " : ", ").Append(ShortName(p.DestinationStopId));
                first = false;
            }
            _status.text = sb.ToString();
            _status.fontSize = Mathf.RoundToInt(h * 0.04f);
            _status.rectTransform.anchoredPosition = new Vector2(h * 0.04f, -h * 0.12f);
            _status.rectTransform.sizeDelta = new Vector2(h * 1.2f, h * 0.3f);
            _status.enabled = !Street.DayFinished;

            _toast.enabled = Time.time < _toastUntil;
            _toast.fontSize = Mathf.RoundToInt(h * 0.045f);
            _toast.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.24f);
            _toast.rectTransform.sizeDelta = new Vector2(h * 1.4f, h * 0.1f);
        }

        private void Toast(string text, float seconds = 3f)
        {
            _toast.text = text;
            _toastUntil = Time.time + seconds;
        }

        private string ShortName(string stopId)
        {
            var name = Street.FindStop(stopId)?.name ?? stopId;
            var cut = name.IndexOf(" (", System.StringComparison.Ordinal);
            if (cut > 0) name = name.Substring(0, cut);
            cut = name.IndexOf(" / ", System.StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        private static string CorpulenceLabel(Client c) => c.Corpulence switch
        {
            Core.Rides.Corpulence.Mince => "mince",
            Core.Rides.Corpulence.Costaud => "costaud",
            _ => "carrure normale",
        };

        private static string Money(int amount) => DaySummary.Money(amount);

        /// <summary>10000 → « 10 000 » (sans la devise).</summary>
        private static string Thousands(int amount) => DaySummary.Money(amount).Replace(" FCFA", "");
    }
}
