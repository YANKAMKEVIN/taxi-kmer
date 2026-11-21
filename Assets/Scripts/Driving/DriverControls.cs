using System.Collections.Generic;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Commandes du joueur : écran tactile (flèches à gauche, pédales à droite, klaxon en haut à droite, glisser ailleurs
    /// pour tourner la caméra), souris et clavier pour l'éditeur. Dessine les boutons et le compteur de vitesse.
    /// Clavier : ← → / Q D / A (tourner), ↑ / Z / W (accélérer), ↓ / S / espace (frein, marche arrière), H (klaxon),
    /// E ou Entrée (bouton contextuel), R (remettre sur la route), Échap ou Retour Android (pause),
    /// G (qualité graphique suivante).
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class DriverControls : MonoBehaviour
    {
        public TaxiCar taxi;
        public TaxiHorn horn;
        public ChaseCamera chaseCamera;

        private ControlLayout _layout;
        private readonly Dictionary<int, DriveControl> _fingerStart = new Dictionary<int, DriveControl>();
        private DriveControl _mouseStart;
        private Vector3 _lastMouse;

        private RectTransform _canvas;
        private readonly List<(ControlZone zone, Image image, RectTransform rect)> _buttons = new List<(ControlZone, Image, RectTransform)>();
        private Text _speed;
        private Image _reverseLight;
        private readonly Dictionary<DriveControl, Image> _icons = new Dictionary<DriveControl, Image>();
        private Text _actionLabel;
        private Text _qualityLabel;
        private Vector2Int _laidOutFor;
        private DriveInput _previous;

        /// <summary>Le bouton contextuel est affiché (posé par le jeu, ex. StreetView).</summary>
        public bool ActionVisible { get; set; }
        /// <summary>Texte du bouton contextuel (« Faire monter », « Faire descendre (2) »…).</summary>
        public string ActionLabel { get; set; } = "";
        /// <summary>Le bouton contextuel vient d'être pressé (cette image seulement).</summary>
        public bool ActionTriggered { get; private set; }
        /// <summary>Le klaxon vient d'être pressé (cette image seulement).</summary>
        public bool HornTriggered { get; private set; }
        /// <summary>Le bouton pause (ou Échap / Retour Android) vient d'être pressé (cette image seulement).</summary>
        public bool PauseTriggered { get; private set; }
        /// <summary>Le bouton de qualité vient d'être pressé (cette image seulement) ; lu par <see cref="QualityView"/>.</summary>
        public bool QualityTriggered { get; private set; }
        /// <summary>Texte du bouton de qualité (« Qualité : Moyen · 30 i/s »), posé par <see cref="QualityView"/>.</summary>
        public string QualityLabel { get; set; } = "Qualité";
        /// <summary>Calque d'interface où le jeu peut ajouter ses propres textes.</summary>
        public RectTransform Overlay => _canvas;
        public Font Font { get; private set; }
        /// <summary>Commandes bloquées (écran de bilan) : le taxi ne reçoit plus rien et les boutons sont cachés.</summary>
        public bool Locked { get; set; }
        /// <summary>Commande mise en avant (tutoriel) : son bouton pulse en jaune. None : aucune.</summary>
        public DriveControl Highlight { get; set; }

        private static readonly Color Idle = new Color(0f, 0f, 0f, 0.28f);
        private static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color Pressed = new Color(1f, 0.82f, 0.1f, 0.55f);
        private static readonly Color ActionColor = new Color(0.1f, 0.45f, 0.2f, 0.75f);
        private static readonly Color HighlightColor = new Color(1f, 0.82f, 0.1f, 0.75f);

        private void Awake()
        {
            UnityEngine.Input.simulateMouseWithTouches = false;
            UnityEngine.Input.multiTouchEnabled = true;
            _layout = DriveData.Load().layout;
            BuildView();
            gameObject.AddComponent<QualityView>().controls = this;
        }

        private void Update()
        {
            var input = ReadKeyboard() | ReadPointers();
            if (Locked) input = default;
            if (!ActionVisible) input.Action = false;
            ActionTriggered = input.Action && !_previous.Action;
            HornTriggered = input.Horn && !_previous.Horn;
            PauseTriggered = input.Pause && !_previous.Pause;
            QualityTriggered = input.Quality && !_previous.Quality;
            _previous = input;
            if (taxi != null)
            {
                taxi.Controls = input;
                if (!Locked && UnityEngine.Input.GetKeyDown(KeyCode.R)) taxi.ResetToRoute();
            }
            if (horn != null) horn.SetPressed(input.Horn);
            UpdateView(input);
        }

        private static DriveInput ReadKeyboard()
        {
            return new DriveInput
            {
                Left = Key(KeyCode.LeftArrow) || Key(KeyCode.Q) || Key(KeyCode.A),
                Right = Key(KeyCode.RightArrow) || Key(KeyCode.D),
                Accelerate = Key(KeyCode.UpArrow) || Key(KeyCode.Z) || Key(KeyCode.W),
                Brake = Key(KeyCode.DownArrow) || Key(KeyCode.S) || Key(KeyCode.Space),
                Horn = Key(KeyCode.H),
                Action = Key(KeyCode.E) || Key(KeyCode.Return),
                Pause = Key(KeyCode.Escape), // aussi le bouton Retour d'Android
                Quality = Key(KeyCode.G),
            };
        }

        private static bool Key(KeyCode k) => UnityEngine.Input.GetKey(k);

        private DriveInput ReadPointers()
        {
            var input = new DriveInput();
            float w = Screen.width, h = Screen.height;

            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                var p = t.position;
                if (t.phase == TouchPhase.Began) _fingerStart[t.fingerId] = _layout.ControlAt(p.x, p.y, w, h, ActionVisible);
                if (!_fingerStart.TryGetValue(t.fingerId, out var start)) start = _fingerStart[t.fingerId] = _layout.ControlAt(p.x, p.y, w, h, ActionVisible);

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _fingerStart.Remove(t.fingerId);
                    continue;
                }
                if (start == DriveControl.None)
                {
                    // Doigt posé hors des boutons : il tourne la caméra jusqu'à ce qu'on le lève.
                    if (chaseCamera != null) chaseCamera.Drag(t.deltaPosition.x / w, t.deltaPosition.y / w);
                }
                else
                {
                    // Doigt posé sur un bouton : on peut glisser d'une flèche à l'autre.
                    input.Press(_layout.ControlAt(p.x, p.y, w, h, ActionVisible));
                }
            }

            if (UnityEngine.Input.touchCount == 0)
            {
                var m = UnityEngine.Input.mousePosition;
                if (UnityEngine.Input.GetMouseButtonDown(0)) { _mouseStart = _layout.ControlAt(m.x, m.y, w, h, ActionVisible); _lastMouse = m; }
                if (UnityEngine.Input.GetMouseButton(0))
                {
                    if (_mouseStart == DriveControl.None)
                    {
                        if (chaseCamera != null) chaseCamera.Drag((m.x - _lastMouse.x) / w, (m.y - _lastMouse.y) / w);
                    }
                    else input.Press(_layout.ControlAt(m.x, m.y, w, h, ActionVisible));
                }
                _lastMouse = m;
            }
            return input;
        }

        private void BuildView()
        {
            var canvasGo = new GameObject("Commandes", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            _canvas = (RectTransform)canvasGo.transform;
            var font = Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (var zone in _layout.zones)
            {
                var go = new GameObject(zone.control.ToString(), typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_canvas, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.color = Idle;
                img.raycastTarget = false;
                img.sprite = UiKit.Rounded;
                img.type = Image.Type.Sliced;
                // Contour clair : le bouton reste visible sur une route claire.
                var outline = UiKit.NewImage(rect, "Contour", OutlineColor, UiKit.Frame);
                UiKit.Fill(outline.rectTransform);

                // Pictogramme dessiné par le code ; les pédales et le klaxon gardent un petit libellé dessous.
                var mask = Pictograms.For(zone.control, 128);
                if (mask != null)
                {
                    var icon = UiKit.NewImage(rect, "Pictogramme", Color.white,
                        UiKit.Cached("picto-" + zone.control, () => UiKit.ToSprite("Picto" + zone.control, mask, 128, 0f)));
                    icon.preserveAspect = true;
                    _icons[zone.control] = icon;
                }

                var label = NewText(rect, font, zone.label, TextAnchor.MiddleCenter);
                var lr = (RectTransform)label.transform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = lr.offsetMax = Vector2.zero;
                // Flèches et pause : le pictogramme suffit.
                label.enabled = mask == null || zone.label.Length > 2;
                _buttons.Add((zone, img, rect));
                if (zone.control == DriveControl.Action) _actionLabel = label;
                if (zone.control == DriveControl.Quality) _qualityLabel = label;
            }

            _speed = NewText(_canvas, font, "", TextAnchor.UpperLeft);
            var sr = (RectTransform)_speed.transform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);

            // Voyant de marche arrière : pastille rouge « R » à droite du compteur.
            _reverseLight = UiKit.NewImage(_canvas, "Marche arrière", new Color(0.85f, 0.12f, 0.1f, 0.95f), UiKit.Rounded);
            var rl = NewText(_reverseLight.rectTransform, font, "R", TextAnchor.MiddleCenter);
            rl.fontStyle = FontStyle.Bold;
            UiKit.Fill(rl.rectTransform);
            _reverseLight.gameObject.SetActive(false);
        }

        private static Text NewText(Transform parent, Font font, string text, TextAnchor anchor)
        {
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            return t;
        }

        /// <summary>Couleur de repos, ou pulsation vers le jaune si c'est la commande mise en avant.</summary>
        private Color Highlighted(DriveControl control, Color rest)
            => control == Highlight ? Color.Lerp(rest, HighlightColor, UiKit.Pulse()) : rest;

        private void UpdateView(DriveInput input)
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _laidOutFor)
            {
                _laidOutFor = size;
                foreach (var (zone, _, rect) in _buttons)
                {
                    var r = zone.Resolve(size.x, size.y);
                    rect.anchoredPosition = new Vector2(r.X, r.Y);
                    rect.sizeDelta = new Vector2(r.Width, r.Height);
                    var label = rect.GetComponentInChildren<Text>();
                    var big = zone.label.Length > 0 && zone.label.Length <= 2;
                    // Le bouton Qualité n'a pas de pictogramme : son libellé dynamique (« Qualité : Moyen · 30 i/s ») reste en petit.
                    var px = size.y * (big ? 0.12f
                        : zone.control == DriveControl.Action ? 0.05f
                        : zone.control == DriveControl.Quality ? 0.032f : 0.04f);
                    label.fontSize = Mathf.RoundToInt(Mathf.Min(px, r.Height * 0.55f));
                    if (_icons.TryGetValue(zone.control, out var icon))
                    {
                        // Pictogramme au centre, ou dans le haut quand un libellé occupe le bas.
                        var captioned = label.enabled;
                        var side = Mathf.Min(r.Width, r.Height * (captioned ? 0.62f : 1f)) * (captioned ? 0.8f : 0.62f);
                        var ir = icon.rectTransform;
                        ir.anchorMin = ir.anchorMax = new Vector2(0.5f, captioned ? 1f : 0.5f);
                        ir.pivot = new Vector2(0.5f, captioned ? 1f : 0.5f);
                        ir.sizeDelta = new Vector2(side, side);
                        ir.anchoredPosition = new Vector2(0f, captioned ? -r.Height * 0.06f : 0f);
                        if (captioned)
                        {
                            label.fontSize = Mathf.RoundToInt(Mathf.Min(size.y * 0.032f, r.Height * 0.16f));
                            label.alignment = TextAnchor.LowerCenter;
                            var lr = label.rectTransform;
                            lr.offsetMin = new Vector2(0f, r.Height * 0.06f);
                        }
                    }
                }
                var sr = (RectTransform)_speed.transform;
                sr.anchoredPosition = new Vector2(size.y * 0.04f, -size.y * 0.04f);
                sr.sizeDelta = new Vector2(size.y * 0.3f, size.y * 0.12f);
                _speed.fontSize = Mathf.RoundToInt(size.y * 0.07f);
                UiKit.PlaceTopLeft(_reverseLight.rectTransform, size.y * 0.34f, size.y * 0.045f, size.y * 0.075f, size.y * 0.075f);
                _reverseLight.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(size.y * 0.055f);
            }

            foreach (var (zone, image, rect) in _buttons)
            {
                rect.gameObject.SetActive(!Locked);
                var on = zone.control switch
                {
                    DriveControl.SteerLeft => input.Left,
                    DriveControl.SteerRight => input.Right,
                    DriveControl.Accelerate => input.Accelerate,
                    DriveControl.Brake => input.Brake,
                    DriveControl.Horn => input.Horn,
                    DriveControl.Action => input.Action,
                    DriveControl.Pause => input.Pause,
                    DriveControl.Quality => input.Quality,
                    _ => false,
                };
                if (zone.control == DriveControl.Action)
                {
                    image.enabled = ActionVisible;
                    _actionLabel.enabled = ActionVisible;
                    _actionLabel.text = ActionLabel;
                    image.color = on ? Pressed : Highlighted(zone.control, ActionColor);
                    continue;
                }
                if (zone.control == DriveControl.Quality && _qualityLabel != null) _qualityLabel.text = QualityLabel;
                image.color = on ? Pressed : Highlighted(zone.control, Idle);
            }

            _speed.enabled = !Locked;
            if (taxi != null)
            {
                _speed.text = $"{Mathf.RoundToInt(taxi.SpeedKmh)} km/h";
                var reverse = taxi.Command.Gear == Gear.Reverse && !Locked;
                _reverseLight.gameObject.SetActive(reverse);
                if (reverse) _reverseLight.color = new Color(0.85f, 0.12f, 0.1f, 0.7f + 0.3f * UiKit.Pulse(8f));
            }
        }
    }
}
