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
    public sealed partial class StreetView : MonoBehaviour
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
        private TrafficView _trafficView;
        // Textes de l'interface (ui_texts.json, idée n° 32).
        private UiTextsConfig _texts = new UiTextsConfig();
        private PhotoModeView _photo;
        // Petits objectifs du jour (goals.json, idée n° 25).
        private DailyGoals _goals;
        // Vendeurs ambulants aux feux (vendors.json, idée n° 24).
        private StreetVendors _vendors;
        private readonly List<(int id, float along)> _lightAlongs = new List<(int, float)>();
        private GameObject _vendor;
        // Construction progressive du monde (idée n° 21) : écran de chargement, puis les vues qui ont besoin des maillages.
        private LoadingView _loading;
        private DayLightConfig _daylightConfig;
        private Core.Road.RainLookConfig _rainConfig;
        // Dégâts visibles sur le taxi (idée n° 17).
        private DamageView _damage;
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

        // Couleurs des bulles : vert / orange / rouge, ou bleu / jaune / orange pour les daltoniens (idée n° 29).
        private static Color BubbleFits => UiKit.Palette(HudPalette.Good(UiKit.ColorBlind));
        private static Color BubbleNeedsMove => UiKit.Palette(HudPalette.Warning(UiKit.ColorBlind));
        private static Color BubbleNoRoom => UiKit.Palette(HudPalette.Bad(UiKit.ColorBlind));

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
            _texts = Load("Data/ui_texts", new UiTextsConfig());
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
            _trafficView = trafficView;
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
            _daylightConfig = Load("Data/daylight", new DayLightConfig());
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
            _rainConfig = Load("Data/rain", new Core.Road.RainLookConfig());
            if (_road != null) _damage = new DamageView(road, taxi.gameObject);
            if (police.enabled)
                _police = new PoliceView(controls.Overlay, controls.Font, police, Save.StartDayPapers(police, _paperNews), Save.Career.DayNumber, _cum[_cum.Length - 1],
                    dayCfg.startHour * 60, dayCfg.endHour * 60, actualSeed + 47);
            if (_police != null) foreach (var post in _police.Police.Schedule.Posts) _postViews.Add(MakePolicePost(post));
            _menu = new GameMenu(!TitleDone);
            _menuView = new GameMenuView(transform, controls.Font) { Career = Save.Data };
            _photo = new PhotoModeView(transform, controls.Font);
            _settings.Load(PrefsStore.Instance); // mêmes clés qu'avant pour le volume et les vibrations
            AudioListener.volume = _settings.Volume;
            Haptics.Enabled = _settings.Vibration;
            ApplyAccessibility();
            _strip = new RouteStripView(controls.Overlay, controls.Font, stops.stops, _cum[_cum.Length - 1]);
            _cabinView = new CabinPlanView(controls.Overlay, controls.Font, stops.stops);
            if (!world.Ready)
            {
                _loading = new LoadingView(transform, controls.Font);
                Time.timeScale = 0f; // rien ne bouge tant que le monde se construit
            }
            else OnWorldReady();
            _goals = new DailyGoals(Load("Data/goals", new DailyGoalsConfig()), Save.Career.DayNumber, 0);
            var vendors = Load("Data/vendors", new VendorsConfig());
            if (vendors.enabled)
            {
                _vendors = new StreetVendors(vendors, new System.Random(actualSeed + 83));
                foreach (var l in world.Data.traffic?.lights ?? new TrafficLightData[0])
                {
                    WorldGeometry.Project(_xz, _cum, l.x, l.z, out var lightAlong, out _);
                    _lightAlongs.Add((l.id, lightAlong));
                }
            }
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

        /// <summary>Le monde est construit : vues qui ont besoin de ses maillages (lampadaires, chaussées).</summary>
        private void OnWorldReady()
        {
            if (_daylightConfig != null && _daylightConfig.enabled) _dayLight = new DayLightView(_daylightConfig, world.LampHeads, taxi.gameObject);
            if (_road != null && _rainConfig != null && _rainConfig.enabled) _rain = new RainView(_rainConfig, world.transform, taxi.gameObject);
        }

        private void Update()
        {
            if (_loading != null)
            {
                if (!world.Ready)
                {
                    _loading.Update(world.Progress);
                    return;
                }
                _loading.Close();
                _loading = null;
                Time.timeScale = 1f;
                OnWorldReady();
            }
            var dt = Time.deltaTime;
            var shift = Street.Shift;

            // Menu d'abord : la touche qui ouvre la pause ne doit pas la refermer dans la même image.
            // Le bilan passe devant tout (l'écran titre ne reste pas affiché par-dessus).
            var pending = _menu.Screen == MenuScreen.Pause && !Street.DayFinished ? CurrentPendingChoices() : default; // seulement en pause (projection du taxi)
            ApplyMenu(_menuView.Update(Street.DayFinished ? MenuScreen.None : _menu.Screen, _settings, shift.Cabin.PassengerCount,
                PauseNote(), pending), pending);
            if (controls.PauseTriggered && !Street.DayFinished && _menu.Pause()) SetTimeStopped(true);
            // Mode photo (idée n° 27) : HUD masqué, caméra libre ; « Retour » revient à la pause.
            if (_photo.Update(_menu.Screen == MenuScreen.Photo, taxi.transform, controls.Overlay.gameObject, Save.Career.DayNumber, Street.Shift.Clock.Display))
                ApplyMenu(MenuChoice.ClosePhoto);
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
                    Toast(UiText.Fill(_texts.dayResumed, "jour", Save.Career.DayNumber.ToString(), "semaine", Today.Name, "heure", Street.Shift.Clock.Display), 4f);
                }
                else
                {
                    var start = Load("Data/day", new DayConfig());
                    var career = Save.StartedNewCareer ? _texts.newCareer : "";
                    Toast(UiText.Fill(_texts.dayStart, "carriere", career, "jour", Save.Career.DayNumber.ToString(), "semaine", Today.Name,
                        "debut", start.startHour.ToString(), "fin", start.endHour.ToString()), 6f);
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
                if (_road.PotholeHit) ChaseCam()?.PotholeShake();
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
            if (_trafficView != null && _quality != null) _trafficView.ShadowMode = _quality.Current.trafficShadows; // idée n° 20
            _dayLight?.Update(Street.Shift.Clock.GameMinutes, _quality != null && _quality.Level >= 2);
            _damage?.Update(_road.Road.Car.Damage);
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
                StreetActionKind.Drop => action.Count > 1 ? UiText.Fill(_texts.dropMany, "n", action.Count.ToString()) : _texts.dropOne,
                StreetActionKind.Board => action.MoveFirst != null ? _texts.moveAndBoard : _texts.board,
                StreetActionKind.EndDay => _texts.endDay,
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
                Toast(_texts.dayOverDropPassengers, 8f);
            }
            if (Street.DayFinished) ShowSummary();
            UpdateTutorial(dt, state);

            SyncPeople();
            SyncStopMarkers(state.Along);
            UpdateHud();
            _strip.Update(Street, state.Along, !Street.DayFinished);
            if (_miniMap != null) UpdateMiniMap();
            if (_vendors != null && !_menu.Frozen && !Street.DayFinished) UpdateVendor(dt, state);
            if (_goals != null && !Street.DayFinished) UpdateGoals();
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
                case MenuChoice.OpenPhoto: _menu.OpenPhoto(); break;
                case MenuChoice.ClosePhoto: _menu.Back(); break;
                case MenuChoice.Resume: _menu.Resume(); break;
                case MenuChoice.OpenSettings: _menu.OpenSettings(); _menuView.ConfirmingNewCareer = false; break;
                case MenuChoice.CloseSettings: _menu.CloseSettings(); break;
                case MenuChoice.EndDay:
                    if (_menu.EndDay(pending)) Street.EndDayNow(); // refusé tant qu'un choix attend (le bouton dit lequel)
                    break;
                case MenuChoice.SettingRow:
                    _settings.Cycle(_menuView.RowId);
                    Haptics.Enabled = _settings.Vibration;
                    if (_menuView.RowId == SettingIds.Vibration && _settings.Vibration) Haptics.Play(HapticEvent.Board);
                    _settings.Save(PrefsStore.Instance);
                    PlayerPrefs.Save();
                    ApplyAccessibility();
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

        private void OnDestroy()
        {
            SetTimeStopped(false);
            if (_loading != null) Time.timeScale = 1f; // scène quittée pendant le chargement
        }

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
                    Toast(UiText.Fill(_texts.honkAccepted, "lieu", ShortName(client.Client.DestinationStopId), "prix", Money(client.Client.ProposedFare)));
                    break;
                case HonkResult.AcceptedNeedsMove:
                    Toast(_texts.honkNeedsMove);
                    break;
                case HonkResult.NoRoom:
                    Toast(UiText.Fill(_texts.honkNoRoom, "carrure", CorpulenceLabel(client.Client)));
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
            var who = outcome.Severity == InjurySeverity.Light ? _texts.pedestrianLight : _texts.pedestrianSerious;
            var hours = outcome.LostGameMinutes >= 60 ? $"{outcome.LostGameMinutes / 60} h {outcome.LostGameMinutes % 60:00}" : $"{outcome.LostGameMinutes} min";
            Toast(UiText.Fill(_texts.pedestrianHit, "blessure", who, "prix", Money(outcome.CareCost), "duree", hours), 9f);
        }

        /// <summary>
        /// Le taxi a heurté un véhicule du trafic (jalon 6) : vitesse au choc et fautif d'après la simulation, puis la règle
        /// de Kevin (fuite option B si le joueur est fautif, poursuite si l'autre fuit) dans <see cref="RoadView"/>.
        /// </summary>
        private ChaseCamera ChaseCam() => _camera != null ? _camera.GetComponent<ChaseCamera>() : null;

        private void OnVehicleHit(int vehicleId)
        {
            _goals?.Crash();
            ChaseCam()?.CrashShake(); // idée n° 16
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
                Toast(_texts.clientGaveUp);
                return;
            }
            if (lost.Reason != ClientLostReason.TookOtherTaxi) return;
            if (lost.WasAccepted)
            {
                Toast(_texts.clientTakenByMoto);
                return;
            }
            var place = City.Place(lost.Client);
            var d = new Vector2(place.X - taxi.transform.position.x, place.Z - taxi.transform.position.z).magnitude;
            var who = lost.ByMoto ? _texts.rivalMoto : _texts.rivalTaxi;
            if (d < 120f) Toast(UiText.Fill(_texts.clientTakenNearby, "qui", who, "lieu", ShortName(lost.Client.Client.DestinationStopId)));
        }

        private void OnAction(TaxiOnRoute state)
        {
            var candidate = Street.AvailableAction(state).Client;
            var outcome = Street.Perform(state);
            switch (outcome.Kind)
            {
                case StreetActionKind.Drop:
                    if (outcome.Paid > 0) StartPaymentPop(outcome.Paid);
                    if (outcome.Paid > 0) _goals?.RidePaid(Street.Shift.Clock.GameMinutes);
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
                        var moved = outcome.Moved != null ? _texts.passengerMoved : "";
                        Toast(UiText.Fill(_texts.boarded, "deplace", moved, "lieu", ShortName(outcome.Boarded.DestinationStopId), "prix", Money(outcome.Boarded.ProposedFare)));
                        if (outcome.Moved != null) _talk?.Say(Core.Talk.TalkTriggers.SeatMove, outcome.Moved.Id);
                        else
                        {
                            var values = TalkView.RideValues(Street, outcome.Boarded.DestinationStopId, outcome.Boarded.ProposedFare);
                            _talk?.Say(Core.Talk.TalkTriggers.Boarding, outcome.Boarded.Id, values);
                            _talk?.Director.Follow(Core.Talk.TalkTriggers.Destination, outcome.Boarded.Id, values); // il précise où il descend
                        }
                    }
                    else if (outcome.Board == BoardResult.DayOver) Toast(_texts.dayIsOver);
                    else
                    {
                        Toast(_texts.doesNotFit);
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
            return best != null ? UiText.Fill(_texts.towards, "lieu", ShortName(best.id)) : "";
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
            _goals?.EndDay();
            if (_goals != null && _goals.Reward > 0) Save.Career.AddBonus(_goals.Reward); // prime, si Kevin l'active
            var closure = Save.CloseDay();
            _summaryView = gameObject.AddComponent<DaySummaryView>();
            var reputation = Street.Reputation != null && Street.Reputation.Visible
                ? Core.Career.DriverReputation.SummaryLine(Street.Reputation.Config, Street.Summary.Reputation, Street.Summary.ReputationChange) : "";
            var goals = _goals != null ? _goals.Summary() : "";
            if (goals.Length > 0) reputation = reputation.Length > 0 ? reputation + "\n" + goals.Replace("\n", " · ") : goals.Replace("\n", " · ");
            _summaryView.Show(Street.Summary, closure, Street.Shift.Clock.Display, controls.Font, Today.Name, reputation);
            _summaryView.NewCareerRequested += Save.StartNewCareer;
        }

        /// <summary>L'écran de bilan (null tant que la journée n'est pas terminée).</summary>
        public DaySummaryView SummaryView => _summaryView;

        private string ShortName(string stopId) => StopNames.Short(Street.FindStop(stopId)?.name ?? stopId);

        private string CorpulenceLabel(Client c) => c.Corpulence switch
        {
            Core.Rides.Corpulence.Mince => _texts.slim,
            Core.Rides.Corpulence.Costaud => _texts.big,
            _ => _texts.normalBuild,
        };

        private static string Money(int amount) => DaySummary.Money(amount);
    }
}
