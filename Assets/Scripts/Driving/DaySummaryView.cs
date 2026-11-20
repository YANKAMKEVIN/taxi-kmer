using TaxiKmer.Core.Day;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Écran de bilan de fin de journée (tâche 1.6) : recettes, versement, carburant, reste, puis « Nouvelle journée ».
    /// Les lignes tombent une à une avec leur montant qui défile, puis un tampon « VERSEMENT COUVERT » ou « IL MANQUE … »
    /// frappe le total (<see cref="SummaryReveal"/>) ; un appui pendant l'animation la termine d'un coup.
    /// Les chiffres viennent de <see cref="DaySummary"/> (Core, testé) ; ce composant ne fait que les afficher.
    /// Tailles en fractions de la hauteur d'écran, comme les commandes.
    /// </summary>
    public sealed class DaySummaryView : MonoBehaviour
    {
        private RectTransform _panel;
        private RectTransform _button;
        private Text _title, _total, _note, _buttonLabel, _stampText;
        private readonly Text[] _labels = new Text[SummaryReveal.Lines];
        private readonly Text[] _amounts = new Text[SummaryReveal.Lines];
        private readonly int[] _values = new int[SummaryReveal.Lines];
        private readonly string[] _signs = { "+ ", "- ", "- " };
        private Image _backdrop, _panelImage, _buttonImage, _stamp;
        private Font _font;
        private DaySummary _summary;
        private readonly SummaryReveal _reveal = new SummaryReveal();

        public bool Visible { get; private set; }

        private static readonly Color Positive = new Color(0.45f, 0.9f, 0.45f);
        private static readonly Color Negative = new Color(1f, 0.45f, 0.4f);
        private static readonly Color ButtonColor = new Color(0.1f, 0.45f, 0.2f, 0.95f);

        public void Show(DaySummary s, string clock, Font font)
        {
            if (Visible) return;
            Visible = true;
            _font = font;
            _summary = s;
            Build();

            _title.text = $"Bilan du jour — {clock}";
            var courses = s.RideCount == 1 ? "1 course" : $"{s.RideCount} courses";
            _labels[0].text = $"Recettes ({courses})";
            _labels[1].text = "Versement au propriétaire";
            _labels[2].text = $"Carburant ({DaySummary.OneDecimal(s.FuelLitres)} L, {DaySummary.OneDecimal(s.Kilometres)} km)";
            _values[0] = s.Revenue;
            _values[1] = s.Versement;
            _values[2] = s.FuelCost;
            _total.color = s.Remaining >= 0 ? Positive : Negative;

            _stampText.text = SummaryReveal.StampText(s);
            var stampColor = s.VersementReached ? Positive : Negative;
            _stampText.color = stampColor;
            _stamp.color = stampColor;

            var note = "";
            if (s.UnpaidPassengers > 0)
            {
                var who = s.UnpaidPassengers == 1 ? "1 passager descendu sans payer" : $"{s.UnpaidPassengers} passagers descendus sans payer";
                note = $"{who} ({DaySummary.Money(s.UnpaidFares)}).";
            }
            _note.text = note;
            Layout();
        }

        private void Update()
        {
            if (!Visible) return;
            _reveal.Advance(Time.unscaledDeltaTime);
            Layout();
            var pressed = UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.E);
            var tappedAnywhere = false;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                tappedAnywhere = true;
                if (RectTransformUtility.RectangleContainsScreenPoint(_button, t.position)) pressed = true;
            }
            if (UnityEngine.Input.touchCount == 0 && UnityEngine.Input.GetMouseButtonDown(0))
            {
                tappedAnywhere = true;
                if (RectTransformUtility.RectangleContainsScreenPoint(_button, UnityEngine.Input.mousePosition)) pressed = true;
            }
            if (!_reveal.Done)
            {
                // Pendant l'animation, un appui (où que ce soit) affiche tout d'un coup.
                if (pressed || tappedAnywhere) _reveal.Finish();
                return;
            }
            if (pressed) NewDay();
        }

        /// <summary>Nouvelle journée : on recharge la scène (pas encore de progression d'un jour à l'autre).</summary>
        public void NewDay()
        {
            StreetView.TitleDone = true; // la nouvelle journée repart directement au volant
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Build()
        {
            var canvasGo = new GameObject("Bilan", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var root = (RectTransform)canvasGo.transform;

            _backdrop = UiKit.NewImage(root, "Fond", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Fill(_backdrop.rectTransform);

            _panelImage = UiKit.NewImage(root, "Panneau", new Color(0.08f, 0.08f, 0.1f, 0.97f), UiKit.Rounded);
            _panel = _panelImage.rectTransform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);

            _title = NewText(_panel, TextAnchor.UpperCenter, new Color(1f, 0.82f, 0.1f));
            for (var i = 0; i < SummaryReveal.Lines; i++)
            {
                _labels[i] = NewText(_panel, TextAnchor.UpperLeft, Color.white);
                _amounts[i] = NewText(_panel, TextAnchor.UpperRight, Color.white);
            }
            _total = NewText(_panel, TextAnchor.UpperCenter, Color.white);
            _note = NewText(_panel, TextAnchor.UpperCenter, new Color(0.85f, 0.85f, 0.85f));

            _stamp = UiKit.NewImage(_panel, "Tampon", Positive, UiKit.Frame);
            _stampText = NewText(_stamp.rectTransform, TextAnchor.MiddleCenter, Positive);
            _stampText.fontStyle = FontStyle.Bold;
            _stampText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Fill(_stampText.rectTransform);

            _buttonImage = UiKit.NewImage(_panel, "Nouvelle journée", ButtonColor, UiKit.Rounded);
            _button = _buttonImage.rectTransform;
            _buttonLabel = NewText(_button, TextAnchor.MiddleCenter, Color.white);
            _buttonLabel.text = "Nouvelle journée";
            UiKit.Fill(_buttonLabel.rectTransform);
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
            for (var i = 0; i < SummaryReveal.Lines; i++)
            {
                // La ligne tombe de 0,06 hauteur d'écran en apparaissant ; son montant défile ensuite.
                var drop = _reveal.LineDrop(i);
                var y = h * (0.15f + 0.075f * i) - (1f - drop) * h * 0.06f;
                Place(_labels[i], pad, y, w * 0.62f, h * 0.07f, h * 0.05f);
                Place(_amounts[i], w * 0.55f, y, w * 0.45f - pad, h * 0.07f, h * 0.05f);
                var alpha = new Color(1f, 1f, 1f, drop);
                _labels[i].color = _amounts[i].color = alpha;
                _amounts[i].text = _signs[i] + DaySummary.Money(SummaryReveal.Partial(_values[i], _reveal.LineCount(i)));
            }

            var totalDrop = _reveal.TotalDrop;
            Place(_total, pad, h * 0.4f - (1f - totalDrop) * h * 0.06f, w - 2 * pad, h * 0.09f, h * 0.07f);
            var remaining = SummaryReveal.Partial(Mathf.Abs(_summary.Remaining), _reveal.TotalCount);
            _total.text = $"Reste : {(_summary.Remaining >= 0 ? "+ " : "- ")}{DaySummary.Money(remaining)}";
            var tc = _total.color;
            tc.a = totalDrop;
            _total.color = tc;

            // Tampon : arrive en grand et se pose de biais sous le total.
            var stampScale = _reveal.StampScale;
            _stamp.gameObject.SetActive(stampScale > 0f);
            var sr = _stamp.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            _stampText.fontSize = Mathf.RoundToInt(h * 0.055f);
            sr.sizeDelta = new Vector2(_stampText.preferredWidth + h * 0.08f, h * 0.1f);
            sr.anchoredPosition = new Vector2(0f, -h * 0.545f);
            sr.localRotation = Quaternion.Euler(0f, 0f, 6f);
            sr.localScale = Vector3.one * Mathf.Max(stampScale, 0.01f);

            Place(_note, pad, h * 0.61f, w - 2 * pad, h * 0.08f, h * 0.04f);
            _note.enabled = _reveal.NoteVisible;

            _button.anchorMin = _button.anchorMax = new Vector2(0.5f, 0f);
            _button.pivot = new Vector2(0.5f, 0f);
            _button.sizeDelta = new Vector2(h * 0.6f, h * 0.13f);
            _button.anchoredPosition = new Vector2(0f, h * 0.04f);
            _buttonLabel.fontSize = Mathf.RoundToInt(h * 0.055f);
            var ready = _reveal.Done;
            _buttonImage.color = ready ? ButtonColor : new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.35f);
            _buttonLabel.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        /// <summary>Texte placé depuis le coin haut gauche du panneau.</summary>
        private static void Place(Text t, float x, float y, float w, float hgt, float fontPx)
        {
            UiKit.PlaceTopLeft(t.rectTransform, x, y, w, hgt);
            t.fontSize = Mathf.RoundToInt(fontPx);
        }

        private Text NewText(Transform parent, TextAnchor anchor, Color color) => UiKit.NewText(parent, _font, anchor, color);
    }
}
