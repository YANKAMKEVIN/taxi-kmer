using System.Collections.Generic;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Plan du taxi vu de dessus dans le HUD (sous l'heure et la recette) : le chauffeur et 2 places devant, 3 derrière.
    /// Chaque passager est une silhouette mince, normale ou costaude à la couleur de son lieu de descente (la même que
    /// sur la frise), avec l'initiale du lieu ; les places libres restent en contour, et celles qui coincent clignotent
    /// en rouge quand un client ne rentre pas. Places calculées par <see cref="CabinPlan"/> (Core).
    /// </summary>
    public sealed class CabinPlanView
    {
        private const float BlinkSeconds = 2f;

        private readonly Dictionary<string, int> _stopIndex = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _initials = new Dictionary<string, string>();
        private readonly RectTransform _root;
        private readonly Image _body;
        private readonly Image _wheel;
        private readonly List<Image> _frames = new List<Image>();
        private readonly List<Image> _people = new List<Image>();
        private readonly List<Text> _letters = new List<Text>();
        private Client _refused;
        private float _refusedUntil;

        private static readonly Color FreeFrame = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color BlockFrame = new Color(1f, 0.25f, 0.2f, 1f);

        public CabinPlanView(RectTransform overlay, Font font, Stop[] stops)
        {
            for (var i = 0; i < stops.Length; i++)
            {
                _stopIndex[stops[i].id] = i;
                var name = StopNames.Strip(stops[i].name);
                _initials[stops[i].id] = name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
            }
            var go = new GameObject("Places du taxi", typeof(RectTransform));
            go.transform.SetParent(overlay, false);
            _root = (RectTransform)go.transform;
            _body = UiKit.NewImage(_root, "Caisse", new Color(0.05f, 0.05f, 0.07f, 0.6f), UiKit.Rounded);
            UiKit.Fill(_body.rectTransform);
            var outline = UiKit.NewImage(_root, "Contour", new Color(1f, 0.82f, 0.1f, 0.9f), UiKit.Frame);
            UiKit.Fill(outline.rectTransform);
            _wheel = UiKit.NewImage(_root, "Volant", new Color(0.75f, 0.75f, 0.75f, 0.8f), UiKit.Ring);
            for (var i = 0; i < 5; i++)
            {
                _frames.Add(UiKit.NewImage(_root, "Place", FreeFrame, UiKit.Frame));
                var person = UiKit.NewImage(_root, "Passager", Color.white, UiKit.Rounded);
                var letter = UiKit.NewText(person.transform, font, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f));
                letter.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Fill(letter.rectTransform);
                _people.Add(person);
                _letters.Add(letter);
            }
        }

        /// <summary>Un client ne rentre pas : ses places trop étroites clignotent un moment.</summary>
        public void ShowRefused(Client client)
        {
            _refused = client;
            _refusedUntil = Time.time + BlinkSeconds;
        }

        public void Update(TaxiCabin cabin, bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible) return;
            float h = Screen.height;
            float w = h * 0.2f, ht = h * 0.25f, pad = h * 0.015f;
            UiKit.PlaceTopLeft(_root, h * 0.04f, h * (ShiftGaugesView.BottomFraction + 0.02f), w, ht);

            var refused = Time.time < _refusedUntil ? _refused : null;
            var seats = CabinPlan.Seats(cabin, refused);
            var cell = (w - 2f * pad) / 3f;
            var rowH = (ht - 3f * pad) / 2f;
            // Rangée avant : le chauffeur à gauche (on roule à droite), puis les places de la banquette avant.
            var front = 0;
            var back = 0;
            var wheel = Mathf.Min(cell, rowH) * 0.6f;
            PlaceCell(_wheel.rectTransform, pad, pad, cell, rowH, 1f);
            _wheel.rectTransform.sizeDelta = new Vector2(wheel, wheel);
            for (var i = 0; i < seats.Count && i < _frames.Count; i++)
            {
                var s = seats[i];
                var col = s.Bench == BenchSide.Front ? 1 + front++ : back++;
                var row = s.Bench == BenchSide.Front ? 0 : 1;
                var x = pad + col * cell;
                var y = pad + row * (rowH + pad);
                PlaceCell(_frames[i].rectTransform, x, y, cell, rowH, 0.9f);
                _frames[i].color = s.Blocking ? Color.Lerp(FreeFrame, BlockFrame, UiKit.Pulse(14f)) : FreeFrame;

                var person = _people[i];
                person.gameObject.SetActive(s.Passenger != null);
                if (s.Passenger == null) continue;
                var k = CabinPlan.SilhouetteWidth(s.Passenger.Corpulence);
                PlaceCell(person.rectTransform, x, y, cell, rowH, 0.8f, k);
                _stopIndex.TryGetValue(s.Passenger.DestinationStopId, out var stop);
                person.color = StopPalette.For(stop);
                _initials.TryGetValue(s.Passenger.DestinationStopId, out var initial);
                _letters[i].text = initial ?? "?";
                _letters[i].fontSize = Mathf.RoundToInt(rowH * 0.45f);
            }
        }

        /// <summary>Élément centré dans une case (x, y depuis le coin haut gauche du plan), réduit à <paramref name="fill"/>.</summary>
        private static void PlaceCell(RectTransform r, float x, float y, float w, float h, float fill, float widthFactor = 1f)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x + w / 2f, -(y + h / 2f));
            r.sizeDelta = new Vector2(w * fill * widthFactor, h * fill);
        }
    }
}
