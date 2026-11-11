using System.Collections.Generic;
using TaxiKmer.Core.Driving;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Commandes du joueur : écran tactile (flèches à gauche, pédales à droite, klaxon en haut à droite, glisser ailleurs
    /// pour tourner la caméra), souris et clavier pour l'éditeur. Dessine les boutons et le compteur de vitesse.
    /// Clavier : ← → / Q D / A (tourner), ↑ / Z / W (accélérer), ↓ / S / espace (frein, marche arrière), H (klaxon),
    /// E ou Entrée (bouton contextuel), R (remettre sur la route).
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
        private Text _actionLabel;
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
        /// <summary>Calque d'interface où le jeu peut ajouter ses propres textes.</summary>
        public RectTransform Overlay => _canvas;
        public Font Font { get; private set; }

        private static readonly Color Idle = new Color(0f, 0f, 0f, 0.28f);
        private static readonly Color Pressed = new Color(1f, 0.82f, 0.1f, 0.55f);
        private static readonly Color ActionColor = new Color(0.1f, 0.45f, 0.2f, 0.75f);

        private void Awake()
        {
            UnityEngine.Input.simulateMouseWithTouches = false;
            UnityEngine.Input.multiTouchEnabled = true;
            _layout = DriveData.Load().layout;
            BuildView();
        }

        private void Update()
        {
            var input = ReadKeyboard() | ReadPointers();
            if (!ActionVisible) input.Action = false;
            ActionTriggered = input.Action && !_previous.Action;
            HornTriggered = input.Horn && !_previous.Horn;
            _previous = input;
            if (taxi != null)
            {
                taxi.Controls = input;
                if (UnityEngine.Input.GetKeyDown(KeyCode.R)) taxi.ResetToRoute();
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

                var label = NewText(rect, font, zone.label, TextAnchor.MiddleCenter);
                var lr = (RectTransform)label.transform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = lr.offsetMax = Vector2.zero;
                _buttons.Add((zone, img, rect));
                if (zone.control == DriveControl.Action) _actionLabel = label;
            }

            _speed = NewText(_canvas, font, "", TextAnchor.UpperLeft);
            var sr = (RectTransform)_speed.transform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
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
                    label.fontSize = Mathf.RoundToInt(size.y * (big ? 0.12f : zone.control == DriveControl.Action ? 0.05f : 0.04f));
                }
                var sr = (RectTransform)_speed.transform;
                sr.anchoredPosition = new Vector2(size.y * 0.04f, -size.y * 0.04f);
                sr.sizeDelta = new Vector2(size.y * 0.4f, size.y * 0.12f);
                _speed.fontSize = Mathf.RoundToInt(size.y * 0.07f);
            }

            foreach (var (zone, image, _) in _buttons)
            {
                var on = zone.control switch
                {
                    DriveControl.SteerLeft => input.Left,
                    DriveControl.SteerRight => input.Right,
                    DriveControl.Accelerate => input.Accelerate,
                    DriveControl.Brake => input.Brake,
                    DriveControl.Horn => input.Horn,
                    DriveControl.Action => input.Action,
                    _ => false,
                };
                if (zone.control == DriveControl.Action)
                {
                    image.enabled = ActionVisible;
                    _actionLabel.enabled = ActionVisible;
                    _actionLabel.text = ActionLabel;
                    image.color = on ? Pressed : ActionColor;
                    continue;
                }
                image.color = on ? Pressed : Idle;
            }

            if (taxi != null)
            {
                var gear = taxi.Command.Gear == Gear.Reverse ? "  R" : "";
                _speed.text = $"{Mathf.RoundToInt(taxi.SpeedKmh)} km/h{gear}";
            }
        }
    }
}
