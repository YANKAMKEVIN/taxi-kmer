using TaxiKmer.Core.Day;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Écran de bilan de fin de journée (tâche 1.6) : recettes, versement, carburant, reste, puis « Nouvelle journée ».
    /// Les chiffres viennent de <see cref="DaySummary"/> (Core, testé) ; ce composant ne fait que les afficher.
    /// Tailles en fractions de la hauteur d'écran, comme les commandes.
    /// </summary>
    public sealed class DaySummaryView : MonoBehaviour
    {
        private RectTransform _panel;
        private RectTransform _button;
        private Text _title, _labels, _amounts, _total, _note, _buttonLabel;
        private Image _backdrop, _panelImage, _buttonImage;
        private Font _font;

        public bool Visible { get; private set; }

        private static readonly Color Positive = new Color(0.45f, 0.9f, 0.45f);
        private static readonly Color Negative = new Color(1f, 0.45f, 0.4f);

        public void Show(DaySummary s, string clock, Font font)
        {
            if (Visible) return;
            Visible = true;
            _font = font;
            Build();

            _title.text = $"Bilan du jour — {clock}";
            var courses = s.RideCount == 1 ? "1 course" : $"{s.RideCount} courses";
            _labels.text = $"Recettes ({courses})\nVersement au propriétaire\nCarburant ({DaySummary.OneDecimal(s.FuelLitres)} L, {DaySummary.OneDecimal(s.Kilometres)} km)";
            _amounts.text = $"+ {DaySummary.Money(s.Revenue)}\n- {DaySummary.Money(s.Versement)}\n- {DaySummary.Money(s.FuelCost)}";
            _total.text = $"Reste : {(s.Remaining >= 0 ? "+ " : "- ")}{DaySummary.Money(Mathf.Abs(s.Remaining))}";
            _total.color = s.Remaining >= 0 ? Positive : Negative;

            var note = s.VersementReached
                ? "Versement couvert."
                : $"Versement non atteint : il manque {DaySummary.Money(s.VersementShortfall)}.";
            if (s.UnpaidPassengers > 0)
            {
                var who = s.UnpaidPassengers == 1 ? "1 passager descendu sans payer" : $"{s.UnpaidPassengers} passagers descendus sans payer";
                note += $"\n{who} ({DaySummary.Money(s.UnpaidFares)}).";
            }
            _note.text = note;
            Layout();
        }

        private void Update()
        {
            if (!Visible) return;
            Layout();
            var pressed = UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.E);
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && RectTransformUtility.RectangleContainsScreenPoint(_button, t.position)) pressed = true;
            }
            if (UnityEngine.Input.touchCount == 0 && UnityEngine.Input.GetMouseButtonDown(0)
                && RectTransformUtility.RectangleContainsScreenPoint(_button, UnityEngine.Input.mousePosition)) pressed = true;
            if (pressed) NewDay();
        }

        /// <summary>Nouvelle journée : on recharge la scène (pas encore de progression d'un jour à l'autre).</summary>
        public void NewDay() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private void Build()
        {
            var canvasGo = new GameObject("Bilan", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var root = (RectTransform)canvasGo.transform;

            _backdrop = NewImage(root, "Fond", new Color(0f, 0f, 0f, 0.55f));
            var br = _backdrop.rectTransform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;

            _panelImage = NewImage(root, "Panneau", new Color(0.08f, 0.08f, 0.1f, 0.97f));
            _panel = _panelImage.rectTransform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);

            _title = NewText(_panel, TextAnchor.UpperCenter, new Color(1f, 0.82f, 0.1f));
            _labels = NewText(_panel, TextAnchor.UpperLeft, Color.white);
            _amounts = NewText(_panel, TextAnchor.UpperRight, Color.white);
            _total = NewText(_panel, TextAnchor.UpperCenter, Color.white);
            _note = NewText(_panel, TextAnchor.UpperCenter, new Color(0.85f, 0.85f, 0.85f));

            _buttonImage = NewImage(_panel, "Nouvelle journée", new Color(0.1f, 0.45f, 0.2f, 0.95f));
            _button = _buttonImage.rectTransform;
            _buttonLabel = NewText(_button, TextAnchor.MiddleCenter, Color.white);
            _buttonLabel.text = "Nouvelle journée";
            var lr = _buttonLabel.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        /// <summary>Panneau de 1,5 × 0,86 hauteur d'écran (rentre dans un 16:9), centré.</summary>
        private void Layout()
        {
            float h = Screen.height;
            var w = Mathf.Min(h * 1.5f, Screen.width * 0.94f);
            var ph = h * 0.86f;
            _panel.sizeDelta = new Vector2(w, ph);
            var pad = h * 0.05f;

            Place(_title, pad, h * 0.04f, w - 2 * pad, h * 0.09f, h * 0.065f);
            Place(_labels, pad, h * 0.16f, w * 0.62f, h * 0.27f, h * 0.05f);
            Place(_amounts, w * 0.55f, h * 0.16f, w * 0.45f - pad, h * 0.27f, h * 0.05f);
            _labels.lineSpacing = _amounts.lineSpacing = 1.25f;
            Place(_total, pad, h * 0.45f, w - 2 * pad, h * 0.09f, h * 0.07f);
            Place(_note, pad, h * 0.55f, w - 2 * pad, h * 0.12f, h * 0.04f);

            _button.anchorMin = _button.anchorMax = new Vector2(0.5f, 0f);
            _button.pivot = new Vector2(0.5f, 0f);
            _button.sizeDelta = new Vector2(h * 0.6f, h * 0.13f);
            _button.anchoredPosition = new Vector2(0f, h * 0.04f);
            _buttonLabel.fontSize = Mathf.RoundToInt(h * 0.055f);
        }

        /// <summary>Texte placé depuis le coin haut gauche du panneau.</summary>
        private static void Place(Text t, float x, float y, float w, float hgt, float fontPx)
        {
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, hgt);
            t.fontSize = Mathf.RoundToInt(fontPx);
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text NewText(Transform parent, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
