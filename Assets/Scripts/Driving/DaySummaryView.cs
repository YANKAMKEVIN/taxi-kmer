using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Écran de bilan de fin de journée (tâche 1.6, jalon 3) : recettes, versement (complété par l'épargne s'il le faut),
    /// carburant, dépenses imprévues, épargne reportée, humeur du propriétaire, puis « Jour suivant » (ou « Nouvelle
    /// carrière » après un renvoi). Les lignes tombent une à une avec leur montant qui défile, puis un tampon
    /// (« VERSEMENT COUVERT », « IL MANQUE … », « TAXI REPRIS »…) frappe le total (<see cref="SummaryReveal"/>) ;
    /// un appui pendant l'animation la termine d'un coup.
    /// Les chiffres viennent de <see cref="DaySummary"/> (Core, testé) ; ce composant ne fait que les afficher.
    /// Tailles en fractions de la hauteur d'écran, comme les commandes.
    /// </summary>
    public sealed class DaySummaryView : MonoBehaviour
    {
        private RectTransform _panel;
        private RectTransform _button;
        private Text _title, _total, _note, _buttonLabel, _stampText;
        // Une ligne par montant : recettes, versement, carburant, puis les dépenses imprévues (nombre variable).
        private Text[] _labels = new Text[0];
        private Text[] _amounts = new Text[0];
        private int[] _values = new int[0];
        private string[] _signs = new string[0];
        private Image _backdrop, _panelImage, _buttonImage, _stamp;
        private Font _font;
        private DayClosure _closure;
        private SummaryReveal _reveal = new SummaryReveal();

        public bool Visible { get; private set; }
        /// <summary>Le propriétaire a renvoyé le chauffeur : le bouton commence une nouvelle carrière.</summary>
        public bool Fired { get; private set; }
        /// <summary>Le joueur demande une nouvelle carrière (après un renvoi), avant le rechargement de la scène.</summary>
        public event System.Action NewCareerRequested;

        private static readonly Color Positive = new Color(0.45f, 0.9f, 0.45f);
        private static readonly Color Negative = new Color(1f, 0.45f, 0.4f);
        private static readonly Color ButtonColor = new Color(0.1f, 0.45f, 0.2f, 0.95f);

        public void Show(DaySummary s, DayClosure c, string clock, Font font, string weekday = "", string reputationLine = "")
        {
            if (Visible) return;
            Visible = true;
            Fired = c.Mood == OwnerMood.Fired;
            _font = font;
            _closure = c;

            var courses = s.RideCount == 1 ? "1 course" : $"{s.RideCount} courses";
            var versementLabel = c.TakenFromSavings > 0
                ? $"Versement au propriétaire (dont {DaySummary.Money(c.TakenFromSavings)} d'épargne)"
                : c.VersementMissing > 0 ? "Versement remis (incomplet)" : "Versement au propriétaire";
            var labels = new System.Collections.Generic.List<string>
            {
                $"Recettes ({courses})",
                versementLabel,
                s.TankLitres >= 0f
                    ? $"Carburant acheté ({DaySummary.OneDecimal(s.FuelLitres)} L ; {DaySummary.OneDecimal(s.Kilometres)} km parcourus)"
                    : $"Carburant ({DaySummary.OneDecimal(s.FuelLitres)} L, {DaySummary.OneDecimal(s.Kilometres)} km)",
            };
            var values = new System.Collections.Generic.List<int> { s.Revenue, c.VersementPaid, s.FuelCost };
            var signs = new System.Collections.Generic.List<string> { "+ ", "- ", "- " };
            foreach (var e in s.Expenses)
            {
                labels.Add(e.Label);
                values.Add(e.Amount);
                signs.Add("- ");
            }
            _values = values.ToArray();
            _signs = signs.ToArray();
            _reveal = new SummaryReveal(_values.Length);

            Build();
            _buttonLabel.text = Fired ? "Nouvelle carrière" : "Jour suivant";
            var dayName = string.IsNullOrEmpty(weekday) ? "" : $" ({weekday})";
            _title.text = $"Bilan du jour {c.DayNumber}{dayName} — {clock}";
            for (var i = 0; i < _labels.Length; i++) _labels[i].text = labels[i];
            _total.color = c.SavingsAfter - c.SavingsBefore >= 0 ? Positive : Negative;

            _stampText.text = SummaryReveal.StampText(c);
            var stampColor = SummaryReveal.StampPositive(c) ? Positive : Negative;
            _stampText.color = stampColor;
            _stamp.color = stampColor;

            string note;
            switch (c.Mood)
            {
                case OwnerMood.Fired:
                    note = s.VehicleSeized
                        ? "Le propriétaire t'a repris son taxi : il est à la fourrière par ta faute."
                        : $"Le propriétaire t'a repris son taxi : {c.RecentMisses} versements manqués en une semaine.";
                    break;
                case OwnerMood.Threatens:
                    var left = c.MissesBeforeFired - c.RecentMisses;
                    note = $"Il manque {DaySummary.Money(c.VersementMissing)} au versement. Le propriétaire menace : "
                        + (left <= 1 ? "encore un manque cette semaine et il te reprend le taxi." : $"encore {left} manques cette semaine et il te reprend le taxi.");
                    break;
                default:
                    note = "Le propriétaire a son versement.";
                    break;
            }
            if (s.Arrested)
                note = "Garde à vue : la gendarmerie t'a interpellé après l'accident."
                    + (s.VehicleSeized ? " Le taxi a été saisi." : "") + "\n" + note;
            if (s.UnpaidPassengers > 0)
            {
                var who = s.UnpaidPassengers == 1 ? "1 passager descendu sans payer" : $"{s.UnpaidPassengers} passagers descendus sans payer";
                note += $"\n{who} ({DaySummary.Money(s.UnpaidFares)}).";
            }
            if (!string.IsNullOrEmpty(reputationLine)) note += "\n" + reputationLine;
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

        /// <summary>Jour suivant (la carrière est déjà sauvegardée) ou nouvelle carrière après un renvoi : on recharge la scène.</summary>
        public void NewDay()
        {
            StreetView.TitleDone = true; // la journée suivante repart directement au volant
            if (Fired) NewCareerRequested?.Invoke();
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
            _labels = new Text[_values.Length];
            _amounts = new Text[_values.Length];
            for (var i = 0; i < _values.Length; i++)
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
            // Lignes dans une bande fixe (0,15 à 0,39 de la hauteur d'écran) : avec des dépenses imprévues,
            // elles se resserrent et le texte rapetisse pour garder la place du total, du tampon et de la note.
            var step = Mathf.Min(0.075f, 0.24f / Mathf.Max(1, _labels.Length));
            var lineFont = Mathf.Min(0.05f, step * 0.68f);
            for (var i = 0; i < _labels.Length; i++)
            {
                // La ligne tombe d'un écart de ligne en apparaissant ; son montant défile ensuite.
                var drop = _reveal.LineDrop(i);
                var y = h * (0.15f + step * i) - (1f - drop) * h * Mathf.Min(0.06f, step);
                Place(_labels[i], pad, y, w * 0.62f, h * step, h * lineFont);
                Place(_amounts[i], w * 0.55f, y, w * 0.45f - pad, h * step, h * lineFont);
                var alpha = new Color(1f, 1f, 1f, drop);
                _labels[i].color = _amounts[i].color = alpha;
                _amounts[i].text = _signs[i] + DaySummary.Money(SummaryReveal.Partial(_values[i], _reveal.LineCount(i)));
            }

            // Total : l'épargne reportée au jour suivant et sa variation du jour, qui défilent.
            var totalDrop = _reveal.TotalDrop;
            Place(_total, pad, h * 0.4f - (1f - totalDrop) * h * 0.06f, w - 2 * pad, h * 0.09f, h * 0.07f);
            var after = _closure.SavingsAfter;
            var change = after - _closure.SavingsBefore;
            var shownAfter = SummaryReveal.Partial(Mathf.Abs(after), _reveal.TotalCount);
            var shownChange = SummaryReveal.Partial(Mathf.Abs(change), _reveal.TotalCount);
            _total.text = $"Épargne : {DaySummary.Money(after < 0 ? -shownAfter : shownAfter)} ({(change >= 0 ? "+ " : "- ")}{DaySummary.Money(shownChange)})";
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

            // Note (propriétaire, garde à vue, passagers partis sans payer) : jusqu'à trois lignes dans 0,09 d'écran.
            var noteLines = _note.text.Split('\n').Length;
            Place(_note, pad, h * 0.6f, w - 2 * pad, h * 0.09f, h * Mathf.Min(0.04f, 0.075f / noteLines));
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
