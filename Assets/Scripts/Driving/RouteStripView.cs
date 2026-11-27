using System.Collections.Generic;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>Couleur de chaque lieu, partagée par la frise du trajet et le plan des places.</summary>
    public static class StopPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.95f, 0.35f, 0.35f), new Color(1f, 0.62f, 0.15f), new Color(0.35f, 0.75f, 1f),
            new Color(0.75f, 0.5f, 1f), new Color(0.35f, 0.9f, 0.55f), new Color(1f, 0.88f, 0.2f),
        };

        public static Color For(int stopIndex) => Colors[((stopIndex % Colors.Length) + Colors.Length) % Colors.Length];
    }

    /// <summary>
    /// Frise du trajet Messassi → Étoudi en haut de l'écran (entre le compteur de vitesse et le klaxon) : les six lieux
    /// nommés, le taxi qui avance, une pastille par lieu où des passagers descendent (avec leur nombre) et un point
    /// par client qui attend. Placement calculé par <see cref="RouteStrip"/> (Core).
    /// </summary>
    public sealed class RouteStripView
    {
        private readonly Stop[] _stops;
        private readonly float _length;
        private readonly RectTransform _root;
        private readonly Image _line;
        private readonly Image _taxi;
        private readonly List<Image> _ticks = new List<Image>();
        private readonly List<Text> _labels = new List<Text>();
        private readonly List<Image> _pins = new List<Image>();
        private readonly List<Text> _pinCounts = new List<Text>();
        private readonly List<Image> _dots = new List<Image>();
        private Vector2Int _laidOutFor;
        private float _x0, _width, _lineY, _unit;

        public RouteStripView(RectTransform overlay, Font font, Stop[] stops, float routeLength)
        {
            _stops = stops;
            _length = routeLength;
            var go = new GameObject("Frise du trajet", typeof(RectTransform));
            go.transform.SetParent(overlay, false);
            _root = (RectTransform)go.transform;
            UiKit.Fill(_root);

            _line = UiKit.NewImage(_root, "Ligne", new Color(1f, 1f, 1f, 0.55f), UiKit.Rounded);
            for (var i = 0; i < stops.Length; i++)
            {
                _ticks.Add(UiKit.NewImage(_root, "Lieu", StopPalette.For(i), UiKit.Disc));
                var label = UiKit.NewText(_root, font, TextAnchor.LowerLeft, Color.white);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.text = StopNames.Strip(stops[i].name);
                label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
                _labels.Add(label);
                var pin = UiKit.NewImage(_root, "Passagers", StopPalette.For(i), UiKit.Disc);
                var count = UiKit.NewText(pin.transform, font, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f));
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Fill(count.rectTransform);
                _pins.Add(pin);
                _pinCounts.Add(count);
            }
            _taxi = UiKit.NewImage(_root, "Taxi", new Color(1f, 0.82f, 0.1f), UiKit.Rounded);
            _taxi.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        }

        public void Update(Street street, float taxiAlong, bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible) return;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _laidOutFor) Layout(size);

            var u = _unit;
            Place(_taxi, X(taxiAlong), _lineY, u * 0.034f, u * 0.024f);

            var cabin = street.Shift.Cabin;
            for (var i = 0; i < _stops.Length; i++)
            {
                var n = cabin.PassengersFor(_stops[i].id).Count;
                _pins[i].gameObject.SetActive(n > 0);
                _ticks[i].enabled = n == 0;
                if (n > 0)
                {
                    Place(_pins[i], X(_stops[i].distanceAlongRouteMeters), _lineY, u * 0.034f, u * 0.034f);
                    _pinCounts[i].text = n.ToString();
                    _pinCounts[i].fontSize = Mathf.RoundToInt(u * 0.026f);
                }
            }

            // Clients qui attendent : petits points sous la ligne, alignés sous leur lieu.
            var shown = 0;
            var perStop = new Dictionary<Stop, int>();
            foreach (var c in street.Waiting)
            {
                perStop.TryGetValue(c.Pickup, out var k);
                perStop[c.Pickup] = k + 1;
                if (shown == _dots.Count) _dots.Add(UiKit.NewImage(_root, "Client", new Color(1f, 1f, 1f, 0.9f), UiKit.Disc));
                var dot = _dots[shown++];
                dot.gameObject.SetActive(true);
                var x = X(c.Pickup.distanceAlongRouteMeters) + (k - 1) * u * 0.016f;
                Place(dot, x, _lineY + u * 0.03f, u * 0.011f, u * 0.011f);
            }
            for (var i = shown; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
        }

        private void Layout(Vector2Int size)
        {
            _laidOutFor = size;
            float h = size.y;
            _unit = h;
            // Entre le compteur de vitesse (haut gauche) et le bouton pause, à gauche du klaxon (haut droite).
            _x0 = h * 0.47f;
            _width = Mathf.Max(h * 0.4f, size.x - h * 0.43f - _x0);
            _lineY = h * 0.07f;
            Place(_line, _x0 + _width / 2f, _lineY, _width, h * 0.008f);

            var fontPx = Mathf.RoundToInt(h * 0.024f);
            var centers = new float[_stops.Length];
            var widths = new float[_stops.Length];
            for (var i = 0; i < _stops.Length; i++)
            {
                Place(_ticks[i], X(_stops[i].distanceAlongRouteMeters), _lineY, h * 0.02f, h * 0.02f);
                _labels[i].fontSize = fontPx;
                centers[i] = X(_stops[i].distanceAlongRouteMeters) - _x0;
                widths[i] = _labels[i].preferredWidth;
            }
            var placed = RouteStrip.LayoutLabels(centers, widths, _width, 2, h * 0.012f);
            for (var i = 0; i < _stops.Length; i++)
            {
                var row = placed[i].Row;
                _labels[i].enabled = !placed[i].Hidden;
                UiKit.PlaceTopLeft(_labels[i].rectTransform, _x0 + placed[i].Left, h * (0.006f + 0.026f * row), widths[i] + 2f, h * 0.03f);
            }
        }

        private float X(float along) => _x0 + RouteStrip.Fraction(along, _length) * _width;

        /// <summary>Élément centré sur (x, y), y compté depuis le haut de l'écran.</summary>
        private static void Place(Image img, float x, float y, float w, float h)
        {
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }
    }
}
