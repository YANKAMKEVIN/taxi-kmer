using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
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
    /// </summary>
    public sealed class StreetView : MonoBehaviour
    {
        public WorldBuilder world;
        public TaxiCar taxi;
        public DriverControls controls;
        public Material personMaterial;
        public Material markerMaterial;
        [Tooltip("Distance des clients à l'axe de la route (m), du côté droit de leur sens.")]
        public float sideOffset = 5.5f;
        [Tooltip("Graine des clients ; 0 = différente à chaque partie.")]
        public int seed;

        public Street Street { get; private set; }

        private float[] _xz;
        private float[] _cum;
        private readonly Dictionary<RoadsideClient, Person> _people = new Dictionary<RoadsideClient, Person>();
        private readonly Dictionary<string, GameObject> _stopMarkers = new Dictionary<string, GameObject>();
        private readonly List<Bubble> _bubbles = new List<Bubble>();
        private readonly List<Rect> _placed = new List<Rect>();
        private Text _toast;
        private float _toastUntil;
        private bool _dayOverShown;
        private DaySummaryView _summaryView;
        private Tutorial _tutorial;
        private Image _tutorialPanel;
        private Text _tutorialText;
        private RouteStripView _strip;
        private CabinPlanView _cabinView;
        private ShiftGaugesView _gauges;
        private GameMenu _menu;
        private TaxiSounds _sounds;
        private GameMenuView _menuView;
        private readonly GameSettings _settings = new GameSettings();
        private bool _timeStopped;
        private float _savedTimeScale = 1f;

        /// <summary>L'écran titre a déjà été passé depuis le lancement (une nouvelle journée repart directement au volant).</summary>
        public static bool TitleDone { get; set; }
        /// <summary>Clé des préférences : volume général (0 à 1).</summary>
        public const string VolumeKey = "reglages.volume";

        /// <summary>Clé des préférences : le tutoriel de la première journée a été suivi jusqu'au bout.</summary>
        public const string TutorialDoneKey = "tutoriel.fait";
        private MaterialPropertyBlock _block;
        private Camera _camera;

        /// <summary>Bulle d'un client : fond sombre arrondi, pointe, bordure (rentre / à déplacer / ne rentre pas), carrure, texte.</summary>
        private sealed class Bubble
        {
            public RectTransform Root;
            public Image Border;
            public Image Pointer;
            public Image Build;
            public Text Text;
        }

        private static readonly Color BubbleFits = new Color(0.3f, 0.85f, 0.4f);
        private static readonly Color BubbleNeedsMove = new Color(1f, 0.6f, 0.15f);
        private static readonly Color BubbleNoRoom = new Color(1f, 0.3f, 0.25f);

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
            var factory = new ClientFactory(stops, fares, seed != 0 ? seed : System.Environment.TickCount);
            Street = new Street(Load("Data/street", new StreetConfig()), stops, factory, shift, _cum[_cum.Length - 1]);

            foreach (var stop in stops.stops) _stopMarkers[stop.id] = MakeStopMarker(stop);
            _sounds = taxi.GetComponent<TaxiSounds>();
            if (_sounds == null) _sounds = taxi.gameObject.AddComponent<TaxiSounds>(); // pas de « ?? » avec un objet Unity
            _tutorial = new Tutorial(PlayerPrefs.GetInt(TutorialDoneKey, 0) == 0, shift.Ledger.Versement);
            BuildHud();
            _gauges = new ShiftGaugesView(controls.Overlay, controls.Font);
            _menu = new GameMenu(!TitleDone);
            _menuView = new GameMenuView(transform, controls.Font);
            _settings.Volume = PlayerPrefs.GetFloat(VolumeKey, GameSettings.DefaultVolume);
            AudioListener.volume = _settings.Volume;
            _strip = new RouteStripView(controls.Overlay, controls.Font, stops.stops, _cum[_cum.Length - 1]);
            _cabinView = new CabinPlanView(controls.Overlay, controls.Font, stops.stops);
        }

        private static T Load<T>(string resource, T fallback)
        {
            var text = Resources.Load<TextAsset>(resource);
            if (text != null) return JsonUtility.FromJson<T>(text.text);
            Debug.LogWarning($"[TaxiKmer] Resources/{resource} introuvable : valeurs par défaut.");
            return fallback;
        }

        private TaxiOnRoute TaxiState()
        {
            var p = taxi.transform.position;
            WorldGeometry.Project(_xz, _cum, p.x, p.z, out var along, out var lateral);
            WorldGeometry.SampleAlong(_xz, _cum, along, out _, out _, out var dx, out var dz);
            var f = taxi.transform.forward;
            return new TaxiOnRoute
            {
                Along = along,
                Lateral = lateral,
                Direction = f.x * dx + f.z * dz >= 0f ? 1 : -1,
                SpeedKmh = taxi.SpeedKmh,
            };
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var shift = Street.Shift;

            // Menu d'abord : la touche qui ouvre la pause ne doit pas la refermer dans la même image.
            // Le bilan passe devant tout (l'écran titre ne reste pas affiché par-dessus).
            ApplyMenu(_menuView.Update(Street.DayFinished ? MenuScreen.None : _menu.Screen, _settings, shift.Cabin.PassengerCount));
            if (controls.PauseTriggered && !Street.DayFinished && _menu.Pause()) SetTimeStopped(true);
            controls.Locked = _menu.Frozen || _summaryView != null;

            if (!Street.DayFinished && !_menu.Frozen)
            {
                shift.Clock.Advance(dt);
                shift.Ledger.AddDistance(Mathf.Abs(taxi.SpeedKmh) / 3.6f * dt);
            }

            var state = TaxiState();
            Street.Update(dt, state);

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
            UpdateTutorial(dt, state);

            SyncPeople();
            SyncStopMarkers();
            UpdateHud();
            _strip.Update(Street, state.Along, !Street.DayFinished);
            _cabinView.Update(Street.Shift.Cabin, !Street.DayFinished);
        }

        private void ApplyMenu(MenuChoice choice)
        {
            switch (choice)
            {
                case MenuChoice.StartDay:
                    _menu.StartDay();
                    TitleDone = true;
                    break;
                case MenuChoice.Resume: _menu.Resume(); break;
                case MenuChoice.OpenSettings: _menu.OpenSettings(); break;
                case MenuChoice.CloseSettings: _menu.CloseSettings(); break;
                case MenuChoice.EndDay:
                    if (_menu.EndDay()) Street.EndDayNow();
                    break;
                case MenuChoice.VolumeDown:
                case MenuChoice.VolumeUp:
                    if (choice == MenuChoice.VolumeUp) _settings.VolumeUp(); else _settings.VolumeDown();
                    AudioListener.volume = _settings.Volume;
                    PlayerPrefs.SetFloat(VolumeKey, _settings.Volume);
                    PlayerPrefs.Save();
                    break;
            }
            SetTimeStopped(_menu.TimeStopped);
        }

        /// <summary>Pause : physique, horloge et sons arrêtés (Time.timeScale = 0), rétablis à la reprise.</summary>
        private void SetTimeStopped(bool stop)
        {
            if (stop == _timeStopped) return;
            _timeStopped = stop;
            if (stop)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else Time.timeScale = _savedTimeScale;
            AudioListener.pause = stop;
        }

        /// <summary>L'appli passe en arrière-plan (appel, notification) : la journée se met en pause.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && Street != null && _menu != null && !Street.DayFinished && _menu.Pause()) SetTimeStopped(true);
        }

        private void OnDestroy() => SetTimeStopped(false);

        /// <summary>Tutoriel de la première journée : consigne sous le bouton contextuel, bouton concerné mis en avant.</summary>
        private void UpdateTutorial(float dt, TaxiOnRoute state)
        {
            var wasActive = _tutorial.Active;
            if (wasActive && !Street.DayFinished && !_menu.Frozen)
            {
                var accepted = false;
                foreach (var c in Street.Waiting) accepted |= c.State == RoadsideState.Accepted;
                _tutorial.Update(dt, new TutorialObservation
                {
                    SpeedKmh = state.SpeedKmh,
                    HasAcceptedClient = accepted,
                    Passengers = Street.Shift.Cabin.PassengerCount,
                    RidesPaid = Street.Shift.Ledger.RideCount,
                    ActionVisible = controls.ActionVisible,
                });
                if (!_tutorial.Active)
                {
                    PlayerPrefs.SetInt(TutorialDoneKey, 1);
                    PlayerPrefs.Save();
                }
            }
            var show = _tutorial.Active && !Street.DayFinished && !_menu.Frozen;
            controls.Highlight = show ? _tutorial.Highlight : Core.Driving.DriveControl.None;
            _tutorialPanel.gameObject.SetActive(show);
            if (!show) return;
            var h = Screen.height;
            _tutorialText.text = _tutorial.Instruction;
            _tutorialText.fontSize = Mathf.RoundToInt(h * 0.04f);
            var w = Mathf.Min(Screen.width * 0.6f, h * 1.1f);
            var r = _tutorialPanel.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -h * 0.30f);
            r.sizeDelta = new Vector2(w, Mathf.Max(h * 0.09f, _tutorialText.preferredHeight + h * 0.03f));
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
                    _cabinView.ShowRefused(client.Client);
                    break;
            }
        }

        private void OnAction(TaxiOnRoute state)
        {
            var candidate = Street.AvailableAction(state).Client;
            var outcome = Street.Perform(state);
            switch (outcome.Kind)
            {
                case StreetActionKind.Drop:
                    Toast($"+{Money(outcome.Paid)}");
                    _sounds.PlayDoor();
                    if (outcome.Paid > 0) _sounds.PlayCoins();
                    foreach (var c in outcome.Dropped) SpawnLeavingPerson(c);
                    break;
                case StreetActionKind.Board:
                    if (outcome.Board == BoardResult.Boarded)
                    {
                        _sounds.PlayDoor();
                        var moved = outcome.Moved != null ? "Un passager a changé de place. " : "";
                        Toast($"{moved}Monté : {ShortName(outcome.Boarded.DestinationStopId)}, {Money(outcome.Boarded.ProposedFare)}");
                    }
                    else if (outcome.Board == BoardResult.DayOver) Toast("La journée est finie");
                    else
                    {
                        Toast("Il ne rentre pas");
                        if (candidate != null) _cabinView.ShowRefused(candidate.Client);
                    }
                    break;
            }
        }

        private void ShowSummary()
        {
            if (_summaryView != null) return;
            controls.Locked = true;
            controls.ActionVisible = false;
            _sounds.Muted = true;
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
                ShowBubble(i, _people[c].Root.transform.position + Vector3.up * 2.4f, c.Client);
            }
            for (var i = calling.Count; i < _bubbles.Count; i++) _bubbles[i].Root.gameObject.SetActive(false);
        }

        private Person MakePerson(RoadsideClient c)
        {
            WorldGeometry.SampleAlong(_xz, _cum, c.Along, out var x, out var z, out var dx, out var dz);
            dx *= c.Direction;
            dz *= c.Direction;
            var right = new Vector3(dz, 0f, -dx);
            var pos = new Vector3(x, 0f, z) + right * sideOffset;
            var root = new GameObject("Client " + c.Client.Id);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-right, Vector3.up));

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
            _toast = NewText(TextAnchor.MiddleCenter);
            var tr = _toast.rectTransform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);

            _tutorialPanel = UiKit.NewImage(controls.Overlay, "Tutoriel", new Color(0.05f, 0.05f, 0.07f, 0.82f), UiKit.Rounded);
            _tutorialText = UiKit.NewText(_tutorialPanel.transform, controls.Font, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.55f));
            UiKit.Fill(_tutorialText.rectTransform, Screen.height * 0.015f);
            _tutorialPanel.gameObject.SetActive(false);
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

        private Bubble NewBubble()
        {
            var go = new GameObject("Bulle", typeof(RectTransform));
            go.transform.SetParent(controls.Overlay, false);
            var b = new Bubble { Root = (RectTransform)go.transform };
            b.Root.anchorMin = b.Root.anchorMax = Vector2.zero;
            b.Root.pivot = new Vector2(0.5f, 0f);
            var dark = new Color(0.06f, 0.06f, 0.08f, 0.9f);
            b.Pointer = UiKit.NewImage(b.Root, "Pointe", dark);
            b.Pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var bg = UiKit.NewImage(b.Root, "Fond", dark, UiKit.Rounded);
            UiKit.Fill(bg.rectTransform);
            b.Border = UiKit.NewImage(b.Root, "Bordure", BubbleFits, UiKit.Frame);
            UiKit.Fill(b.Border.rectTransform);
            b.Build = UiKit.NewImage(b.Root, "Carrure", new Color(1f, 1f, 1f, 0.9f), UiKit.Rounded);
            b.Text = UiKit.NewText(b.Root, controls.Font, TextAnchor.MiddleLeft, Color.white);
            b.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.Text.supportRichText = true;
            return b;
        }

        private void ShowBubble(int index, Vector3 world, Client client)
        {
            while (_bubbles.Count <= index) _bubbles.Add(NewBubble());
            var bubble = _bubbles[index];
            var screen = _camera != null ? _camera.WorldToScreenPoint(world) : Vector3.back;
            var visible = screen.z > 0f;
            bubble.Root.gameObject.SetActive(visible);
            if (!visible) return;
            var h = Screen.height;
            var font = Mathf.RoundToInt(h * Mathf.Clamp(0.7f / screen.z, 0.028f, 0.045f));
            bubble.Text.fontSize = font;
            // Destination en blanc, prix en jaune et en gras (« Étoudi, 500 ! »).
            bubble.Text.text = $"{ShortName(client.DestinationStopId)}, <b><color=#FFD21A>{client.ProposedFare}</color></b> !";

            var fit = CabinPlan.Fit(Street.Shift.Cabin, client);
            bubble.Border.color = fit == SeatFit.Fits ? BubbleFits : fit == SeatFit.NeedsMove ? BubbleNeedsMove : BubbleNoRoom;

            // Icône de carrure : un buste plus ou moins large, à gauche du texte.
            var pad = font * 0.45f;
            var iconH = font * 0.9f;
            var iconW = iconH * CabinPlan.SilhouetteWidth(client.Corpulence) * 0.8f;
            var iconSlot = iconH * 0.8f;
            var textW = bubble.Text.preferredWidth;
            var size = new Vector2(pad + iconSlot + pad * 0.6f + textW + pad, font * 1.6f);
            var pointer = font * 0.45f;

            var pos = new Vector2(screen.x, screen.y + pointer * 0.7f);
            for (var guard = 0; guard < 10; guard++)
            {
                var r = new Rect(pos.x - size.x / 2f, pos.y, size.x, size.y);
                var overlap = false;
                foreach (var p in _placed)
                {
                    if (!p.Overlaps(r)) continue;
                    overlap = true;
                    pos.y = p.yMax + 2f;
                }
                if (!overlap) { _placed.Add(r); break; }
            }
            bubble.Root.sizeDelta = size;
            bubble.Root.anchoredPosition = pos;

            var pr = bubble.Pointer.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(pointer, pointer);
            pr.anchoredPosition = Vector2.zero;
            // Seule la bulle la plus proche garde sa pointe : les autres sont empilées au-dessus.
            bubble.Pointer.enabled = pos.y <= screen.y + pointer;

            var ir = bubble.Build.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(pad + iconSlot / 2f, 0f);
            ir.sizeDelta = new Vector2(iconW, iconH);

            var tr = bubble.Text.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(pad + iconSlot + pad * 0.6f, 0f);
            tr.sizeDelta = new Vector2(textW + 2f, size.y);
        }

        private void UpdateHud()
        {
            var h = Screen.height;
            var shift = Street.Shift;
            _gauges.Update(shift, shift.Ledger.Revenue, !Street.DayFinished);

            _toast.enabled = Time.time < _toastUntil;
            _toast.fontSize = Mathf.RoundToInt(h * 0.045f);
            // Sous la consigne du tutoriel quand elle est affichée.
            _toast.rectTransform.anchoredPosition = new Vector2(0f, _tutorialPanel.gameObject.activeSelf ? -h * 0.42f : -h * 0.30f);
            _toast.rectTransform.sizeDelta = new Vector2(h * 1.4f, h * 0.1f);
        }

        private void Toast(string text, float seconds = 3f)
        {
            _toast.text = text;
            _toastUntil = Time.time + seconds;
        }

        private string ShortName(string stopId) => StopNames.Short(Street.FindStop(stopId)?.name ?? stopId);

        private static string CorpulenceLabel(Client c) => c.Corpulence switch
        {
            Core.Rides.Corpulence.Mince => "mince",
            Core.Rides.Corpulence.Costaud => "costaud",
            _ => "carrure normale",
        };

        private static string Money(int amount) => DaySummary.Money(amount);
    }
}
