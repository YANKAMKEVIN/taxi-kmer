using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
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
        /// <summary>Sauvegarde (jalon 3, partie technique).</summary>
        public SaveService Save { get; private set; }

        private float[] _xz;
        private float[] _cum;
        private readonly Dictionary<RoadsideClient, Person> _people = new Dictionary<RoadsideClient, Person>();
        private readonly Dictionary<string, GameObject> _stopMarkers = new Dictionary<string, GameObject>();
        private readonly List<Bubble> _bubbles = new List<Bubble>();
        private readonly List<Rect> _placed = new List<Rect>();
        private readonly List<Image> _markerLabels = new List<Image>();
        private readonly Text[] _messageLines = new Text[2];
        private readonly MessageStack _messages = new MessageStack(2);
        private readonly List<(Text text, float start, int amount)> _pops = new List<(Text, float, int)>();
        private readonly RollingCounter _revenue = new RollingCounter(0);
        private bool _dayOverShown;
        private bool _lastHourShown;
        private Text _endReminder;
        // Panne sèche : choix « pousser » ou « appeler un technicien du quartier » (décision de Kevin, 2026-11-21).
        private Image _pushButton, _technicianButton;
        private Text _pushText, _technicianText;
        // Radio du taxi (radio.json ; créée seulement si enabled).
        private RadioView _radio;
        // Contrôles de police (police.json ; créés seulement si enabled, jalon 4 activé le 21/11).
        private PoliceView _police;
        /// <summary>Messages du renouvellement des papiers au début de la journée (décision de Kevin du 21/11).</summary>
        private readonly List<string> _paperNews = new List<string>();
        private float _paperNewsAt = -1f;
        private bool _policeWasActive;
        // Voiture de police de la poursuite (idée n° 9), créée au premier besoin.
        private GameObject _chaseCar;
        // Postes de contrôle dessinés dans le monde (tâche 4.5, primitives provisoires) : racine et policier de chaque poste.
        private readonly List<(Core.Police.ControlPost post, GameObject root, Transform officer)> _postViews = new List<(Core.Police.ControlPost, GameObject, Transform)>();
        // Passagers qui parlent (dialogues.json ; créés seulement si enabled, jalon 5 activé le 21/11).
        private TalkView _talk;
        // Ambiance sonore de la rue (ambience.json, jalon 5) : rumeur, marché, klaxons, appels des clients.
        private AmbienceView _ambience;
        // Cycle jour / nuit (daylight.json, idée n° 3).
        private DayLightView _dayLight;
        // La route est dure (road.json ; créée seulement si enabled, jalon 6 activé le 21/11).
        private RoadView _road;
        // Rédaction de la radio : événements du jeu → flashs d'information (décision de Kevin du 21/11).
        private readonly Core.Radio.RadioNewsDesk _news = new Core.Radio.RadioNewsDesk();
        private Stop[] _stopList = new Stop[0];
        private DaySummaryView _summaryView;
        private Tutorial _tutorial;
        private Image _tutorialPanel;
        private Text _tutorialText;
        private RouteStripView _strip;
        // Mini-carte GPS (minimap.json, idée n° 6) et ses repères de l'image en cours.
        private MiniMapView _miniMap;
        // Téléphone du chauffeur (phone.json, idée n° 11).
        private PhoneView _phone;
        // Pluie visible (rain.json, idée n° 12), créée seulement avec la route (road.json).
        private RainView _rain;
        // Événement de ville du jour (city_events.json, idée n° 14) et barrières du tronçon fermé.
        private CityEventDay _cityEvent = new CityEventDay(null);
        private GameObject _closure;
        private bool _cityEventWasActive;
        private readonly List<(float x, float z, Color color)> _mapMarkers = new List<(float, float, Color)>();
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
        /// <summary>Clé des préférences : vibrations (1 oui, 0 non).</summary>
        public const string VibrationKey = "reglages.vibrations";

        /// <summary>Clé des préférences : le tutoriel de la première journée a été suivi jusqu'au bout.</summary>
        public const string TutorialDoneKey = "tutoriel.fait";
        private MaterialPropertyBlock _block;
        private Camera _camera;
        private QualityView _quality;
        private bool _cityPopulated;
        private AccidentConfig _accidents;
        private Calendar _calendar;
        private FuelConfig _fuel;
        private bool _lowFuelShown, _emptyFuelShown;
        private StreetActionKind _actionKind;
        /// <summary>Jour de la semaine de la journée en cours (calendrier, jalon 3).</summary>
        public CalendarDay Today { get; private set; }
        private System.Random _random;

        /// <summary>Bulle d'un client : fond sombre arrondi, pointe, bordure (rentre / à déplacer / ne rentre pas), carrure, texte.</summary>
        private sealed class Bubble
        {
            public RectTransform Root;
            public Image Border;
            public Image Pointer;
            public Image Build;
            public Text Text;
        }

        private const int RingSegments = 16;

        private static readonly Color BubbleFits = new Color(0.3f, 0.85f, 0.4f);
        private static readonly Color BubbleNeedsMove = new Color(1f, 0.6f, 0.15f);
        private static readonly Color BubbleNoRoom = new Color(1f, 0.3f, 0.25f);

        private sealed class Person
        {
            public GameObject Root;
            public GameObject AcceptedMark;
            public Transform Ring;
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
            _stopList = stops.stops ?? new Stop[0];
            var fares = Load("Data/fares", FareConfig.Default());
            _fuel = Load("Data/fuel", new FuelConfig());
            var shift = new Shift(Load("Data/day", new DayConfig()), Load("Data/seating", new SeatingConfig()), _fuel);
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
            detector.VehicleHit += OnVehicleHit;
            Save = gameObject.AddComponent<SaveService>();
            Save.street = this;
            Save.taxi = taxi;
            Save.Initialize(actualSeed, Load("Data/save", new SaveConfig()), Load("Data/owner", new Core.Career.OwnerConfig()));
            // Calendrier : le jour de la semaine règle les clients et le trafic (dimanche plus calme, jours de marché).
            _calendar = new Calendar(Load("Data/calendar", new CalendarConfig()));
            Today = _calendar.For(Save.Career.DayNumber);
            // Carburant : le réservoir laissé la veille (ou celui du départ d'une carrière).
            shift.Tank.Set(Core.Save.SaveSnapshot.TankAtDayStart(Save.Data, _fuel));
            // Réputation : celle de la veille (visible ou cachée selon reputation.json, question ouverte de Kevin).
            Street.Reputation = Core.Save.SaveSnapshot.LoadReputation(Save.Data, Load("Data/reputation", new Core.Career.ReputationConfig()));
            Street.SpawnRateScale = Today.ClientsFactor;
            _cityEvent = CityEventDay.Draw(Load("Data/city_events", new CityEventsConfig()), Today, 0);
            if (_cityEvent.Any) _paperNews.Insert(0, _cityEvent.Event.announce);
            ApplyTrafficScale();
            var trafficView = gameObject.AddComponent<TrafficView>();
            trafficView.baseMaterial = personMaterial;
            trafficView.City = City;
            // Le taxi du joueur prend les mêmes détails que ceux de Yaoundé (plaques, baguette, pastille).
            trafficView.Decorate(taxi.transform, TaxiFactory.Length, TaxiFactory.Width, true, 0);

            foreach (var stop in stops.stops) _stopMarkers[stop.id] = MakeStopMarker(stop);
            if (_fuel.stations != null) foreach (var station in _fuel.stations) MakeStation(station);
            _sounds = taxi.GetComponent<TaxiSounds>();
            if (_sounds == null) _sounds = taxi.gameObject.AddComponent<TaxiSounds>(); // pas de « ?? » avec un objet Unity
            _tutorial = new Tutorial(PlayerPrefs.GetInt(TutorialDoneKey, 0) == 0, shift.Ledger.Versement);
            BuildHud();
            _gauges = new ShiftGaugesView(controls.Overlay, controls.Font);
            _pushButton = UiKit.NewButton(controls.Overlay, controls.Font, FuelGauge.PushLabel, new Color(0.3f, 0.3f, 0.35f, 0.95f), out _pushText);
            _technicianButton = UiKit.NewButton(controls.Overlay, controls.Font, "", new Color(0.12f, 0.55f, 0.25f, 0.95f), out _technicianText);
            _pushButton.gameObject.SetActive(false);
            _technicianButton.gameObject.SetActive(false);
            var radio = Load("Data/radio", new Core.Radio.RadioConfig());
            if (radio.enabled) _radio = new RadioView(controls.Overlay, controls.Font, taxi.gameObject, radio);
            var ambience = Load("Data/ambience", new AmbienceConfig());
            if (ambience.enabled)
            {
                var markets = new List<float>();
                foreach (var id in ambience.marketStops ?? new string[0])
                {
                    var stop = Street.FindStop(id);
                    if (stop != null) markets.Add(stop.distanceAlongRouteMeters);
                }
                _ambience = new AmbienceView(taxi.gameObject, ambience, markets.ToArray(), actualSeed + 67);
            }
            var daylight = Load("Data/daylight", new DayLightConfig());
            if (daylight.enabled) _dayLight = new DayLightView(daylight, world.LampHeads, taxi.gameObject);
            var police = Load("Data/police", new Core.Police.PoliceConfig());
            var dayCfg = Load("Data/day", new DayConfig());
            var dialogues = Load("Data/dialogues", new Core.Talk.DialogueConfig());
            if (dialogues.enabled)
            {
                _talk = new TalkView(controls.Overlay, controls.Font, taxi.gameObject, dialogues, actualSeed + 53);
                // Histoires des passagers réguliers (idée n° 13), suivies dans la sauvegarde.
                _talk.Director.Stories = new Core.Talk.StoryArcs(dialogues.arcs, Save.Data.career.stories, Save.Career.DayNumber);
                Street.FareDisputes = dialogues.FareRules();
                Street.DisputeRandom = new System.Random(actualSeed + 59);
            }
            var road = Load("Data/road", new Core.Road.RoadConfig());
            if (road.enabled)
                _road = new RoadView(controls.Overlay, controls.Font, road, _cum[_cum.Length - 1], Save.Data.career.vehicleDamage,
                    dayCfg.startHour * 60, dayCfg.endHour * 60, Today.Month, actualSeed + 61);
            var rainLook = Load("Data/rain", new Core.Road.RainLookConfig());
            if (_road != null && rainLook.enabled) _rain = new RainView(rainLook, world.transform, taxi.gameObject);
            if (police.enabled)
                _police = new PoliceView(controls.Overlay, controls.Font, police, Save.StartDayPapers(police, _paperNews), Save.Career.DayNumber, _cum[_cum.Length - 1],
                    dayCfg.startHour * 60, dayCfg.endHour * 60, actualSeed + 47);
            if (_police != null) foreach (var post in _police.Police.Schedule.Posts) _postViews.Add(MakePolicePost(post));
            _menu = new GameMenu(!TitleDone);
            _menuView = new GameMenuView(transform, controls.Font) { Career = Save.Data };
            _settings.Volume = PlayerPrefs.GetFloat(VolumeKey, GameSettings.DefaultVolume);
            AudioListener.volume = _settings.Volume;
            _settings.Vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            Haptics.Enabled = _settings.Vibration;
            _strip = new RouteStripView(controls.Overlay, controls.Font, stops.stops, _cum[_cum.Length - 1]);
            _cabinView = new CabinPlanView(controls.Overlay, controls.Font, stops.stops);
            var miniMap = Load("Data/minimap", new MiniMapConfig());
            if (miniMap.enabled) _miniMap = new MiniMapView(controls.Overlay, miniMap, world.Data);
            var phone = Load("Data/phone", new Core.Phone.PhoneConfig());
            if (phone.enabled)
            {
                _phone = new PhoneView(controls.Overlay, controls.Font);
                foreach (var m in Core.Phone.PhoneMessages.Morning(phone, Save.Data, Load("Data/owner", new Core.Career.OwnerConfig()), new System.Random(actualSeed + 71)))
                    _phone.Inbox.Receive(m);
            }
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
            var scale = (_quality != null ? _quality.Current.trafficScale : 1f) * (_calendar != null ? Today.TrafficFactor : 1f)
                * (_cityEvent != null ? _cityEvent.TrafficFactor(Street.Shift.Clock.GameMinutes) : 1f)
                * (_road != null ? _road.Road.Weather.TrafficFactor(Street.Shift.Clock.GameMinutes) : 1f);
            City.Traffic.DensityScale = scale;
            City.Pedestrians.DensityScale = scale;
        }

        /// <summary>Position du taxi par rapport à l'itinéraire, maintenant (pour la sauvegarde).</summary>
        public TaxiOnRoute TaxiOnRouteNow() => TaxiState();

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

            // Menu d'abord : la touche qui ouvre la pause ne doit pas la refermer dans la même image.
            // Le bilan passe devant tout (l'écran titre ne reste pas affiché par-dessus).
            var pending = _menu.Screen == MenuScreen.Pause && !Street.DayFinished ? CurrentPendingChoices() : default; // seulement en pause (projection du taxi)
            ApplyMenu(_menuView.Update(Street.DayFinished ? MenuScreen.None : _menu.Screen, _settings, shift.Cabin.PassengerCount,
                _police != null ? _police.Police.Papers.Summary(_police.Police.Day) : "", pending), pending);
            if (controls.PauseTriggered && !Street.DayFinished && _menu.Pause()) SetTimeStopped(true);
            controls.Locked = _menu.Frozen || _summaryView != null || (_police != null && _police.Active);

            if (!Street.DayFinished && !_menu.Frozen)
            {
                shift.Clock.Advance(dt);
                shift.Drive(Mathf.Abs(taxi.SpeedKmh) / 3.6f * dt);
            }
            UpdateFuel(shift.Tank);

            var state = TaxiState();
            Street.Update(dt, state);
            ApplyTrafficScale();
            if (!_cityPopulated)
            {
                // À la première image : le taxi est alors posé à son départ (TaxiCar.Start), ou repris de la sauvegarde.
                _cityPopulated = true;
                if (Save.TryResume())
                {
                    state = TaxiState();
                    Toast($"Jour {Save.Career.DayNumber} ({Today.Name}) repris à {Street.Shift.Clock.Display}", 4f);
                }
                else
                {
                    var start = Load("Data/day", new DayConfig());
                    var career = Save.StartedNewCareer ? "Nouvelle carrière. " : "";
                    Toast($"{career}Jour {Save.Career.DayNumber}, {Today.Name} : le patron te veut sur la route de {start.startHour} h à {start.endHour} h", 6f);
                    var news = _calendar.Announcement(Today);
                    if (news.Length > 0) Toast(news, 6f);
                    // Papiers renouvelés, ou restés expirés faute d'épargne : après les annonces du jour (pile de 2 messages).
                    if (_paperNews.Count > 0) _paperNewsAt = Time.time + 6f;
                }
                City.Populate(Hour, PlayerState());
            }
            City.Update(dt, Hour, PlayerState());
            foreach (var lost in Street.Lost) OnClientLost(lost);
            if (_paperNewsAt >= 0f && Time.time >= _paperNewsAt)
            {
                _paperNewsAt = -1f;
                foreach (var paper in _paperNews) Toast(paper, 7f);
            }

            if (controls.HornTriggered)
            {
                Haptics.Play(HapticEvent.Honk);
                OnHonk(state);
            }
            if (Street.DryTankLeft > 0) Toast(FuelGauge.PassengersLeftMessage(Street.DryTankLeft), 6f);
            if (_police != null)
            {
                var policeDone = _police.Update(Street, state, !_menu.Frozen && _summaryView == null);
                if (policeDone.Length > 0) Toast(policeDone, 6f);
                if (_police.Active && !_policeWasActive) _talk?.Say(Core.Talk.TalkTriggers.Control, TalkView.AnyPassenger(Street));
                _policeWasActive = _police.Active;
                UpdateChaseCar(state);
            }
            if (_road != null)
            {
                var roadSays = _road.Update(Street, state, !_menu.Frozen && _summaryView == null);
                if (roadSays.Length > 0) Toast(roadSays, 6f);
                if (_road.RainStarted) _talk?.Say(Core.Talk.TalkTriggers.Rain, TalkView.AnyPassenger(Street));
                if (_road.IncidentStarted) _talk?.Say(Core.Talk.TalkTriggers.Breakdown, TalkView.AnyPassenger(Street));
                Street.SpawnRateScale = Today.ClientsFactor * _road.Road.Weather.ClientsFactor(shift.Clock.GameMinutes);
            }
            UpdateCityEvent(shift.Clock.GameMinutes);
            if (_radio != null) UpdateRadioNews();
            if (_talk != null)
            {
                var said = _talk.Update(dt, Street, state, !_menu.Frozen && _summaryView == null);
                if (said.Length > 0) Toast(said, 5f);
            }
            UpdateDryTank(state);
            // La radio se tait en pause et au bilan ; elle baisse quand un passager parle (jalon 5).
            _radio?.Update(dt, !Street.DayFinished && !_menu.Frozen && _summaryView == null, _talk != null && _talk.Speaking);
            _dayLight?.Update(Street.Shift.Clock.GameMinutes, _quality != null && _quality.Level >= 2);
            _rain?.Update(dt, _road.Raining, _quality != null ? _quality.Level : 1, !_menu.Frozen && !Street.DayFinished);
            _ambience?.Update(dt, Street, state.Along, taxi.transform.position, City.Traffic.Vehicles, Today.Market,
                _talk != null && _talk.Speaking, !_menu.Frozen && _summaryView == null);
            var action = Street.AvailableAction(state);
            if (controls.ActionTriggered && action.Kind != StreetActionKind.None) OnAction(state);
            action = Street.AvailableAction(state);
            controls.ActionVisible = action.Kind != StreetActionKind.None;
            _actionKind = action.Kind;
            controls.ActionLabel = action.Kind switch
            {
                StreetActionKind.Drop => action.Count > 1 ? $"Faire descendre ({action.Count})" : "Faire descendre",
                StreetActionKind.Board => action.MoveFirst != null ? "Changer de place et monter" : "Faire monter",
                StreetActionKind.EndDay => "Terminer la journée",
                StreetActionKind.Refuel => FuelGauge.ActionLabel(action.FuelLitres, action.FuelCost),
                _ => "",
            };

            if (HudGauges.LastHour(shift.Clock) && !_lastHourShown && !Street.DayFinished)
            {
                _lastHourShown = true;
                Toast(HudGauges.LastHourMessage, 5f);
            }
            if (shift.Clock.IsOver && !_dayOverShown && !Street.DayFinished)
            {
                _dayOverShown = true;
                Toast("Fin de la journée : dépose tes passagers, ou arrête-toi pour terminer", 8f);
            }
            if (Street.DayFinished) ShowSummary();
            UpdateTutorial(dt, state);

            SyncPeople();
            SyncStopMarkers(state.Along);
            UpdateHud();
            _strip.Update(Street, state.Along, !Street.DayFinished);
            if (_miniMap != null) UpdateMiniMap();
            if (_talk?.Director.Stories != null && _talk.Director.Stories.Changed)
            {
                Save.Data.career.stories = _talk.Director.Stories.Save(); // écrit avec la prochaine sauvegarde
                _talk.Director.Stories.Changed = false;
            }
            _phone?.Update(!Street.DayFinished && !_menu.Frozen && (_police == null || !_police.Active));
            _cabinView.Update(Street.Shift.Cabin, !Street.DayFinished);
        }

        /// <summary>
        /// Choix qui attendent le joueur (contrôle, accrochage, négociation, refus de payer, panne sèche, crevaison, panne) :
        /// « Terminer la journée » est refusé tant qu'il y en a un (décision de Kevin du 23/11).
        /// </summary>
        private PendingChoices CurrentPendingChoices() =>
            Core.Ux.PendingChoices.Of(Street, TaxiState(), _police != null && _police.Active,
                _road != null ? _road.Crash.Pending : Core.Road.CrashChoice.None, _road?.Road.Car);

        private void ApplyMenu(MenuChoice choice, PendingChoices pending = default)
        {
            switch (choice)
            {
                case MenuChoice.StartDay:
                    _menu.StartDay();
                    _menuView.ConfirmingNewCareer = false;
                    TitleDone = true;
                    break;
                case MenuChoice.NewCareer:
                    // Idée n° 10 : second appui = carrière remise à zéro, puis la scène repart au volant (comme « Nouvelle journée »).
                    if (_menu.RequestNewCareer())
                    {
                        Save.StartNewCareer();
                        TitleDone = true;
                        SetTimeStopped(false);
                        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                        return;
                    }
                    _menuView.ConfirmingNewCareer = _menu.ConfirmingNewCareer;
                    break;
                case MenuChoice.Resume: _menu.Resume(); break;
                case MenuChoice.OpenSettings: _menu.OpenSettings(); _menuView.ConfirmingNewCareer = false; break;
                case MenuChoice.CloseSettings: _menu.CloseSettings(); break;
                case MenuChoice.EndDay:
                    if (_menu.EndDay(pending)) Street.EndDayNow(); // refusé tant qu'un choix attend (le bouton dit lequel)
                    break;
                case MenuChoice.ToggleVibration:
                    _settings.Vibration = !_settings.Vibration;
                    Haptics.Enabled = _settings.Vibration;
                    if (_settings.Vibration) Haptics.Play(HapticEvent.Board); // pour sentir que c'est revenu
                    PlayerPrefs.SetInt(VibrationKey, _settings.Vibration ? 1 : 0);
                    PlayerPrefs.Save();
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
                    // Le tutoriel parle de monter et descendre : le bouton de la station n'en fait pas partie.
                    ActionVisible = _actionKind == StreetActionKind.Board || _actionKind == StreetActionKind.Drop,
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
            City.Traffic.Honk(PlayerState()); // idée n° 15 : la voiture de devant se range
            var result = Street.Honk(state, out var client);
            if ((result == HonkResult.Accepted || result == HonkResult.AcceptedNeedsMove) && Street.PendingHaggle == null)
                _talk?.Say(Core.Talk.TalkTriggers.Price, client.Client.Id, TalkView.RideValues(Street, client.Client.DestinationStopId, client.Client.ProposedFare));
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
                    Haptics.Play(HapticEvent.Refused);
                    _cabinView.ShowRefused(client.Client);
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

        /// <summary>
        /// Le taxi a heurté un véhicule du trafic (jalon 6) : vitesse au choc et fautif d'après la simulation, puis la règle
        /// de Kevin (fuite option B si le joueur est fautif, poursuite si l'autre fuit) dans <see cref="RoadView"/>.
        /// </summary>
        private void OnVehicleHit(int vehicleId)
        {
            if (_road == null || Street == null || Street.DayFinished || City == null) return;
            foreach (var v in City.Traffic.Vehicles)
            {
                if (v.Id != vehicleId) continue;
                var said = _road.ReportCrash(Street, vehicleId, Core.Road.CollisionFault.Judge(PlayerState(), v));
                if (said.Length == 0) return;
                Haptics.Play(HapticEvent.Refused);
                Toast(said, 8f);
                return;
            }
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
            var candidate = Street.AvailableAction(state).Client;
            var outcome = Street.Perform(state);
            switch (outcome.Kind)
            {
                case StreetActionKind.Drop:
                    if (outcome.Paid > 0) StartPaymentPop(outcome.Paid);
                    _sounds.PlayDoor();
                    if (outcome.Paid > 0)
                    {
                        _sounds.PlayCoins();
                        Haptics.Play(HapticEvent.Payment);
                    }
                    foreach (var c in outcome.Dropped) { SpawnLeavingPerson(c); _talk?.Forget(c.Id); }
                    if (outcome.Refusal == null && outcome.Dropped != null && outcome.Dropped.Count > 0) _talk?.Say(Core.Talk.TalkTriggers.Paid, outcome.Dropped[0].Id, TalkView.RideValues(Street, outcome.Dropped[0].DestinationStopId, outcome.Dropped[0].ProposedFare));
                    break;
                case StreetActionKind.Refuel:
                    if (outcome.FuelLitres > 0f)
                    {
                        _sounds.PlayCoins();
                        Haptics.Play(HapticEvent.Payment);
                        Toast(FuelGauge.Bought(outcome.FuelLitres, outcome.FuelCost, Street.Shift.Tank));
                    }
                    break;
                case StreetActionKind.Board:
                    if (outcome.Board == BoardResult.Boarded)
                    {
                        _sounds.PlayDoor();
                        Haptics.Play(HapticEvent.Board);
                        var moved = outcome.Moved != null ? "Un passager a changé de place. " : "";
                        Toast($"{moved}Monté : {ShortName(outcome.Boarded.DestinationStopId)}, {Money(outcome.Boarded.ProposedFare)}");
                        if (outcome.Moved != null) _talk?.Say(Core.Talk.TalkTriggers.SeatMove, outcome.Moved.Id);
                        else
                        {
                            var values = TalkView.RideValues(Street, outcome.Boarded.DestinationStopId, outcome.Boarded.ProposedFare);
                            _talk?.Say(Core.Talk.TalkTriggers.Boarding, outcome.Boarded.Id, values);
                            _talk?.Director.Follow(Core.Talk.TalkTriggers.Destination, outcome.Boarded.Id, values); // il précise où il descend
                        }
                    }
                    else if (outcome.Board == BoardResult.DayOver) Toast("La journée est finie");
                    else
                    {
                        Toast("Il ne rentre pas");
                        Haptics.Play(HapticEvent.Refused);
                        if (candidate != null) _cabinView.ShowRefused(candidate.Client);
                    }
                    break;
            }
        }

        /// <summary>« vers Emana » : le lieu à clients le plus proche d'une position de l'itinéraire (pour les flashs).</summary>
        private string PlaceNear(float along)
        {
            Stop best = null;
            foreach (var st in _stopList)
                if (best == null || Mathf.Abs(st.distanceAlongRouteMeters - along) < Mathf.Abs(best.distanceAlongRouteMeters - along)) best = st;
            return best != null ? $"vers {ShortName(best.id)}" : "";
        }

        /// <summary>Flashs d'information : pluie, contrôles volants, heures de pointe, jour de marché (radio.json → flashes).</summary>
        /// <summary>Repères de la mini-carte : destinations des passagers, postes de police visibles, stations-service.</summary>
        private void UpdateMiniMap()
        {
            _mapMarkers.Clear();
            var seen = new HashSet<string>();
            foreach (var c in Street.Shift.Cabin.Passengers)
            {
                if (!seen.Add(c.DestinationStopId)) continue;
                var index = System.Array.FindIndex(_stopList, st => st.id == c.DestinationStopId);
                if (index >= 0) AddMapMarker(_stopList[index].distanceAlongRouteMeters, StopPalette.For(index));
            }
            if (_police != null)
                foreach (var post in _police.Police.Schedule.Posts)
                    if (Core.Police.PoliceMarkers.Visible(post, Street.Shift.Clock.GameMinutes)) AddMapMarker(post.Along, new Color(0.2f, 0.4f, 1f));
            if (_fuel.stations != null)
                foreach (var station in _fuel.stations) AddMapMarker(station.distanceAlongRouteMeters, new Color(0.2f, 0.85f, 0.35f));
            var forward = taxi.transform.forward;
            _miniMap.Update(taxi.transform.position, forward, _mapMarkers, !Street.DayFinished && !_menu.Frozen);
        }

        private void AddMapMarker(float along, Color color)
        {
            WorldGeometry.SampleAlong(_xz, _cum, along, out var x, out var z, out _, out _);
            _mapMarkers.Add((x, z, color));
        }

        private void UpdateRadioNews()
        {
            if (Street.DayFinished) return;
            var minute = Street.Shift.Clock.GameMinutes;
            var controls = new List<(string, string)>();
            if (_police != null)
                foreach (var p in _police.Police.Schedule.Posts)
                    if (p.Flying && p.ActiveAt(minute)) controls.Add((p.Id, PlaceNear(p.Along)));
            var raining = _road != null && _road.Raining;
            var jam = _stopList.Length > 0 ? PlaceNear(_stopList[_stopList.Length - 1].distanceAlongRouteMeters) : "";
            foreach (var item in _news.Update(minute, raining, Today.Market, _calendar.MarketName, controls, jam))
                _radio.Radio.PushEvent(item.Event, item.Place);
        }

        /// <summary>Réservoir : vitesse limitée quand il est vide, messages de réserve et de panne sèche (une fois chacun).</summary>
        private void UpdateFuel(FuelTank tank)
        {
            if (tank == null) return;
            taxi.SpeedCapKmh = Mathf.Min(tank.SpeedCapKmh, _road != null ? _road.Road.SpeedCapKmh : float.PositiveInfinity);
            if (Street.DayFinished) return;
            if (!tank.IsLow) _lowFuelShown = _emptyFuelShown = false;
            if (tank.IsLow && !_lowFuelShown) { _lowFuelShown = true; Toast(FuelGauge.LowMessage, 5f); }
            if (tank.IsEmpty && !_emptyFuelShown)
            {
                _emptyFuelShown = true;
                Toast(FuelGauge.EmptyMessage, 7f);
                _talk?.Say(Core.Talk.TalkTriggers.Breakdown, TalkView.AnyPassenger(Street));
                Haptics.Play(HapticEvent.Refused);
            }
        }

        /// <summary>
        /// Panne sèche hors de la station : deux boutons au milieu de l'écran (le taxi est arrêté), « Pousser le taxi »
        /// (au pas) ou « Appeler un technicien (prix, délai) » ; touches U et T au clavier (KeyMap). Toute la règle est dans Core.
        /// </summary>
        private void UpdateDryTank(TaxiOnRoute state)
        {
            if (_pushButton == null || _technicianButton == null) return;
            var show = !_menu.Frozen && _summaryView == null && Street.DryTankChoice(state);
            _pushButton.gameObject.SetActive(show);
            _technicianButton.gameObject.SetActive(show);
            if (!show) return;

            var h = Screen.height;
            var bw = h * 0.6f;
            var bh = h * 0.11f;
            var y = h * 0.55f;
            UiKit.PlaceTopCenter(_pushButton.rectTransform, y, bw, bh);
            _pushButton.rectTransform.anchoredPosition = new Vector2(-bw * 0.55f, -y);
            UiKit.PlaceTopCenter(_technicianButton.rectTransform, y, bw, bh);
            _technicianButton.rectTransform.anchoredPosition = new Vector2(bw * 0.55f, -y);
            _pushText.fontSize = _technicianText.fontSize = Mathf.RoundToInt(h * 0.032f);
            _technicianText.text = FuelGauge.TechnicianLabel(Street.TechnicianQuote());

            if (UiKit.Tapped(_pushButton.rectTransform) || UiKit.KeyDown(KeyAction.PushTaxi))
            {
                if (Street.PushTaxi()) Toast(FuelGauge.PushingMessage, 5f);
            }
            else if (UiKit.Tapped(_technicianButton.rectTransform) || UiKit.KeyDown(KeyAction.CallTechnician))
            {
                if (Street.CallTechnician(out var call))
                {
                    _sounds.PlayCoins();
                    Haptics.Play(HapticEvent.Payment);
                    Toast(FuelGauge.TechnicianCame(call), 6f);
                }
            }
            var stillShown = Street.DryTankChoice(state);
            _pushButton.gameObject.SetActive(stillShown);
            _technicianButton.gameObject.SetActive(stillShown);
        }

        /// <summary>Station-service provisoire (auvent et pompe en primitives) à droite de la route dans le sens vers Étoudi.</summary>
        private void MakeStation(StationData station)
        {
            WorldGeometry.SampleAlong(_xz, _cum, station.distanceAlongRouteMeters, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var root = new GameObject("Station " + station.name);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(new Vector3(x, 0f, z) + right * (sideOffset + 6f),
                Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up));
            var white = new Color(0.92f, 0.92f, 0.9f);
            var red = new Color(0.8f, 0.12f, 0.1f);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 4.2f, 0f), new Vector3(9f, 0.4f, 6f), white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 4.55f, 0f), new Vector3(9.1f, 0.3f, 6.1f), red);
            foreach (var px in new[] { -3.5f, 3.5f })
                Part(root.transform, PrimitiveType.Cube, new Vector3(px, 2f, 0f), new Vector3(0.3f, 4f, 0.3f), white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 1.6f, 0.5f), red);
            Part(root.transform, PrimitiveType.Cube, new Vector3(-5.5f, 3f, -2.5f), new Vector3(0.25f, 6f, 0.25f), white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(-5.5f, 6.2f, -2.5f), new Vector3(1.6f, 1.2f, 0.2f), red);
        }

        /// <summary>
        /// Poste de contrôle (tâche 4.5, primitives provisoires) à droite de la route dans le sens vers Étoudi : fixe =
        /// abri bleu, barrière rouge et blanche, plots ; volant = plots et pick-up de police. Un policier attend au bord
        /// de la chaussée (il s'avance quand il arrête le taxi). Règle d'affichage : <see cref="Core.Police.PoliceMarkers"/>.
        /// </summary>
        /// <summary>
        /// Événement de ville (idée n° 14) : clients en plus ou en moins, répliques dédiées, barrières de police en travers
        /// de la route fermée (avec collider : le taxi ne passe pas) pendant le cortège, message de fin.
        /// </summary>
        private void UpdateCityEvent(int minute)
        {
            if (!_cityEvent.Any) return;
            var active = _cityEvent.ActiveAt(minute);
            Street.SpawnRateScale *= _cityEvent.ClientsFactor(minute);
            if (_talk != null) _talk.Director.CityEvent = _cityEvent.ActiveId(minute);
            if (_cityEventWasActive && !active && _cityEvent.Event.endText.Length > 0) Toast(_cityEvent.Event.endText, 6f);
            _cityEventWasActive = active;
            if (!_cityEvent.HasClosure) return;
            if (_closure == null)
            {
                _closure = new GameObject("Route fermée : " + _cityEvent.Event.name);
                _closure.transform.SetParent(transform, false);
                foreach (var along in new[] { _cityEvent.Event.closedFromAlong, _cityEvent.Event.closedToAlong })
                {
                    WorldGeometry.SampleAlong(_xz, _cum, along, out var x, out var z, out var dx, out var dz);
                    var barrier = new GameObject("Barrière");
                    barrier.transform.SetParent(_closure.transform, false);
                    barrier.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up));
                    for (var i = -4; i <= 4; i++)
                        Part(barrier.transform, PrimitiveType.Cube, new Vector3(i * 1.6f, 0.6f, 0f), new Vector3(1.5f, 0.25f, 0.2f),
                            i % 2 == 0 ? new Color(0.8f, 0.12f, 0.1f) : new Color(0.92f, 0.92f, 0.9f));
                    var wall = barrier.AddComponent<BoxCollider>();
                    wall.center = new Vector3(0f, 0.8f, 0f);
                    wall.size = new Vector3(16f, 1.6f, 0.4f);
                }
            }
            _closure.SetActive(_cityEvent.ClosedAt(_cityEvent.Event.closedFromAlong, minute));
        }

        /// <summary>Voiture de police qui suit le taxi pendant une poursuite, sur la voie du taxi (primitives provisoires).</summary>
        private void UpdateChaseCar(TaxiOnRoute state)
        {
            var chase = _police.Chase;
            if (chase == null)
            {
                if (_chaseCar != null) _chaseCar.SetActive(false);
                return;
            }
            if (_chaseCar == null)
            {
                _chaseCar = new GameObject("Voiture de police");
                _chaseCar.transform.SetParent(transform, false);
                var blue = new Color(0.1f, 0.2f, 0.45f);
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 0.8f, 4.4f), blue);
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, -0.2f), new Vector3(1.6f, 0.55f, 2f), new Color(0.92f, 0.92f, 0.9f));
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(-0.3f, 1.7f, -0.2f), new Vector3(0.5f, 0.15f, 0.3f), new Color(0.9f, 0.1f, 0.1f));
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(0.3f, 1.7f, -0.2f), new Vector3(0.5f, 0.15f, 0.3f), new Color(0.1f, 0.3f, 1f));
            }
            _chaseCar.SetActive(true);
            WorldGeometry.SampleAlong(_xz, _cum, chase.PoliceAlong, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var forward = new Vector3(dx, 0f, dz) * state.Direction;
            _chaseCar.transform.SetPositionAndRotation(new Vector3(x, 0f, z) + right * state.Lateral, Quaternion.LookRotation(forward, Vector3.up));
        }

        private (Core.Police.ControlPost, GameObject, Transform) MakePolicePost(Core.Police.ControlPost post)
        {
            WorldGeometry.SampleAlong(_xz, _cum, post.Along, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var root = new GameObject(Core.Police.PoliceMarkers.Label(post) + " " + post.Id);
            root.transform.SetParent(transform, false);
            // Repère local : +x vers l'extérieur de la route, -x vers la chaussée, +z dans le sens de la marche.
            root.transform.SetPositionAndRotation(new Vector3(x, 0f, z) + right * (sideOffset + 2.5f),
                Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up));
            var blue = new Color(0.1f, 0.2f, 0.45f);
            var white = new Color(0.92f, 0.92f, 0.9f);
            var red = new Color(0.8f, 0.12f, 0.1f);
            var orange = new Color(1f, 0.45f, 0.05f);
            if (!post.Flying)
            {
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.5f, 1.3f, 0f), new Vector3(2.4f, 2.6f, 2.4f), blue);
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.5f, 2.75f, 0f), new Vector3(2.8f, 0.3f, 2.8f), white);
                Part(root.transform, PrimitiveType.Cube, new Vector3(0.6f, 0.6f, 3f), new Vector3(0.3f, 1.2f, 0.3f), white);
                for (var i = 0; i < 4; i++) // barrière rabattue le long du bord, bandes rouges et blanches
                    Part(root.transform, PrimitiveType.Cube, new Vector3(0.6f, 1.1f, 3.6f + i * 0.9f), new Vector3(0.15f, 0.15f, 0.9f), i % 2 == 0 ? red : white);
            }
            else
            {
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.2f, 0.8f, 0f), new Vector3(1.9f, 1f, 4.4f), blue);
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.2f, 1.6f, 0.8f), new Vector3(1.8f, 0.7f, 1.8f), blue);
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.2f, 2.05f, 0.8f), new Vector3(0.9f, 0.15f, 0.3f), red);
            }
            foreach (var cz in new[] { -6f, -3f, 3f }) // plots
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(-0.8f, 0.35f, cz), new Vector3(0.35f, 0.35f, 0.35f), orange);
            var officer = new GameObject("Policier").transform;
            officer.SetParent(root.transform, false);
            officer.localPosition = new Vector3(0f, 0f, -1f);
            Part(officer, PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.85f, 0.45f), new Color(0.25f, 0.3f, 0.2f));
            Part(officer, PrimitiveType.Sphere, new Vector3(0f, 1.82f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
            Part(officer, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(0.32f, 0.05f, 0.32f), blue); // képi
            root.SetActive(false);
            return (post, root, officer);
        }

        /// <summary>
        /// Postes de contrôle à chaque image : visibles selon <see cref="Core.Police.PoliceMarkers.Visible"/>, policier
        /// sur la chaussée pendant qu'il arrête le taxi, texte « Contrôle » au-dessus quand le taxi approche. Rend
        /// l'index du prochain texte de repère libre.
        /// </summary>
        private int SyncPolicePosts(int labelIndex, float along)
        {
            if (_police == null) return labelIndex;
            var minute = Street.Shift.Clock.GameMinutes;
            var current = _police.Active ? _police.Police.CurrentPost : null;
            foreach (var (post, root, officer) in _postViews)
            {
                var visible = Core.Police.PoliceMarkers.Visible(post, minute);
                root.SetActive(visible);
                if (!visible) continue;
                officer.localPosition = new Vector3(Core.Police.PoliceMarkers.Holding(post, current) ? -3f : 0f, 0f, -1f);
                if (!Street.DayFinished && Core.Police.PoliceMarkers.LabelShown(post, along))
                    ShowMarkerLabel(labelIndex++, root.transform.position + Vector3.up * 3.6f, Core.Police.PoliceMarkers.Label(post));
            }
            return labelIndex;
        }

        private void ShowSummary()
        {
            if (_summaryView != null) return;
            controls.Locked = true;
            controls.ActionVisible = false;
            _sounds.Muted = true;
            _messages.Clear();
            foreach (var p in _pops) Destroy(p.text.gameObject);
            _pops.Clear();
            if (_road != null) Save.Data.career.vehicleDamage = _road.Road.Car.Damage; // les dégâts passent d'un jour à l'autre
            var closure = Save.CloseDay();
            _summaryView = gameObject.AddComponent<DaySummaryView>();
            var reputation = Street.Reputation != null && Street.Reputation.Visible
                ? Core.Career.DriverReputation.SummaryLine(Street.Reputation.Config, Street.Summary.Reputation, Street.Summary.ReputationChange) : "";
            _summaryView.Show(Street.Summary, closure, Street.Shift.Clock.Display, controls.Font, Today.Name, reputation);
            _summaryView.NewCareerRequested += Save.StartNewCareer;
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
                var accepted = c.State == RoadsideState.Accepted;
                person.AcceptedMark.SetActive(accepted);
                if (accepted)
                {
                    var k = WorldMarkers.RingPulse(Time.time);
                    person.Ring.localScale = new Vector3(k, 1f, k);
                }
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
            // Client accepté : anneau jaune qui pulse au sol sous lui, et zone d'arrêt dessinée sur la voie devant lui
            // (le taxi s'y arrête pour le faire monter ; local +z = vers la route).
            var mark = new GameObject("Client accepté");
            mark.transform.SetParent(root.transform, false);
            var yellow = new Color(1f, 0.8f, 0.05f);
            var ring = new GameObject("Anneau").transform;
            ring.SetParent(mark.transform, false);
            for (var i = 0; i < RingSegments; i++)
            {
                var a = i * 360f / RingSegments;
                var seg = Part(ring, PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.03f, 0.8f),
                    new Vector3(0.34f, 0.02f, 0.12f), yellow);
                seg.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            Part(mark.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, sideOffset - taxi.laneOffset), new Vector3(8f, 0.02f, 2.4f),
                new Color(1f, 0.85f, 0.2f));
            mark.SetActive(false);
            return new Person { Root = root, AcceptedMark = mark, Ring = ring };
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

        private void SyncStopMarkers(float taxiAlong)
        {
            var shown = 0;
            foreach (var kv in _stopMarkers)
            {
                var count = Street.Shift.Cabin.PassengersFor(kv.Key).Count;
                kv.Value.SetActive(count > 0);
                if (count == 0 || Street.DayFinished) continue;
                ShowMarkerLabel(shown++, kv.Value.transform.position + Vector3.up * 6.8f,
                    WorldMarkers.DestinationLabel(Street.FindStop(kv.Key)?.name ?? kv.Key, count));
            }
            shown = SyncPolicePosts(shown, taxiAlong);
            for (var i = shown; i < _markerLabels.Count; i++) _markerLabels[i].gameObject.SetActive(false);
        }

        /// <summary>Nom du lieu et nombre de passagers qui y descendent, au-dessus de la colonne (texte d'écran).</summary>
        private void ShowMarkerLabel(int index, Vector3 world, string text)
        {
            while (_markerLabels.Count <= index)
            {
                var bg = UiKit.NewImage(controls.Overlay, "Nom du repère", new Color(0.05f, 0.25f, 0.1f, 0.85f), UiKit.Rounded);
                var r = bg.rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = new Vector2(0.5f, 0f);
                var t = UiKit.NewText(bg.transform, controls.Font, TextAnchor.MiddleCenter, Color.white);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Fill(t.rectTransform);
                _markerLabels.Add(bg);
            }
            var label = _markerLabels[index];
            var screen = _camera != null ? _camera.WorldToScreenPoint(world) : Vector3.back;
            var visible = screen.z > 0f;
            label.gameObject.SetActive(visible);
            if (!visible) return;
            var h = Screen.height;
            var text2 = label.GetComponentInChildren<Text>();
            text2.text = text;
            text2.fontSize = Mathf.RoundToInt(h * Mathf.Clamp(1.2f / screen.z, 0.026f, 0.04f));
            label.rectTransform.sizeDelta = new Vector2(text2.preferredWidth + h * 0.03f, text2.fontSize * 1.6f);
            label.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y);
        }

        // ---------- Interface ----------

        private void BuildHud()
        {
            for (var i = 0; i < _messageLines.Length; i++)
            {
                _messageLines[i] = NewText(TextAnchor.MiddleCenter);
                var tr = _messageLines[i].rectTransform;
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
            }

            // Rappel de fin de journée, en petit en bas au centre (entre les flèches et les pédales).
            _endReminder = NewText(TextAnchor.LowerCenter);
            _endReminder.color = new Color(1f, 0.75f, 0.35f);
            _endReminder.horizontalOverflow = HorizontalWrapMode.Wrap;
            var er = _endReminder.rectTransform;
            er.anchorMin = er.anchorMax = er.pivot = new Vector2(0.5f, 0f);

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
            UpdatePaymentPops();
            _gauges.Update(shift, Calendar.HudLabel(Today), _revenue.Shown, !Street.DayFinished,
                Street.Reputation != null && Street.Reputation.Visible ? Street.Reputation.HudLabel : "");

            var reminder = Street.DayFinished ? "" : HudGauges.EndOfDayReminder(shift.Clock, shift.Cabin.PassengerCount);
            _endReminder.enabled = reminder.Length > 0;
            _endReminder.text = reminder;
            _endReminder.fontSize = Mathf.RoundToInt(h * 0.03f);
            _endReminder.rectTransform.anchoredPosition = new Vector2(0f, h * 0.07f);
            _endReminder.rectTransform.sizeDelta = new Vector2(Mathf.Max(h * 0.4f, Screen.width - h * 1.2f), h * 0.1f);

            // Messages empilés sur deux lignes (le plus récent en haut), sous la consigne du tutoriel si elle est affichée.
            var lines = Street.DayFinished ? new List<string>() : new List<string>(_messages.Visible(Time.time));
            var top = _tutorialPanel.gameObject.activeSelf ? h * 0.42f : h * 0.30f;
            for (var i = 0; i < _messageLines.Length; i++)
            {
                var line = _messageLines[i];
                line.enabled = i < lines.Count;
                if (!line.enabled) continue;
                line.text = lines[i];
                line.fontSize = Mathf.RoundToInt(h * (i == 0 ? 0.045f : 0.036f));
                line.color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                line.rectTransform.anchoredPosition = new Vector2(0f, -(top + i * h * 0.06f));
                line.rectTransform.sizeDelta = new Vector2(h * 1.4f, h * 0.06f);
            }
        }

        private void Toast(string text, float seconds = 3f) => _messages.Push(text, seconds, Time.time);

        /// <summary>Le montant payé jaillit du taxi en jaune, grossit, puis file vers le compteur de recette.</summary>
        private void StartPaymentPop(int amount)
        {
            var t = NewText(TextAnchor.MiddleCenter);
            t.text = $"+{Money(amount)}";
            t.color = new Color(1f, 0.85f, 0.15f);
            t.fontStyle = FontStyle.Bold;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            _pops.Add((t, Time.time, amount));
            UpdatePaymentPops();
        }

        private void UpdatePaymentPops()
        {
            var h = Screen.height;
            var from = _camera != null ? (Vector2)_camera.WorldToScreenPoint(taxi.transform.position + Vector3.up * 2.2f) : new Vector2(Screen.width / 2f, h / 2f);
            var to = _gauges.RevenueScreenPoint;
            for (var i = _pops.Count - 1; i >= 0; i--)
            {
                var (text, start, amount) = _pops[i];
                var elapsed = Time.time - start;
                if (PaymentPop.Arrived(elapsed))
                {
                    _revenue.SetTarget(_revenue.Target + amount);
                    Destroy(text.gameObject);
                    _pops.RemoveAt(i);
                    continue;
                }
                PaymentPop.Sample(elapsed, out var scale, out var travel);
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(h * 0.06f * scale));
                text.rectTransform.sizeDelta = new Vector2(h * 0.6f, h * 0.1f);
                text.rectTransform.anchoredPosition = Vector2.Lerp(from, to, travel);
            }
            // Recette changée sans animation (ex. tests) : le compteur la rattrape.
            if (_pops.Count == 0 && _revenue.Target != Street.Shift.Ledger.Revenue) _revenue.SetTarget(Street.Shift.Ledger.Revenue);
            _revenue.Step(Time.deltaTime);
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
