using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Coin haut gauche du HUD, sous le compteur de vitesse : l'heure avec une fine barre de journée qui se vide (et le
    /// numéro du jour à sa droite, jalon 3), puis la
    /// jauge de versement qui se remplit jusqu'au montant dû (recette écrite dedans) et passe au vert une fois couvert.
    /// Valeurs et textes calculés par <see cref="HudGauges"/> (Core).
    /// </summary>
    public sealed class ShiftGaugesView
    {
        private readonly RectTransform _root;
        private readonly Text _clock;
        private readonly Text _day;
        private readonly Image _dayTrack, _dayFill;
        private readonly Text _caption;
        private readonly Image _gaugeTrack, _gaugeFill;
        private readonly Text _revenue;

        private static readonly Color Track = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color DayColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color Filling = new Color(1f, 0.82f, 0.1f, 0.95f);
        private static readonly Color Covered = new Color(0.3f, 0.85f, 0.4f, 0.95f);
        private static readonly Color LateColor = new Color(1f, 0.6f, 0.15f, 1f);

        /// <summary>Bas du bloc (pixels depuis le haut de l'écran) : ce qui suit dans le HUD se place dessous.</summary>
        public static float BottomFraction => 0.27f;

        public ShiftGaugesView(RectTransform overlay, Font font)
        {
            var go = new GameObject("Horloge et versement", typeof(RectTransform));
            go.transform.SetParent(overlay, false);
            _root = (RectTransform)go.transform;
            UiKit.Fill(_root);
            _clock = Label(font, TextAnchor.UpperLeft, Color.white);
            _dayTrack = UiKit.NewImage(_root, "Journée", Track, UiKit.Rounded);
            _dayFill = UiKit.NewImage(_dayTrack.transform, "Reste", DayColor, UiKit.Rounded);
            _day = Label(font, TextAnchor.MiddleLeft, Color.white);
            _caption = Label(font, TextAnchor.LowerLeft, Color.white);
            _gaugeTrack = UiKit.NewImage(_root, "Versement", Track, UiKit.Rounded);
            _gaugeFill = UiKit.NewImage(_gaugeTrack.transform, "Recette", Filling, UiKit.Rounded);
            _revenue = UiKit.NewText(_gaugeTrack.transform, font, TextAnchor.MiddleCenter, Color.white);
            _revenue.horizontalOverflow = HorizontalWrapMode.Overflow;
            _revenue.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            UiKit.Fill(_revenue.rectTransform);
        }

        private Text Label(Font font, TextAnchor anchor, Color color)
        {
            var t = UiKit.NewText(_root, font, anchor, color);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            return t;
        }

        /// <summary>Texte de l'heure (la fin de journée le colore et le fait battre).</summary>
        public Text Clock => _clock;

        public void Update(Shift shift, string dayLabel, int shownRevenue, bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible) return;
            float h = Screen.height;
            var x = h * 0.04f;
            var w = h * 0.36f;

            _clock.text = shift.Clock.Display;
            // Dernière heure : l'horloge passe à l'orange et bat doucement ; après l'heure de fin, orange fixe.
            var lastHour = HudGauges.LastHour(shift.Clock);
            var late = lastHour || shift.Clock.IsOver;
            _clock.color = late ? LateColor : Color.white;
            _clock.fontSize = Mathf.RoundToInt(h * 0.05f * (lastHour ? 1f + 0.08f * UiKit.Pulse(3.5f) : 1f));
            _dayFill.color = late ? LateColor : DayColor;
            UiKit.PlaceTopLeft(_clock.rectTransform, x, h * 0.115f, w, h * 0.06f);
            UiKit.PlaceTopLeft(_dayTrack.rectTransform, x, h * 0.175f, w * 0.6f, h * 0.012f);
            Fill(_dayFill.rectTransform, HudGauges.DayLeft(shift.Clock));
            // « Jour 3 · mer. » dans la place libre à droite de la barre de journée (jours et calendrier, jalon 3).
            _day.text = dayLabel;
            _day.fontSize = Mathf.RoundToInt(h * 0.028f);
            UiKit.PlaceTopLeft(_day.rectTransform, x + w * 0.64f, h * 0.162f, w * 0.36f, h * 0.038f);

            var versement = shift.Ledger.Versement;
            var revenue = shownRevenue; // le compteur défile : la jauge et la légende suivent ce qu'il affiche
            var covered = HudGauges.VersementCovered(revenue, versement);
            _caption.text = HudGauges.VersementCaption(revenue, versement);
            _caption.fontSize = Mathf.RoundToInt(h * 0.028f);
            _caption.color = covered ? Covered : Color.white;
            UiKit.PlaceTopLeft(_caption.rectTransform, x, h * 0.19f, w, h * 0.034f);
            UiKit.PlaceTopLeft(_gaugeTrack.rectTransform, x, h * 0.225f, w, h * 0.045f);
            Fill(_gaugeFill.rectTransform, HudGauges.VersementFill(shownRevenue, versement));
            _gaugeFill.color = covered ? Covered : Filling;
            _revenue.text = DaySummary.Money(shownRevenue);
            _revenue.fontSize = Mathf.RoundToInt(h * 0.03f);
        }

        /// <summary>Centre du compteur de recette, en pixels d'écran (le montant payé y file).</summary>
        public Vector2 RevenueScreenPoint
        {
            get
            {
                float h = Screen.height;
                return new Vector2(h * 0.04f + h * 0.18f, h - (h * 0.225f + h * 0.0225f));
            }
        }

        /// <summary>Remplit la barre de la gauche jusqu'à la fraction donnée.</summary>
        private static void Fill(RectTransform r, float fraction)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.gameObject.SetActive(fraction > 0.001f);
        }
    }
}
