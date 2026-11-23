using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>Ce que le joueur a choisi dans le menu cette image.</summary>
    public enum MenuChoice
    {
        None,
        StartDay,
        Resume,
        OpenSettings,
        CloseSettings,
        EndDay,
        VolumeDown,
        VolumeUp,
        ToggleVibration,
    }

    /// <summary>
    /// Écran titre « Taxi Kmer » (Commencer la journée, Réglages), pause (Reprendre, Réglages, Terminer la journée) et
    /// réglages (volume, vibrations ; la qualité graphique de la tâche 1.7 se choisit avec son bouton de conduite). Ne fait qu'afficher l'écran de
    /// <see cref="GameMenu"/> (Core) et rapporter les appuis ; StreetView applique les choix.
    /// </summary>
    public sealed class GameMenuView
    {
        private readonly RectTransform _root;
        private readonly Text _title, _subtitle, _info;
        private readonly Image _primary, _secondary, _tertiary, _minus, _plus, _toggle;
        private readonly Text _primaryText, _secondaryText, _tertiaryText, _toggleText;

        private static readonly Color Green = new Color(0.1f, 0.45f, 0.2f, 0.95f);
        private static readonly Color Grey = new Color(0.22f, 0.22f, 0.26f, 0.95f);
        private static readonly Color Red = new Color(0.5f, 0.14f, 0.12f, 0.95f);

        public GameMenuView(Transform parent, Font font)
        {
            var canvasGo = new GameObject("Menu", typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            _root = (RectTransform)canvasGo.transform;

            var backdrop = UiKit.NewImage(_root, "Fond", new Color(0f, 0f, 0f, 0.6f));
            UiKit.Fill(backdrop.rectTransform);
            _title = UiKit.NewText(_root, font, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.1f));
            _title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            _subtitle = UiKit.NewText(_root, font, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.9f));
            _info = UiKit.NewText(_root, font, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));
            _primary = UiKit.NewButton(_root, font, "", Green, out _primaryText);
            _secondary = UiKit.NewButton(_root, font, "", Grey, out _secondaryText);
            _tertiary = UiKit.NewButton(_root, font, "", Red, out _tertiaryText);
            _minus = UiKit.NewButton(_root, font, "–", Grey, out _);
            _plus = UiKit.NewButton(_root, font, "+", Grey, out _);
            _toggle = UiKit.NewButton(_root, font, "", Grey, out _toggleText);
        }

        /// <param name="pauseNote">Ligne sous le titre de la pause (état des papiers, jalon 4) ; vide : rien.</param>
        /// <param name="pending">Choix en attente : « Terminer la journée » dit quoi régler d'abord et ne fait rien (Kevin, 23/11).</param>
        public MenuChoice Update(MenuScreen screen, GameSettings settings, int passengersOnBoard, string pauseNote = "", PendingChoices pending = default)
        {
            var open = screen != MenuScreen.None;
            _root.gameObject.SetActive(open);
            if (!open) return MenuChoice.None;
            Layout(screen, settings, passengersOnBoard, pauseNote, pending);

            var back = UiKit.KeyDown(KeyAction.Pause);
            switch (screen)
            {
                case MenuScreen.Title:
                    if (UiKit.Tapped(_primary.rectTransform) || UiKit.KeyDown(KeyAction.Action)) return MenuChoice.StartDay;
                    if (UiKit.Tapped(_secondary.rectTransform)) return MenuChoice.OpenSettings;
                    break;
                case MenuScreen.Pause:
                    if (UiKit.Tapped(_primary.rectTransform) || back) return MenuChoice.Resume;
                    if (UiKit.Tapped(_secondary.rectTransform)) return MenuChoice.OpenSettings;
                    if (UiKit.Tapped(_tertiary.rectTransform)) return MenuChoice.EndDay;
                    break;
                case MenuScreen.Settings:
                    if (UiKit.Tapped(_primary.rectTransform) || back) return MenuChoice.CloseSettings;
                    if (UiKit.Tapped(_minus.rectTransform)) return MenuChoice.VolumeDown;
                    if (UiKit.Tapped(_plus.rectTransform)) return MenuChoice.VolumeUp;
                    if (UiKit.Tapped(_toggle.rectTransform)) return MenuChoice.ToggleVibration;
                    break;
            }
            return MenuChoice.None;
        }

        private void Layout(MenuScreen screen, GameSettings settings, int passengersOnBoard, string pauseNote, PendingChoices pending)
        {
            float h = Screen.height;
            var bw = Mathf.Min(h * 0.9f, Screen.width * 0.8f);
            var bh = h * 0.12f;
            var font = Mathf.RoundToInt(h * 0.045f);
            _primaryText.fontSize = _secondaryText.fontSize = _tertiaryText.fontSize = font;

            _title.fontSize = Mathf.RoundToInt(h * (screen == MenuScreen.Title ? 0.15f : 0.09f));
            UiKit.PlaceTopCenter(_title.rectTransform, h * 0.08f, Screen.width, h * 0.18f);
            _subtitle.fontSize = Mathf.RoundToInt(h * 0.04f);
            UiKit.PlaceTopCenter(_subtitle.rectTransform, h * 0.26f, Screen.width, h * 0.06f);
            _info.fontSize = Mathf.RoundToInt(h * 0.032f);

            _subtitle.gameObject.SetActive(screen != MenuScreen.Pause || !string.IsNullOrEmpty(pauseNote));
            _secondary.gameObject.SetActive(screen != MenuScreen.Settings);
            _tertiary.gameObject.SetActive(screen == MenuScreen.Pause);
            _minus.gameObject.SetActive(screen == MenuScreen.Settings);
            _plus.gameObject.SetActive(screen == MenuScreen.Settings);
            // Aide du clavier dans la pause, hors téléphone (éditeur, démo web) : table KeyMap, une touche par action.
            _info.gameObject.SetActive(screen == MenuScreen.Settings || (screen == MenuScreen.Pause && !Application.isMobilePlatform));
            _toggle.gameObject.SetActive(screen == MenuScreen.Settings);
            _toggleText.fontSize = font;

            switch (screen)
            {
                case MenuScreen.Title:
                    _title.text = "Taxi Kmer";
                    _subtitle.text = "Une journée de taxi, de Messassi à Étoudi";
                    _primaryText.text = "Commencer la journée";
                    _secondaryText.text = "Réglages";
                    UiKit.PlaceTopCenter(_primary.rectTransform, h * 0.42f, bw, bh);
                    UiKit.PlaceTopCenter(_secondary.rectTransform, h * 0.58f, bw, bh);
                    break;
                case MenuScreen.Pause:
                    _title.text = "Pause";
                    _subtitle.text = pauseNote ?? "";
                    _subtitle.fontSize = Mathf.RoundToInt(h * 0.032f);
                    _primaryText.text = "Reprendre";
                    _secondaryText.text = "Réglages";
                    _tertiaryText.text = GameMenu.EndDayLabel(passengersOnBoard, pending);
                    _tertiaryText.fontSize = Mathf.RoundToInt(h * (pending.Any ? 0.03f : passengersOnBoard > 0 ? 0.036f : 0.045f));
                    _tertiary.color = pending.Any ? Grey : Red; // bouton grisé : il faut d'abord répondre au choix en attente
                    UiKit.PlaceTopCenter(_primary.rectTransform, h * 0.3f, bw, bh);
                    UiKit.PlaceTopCenter(_secondary.rectTransform, h * 0.46f, bw, bh);
                    UiKit.PlaceTopCenter(_tertiary.rectTransform, h * 0.66f, bw, bh);
                    _info.text = "Clavier : " + KeyMap.Help();
                    _info.fontSize = Mathf.RoundToInt(h * 0.022f);
                    UiKit.PlaceTopCenter(_info.rectTransform, h * 0.8f, Screen.width * 0.96f, h * 0.18f);
                    break;
                case MenuScreen.Settings:
                    _title.text = "Réglages";
                    _subtitle.text = settings.VolumeLabel;
                    UiKit.PlaceTopCenter(_subtitle.rectTransform, h * 0.36f, Screen.width, h * 0.08f);
                    _subtitle.fontSize = font;
                    var side = bw / 2f + bh;
                    UiKit.PlaceTopCenter(_minus.rectTransform, h * 0.34f, bh, bh);
                    _minus.rectTransform.anchoredPosition += new Vector2(-side, 0f);
                    UiKit.PlaceTopCenter(_plus.rectTransform, h * 0.34f, bh, bh);
                    _plus.rectTransform.anchoredPosition += new Vector2(side, 0f);
                    foreach (var t in new[] { _minus, _plus }) t.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(h * 0.07f);
                    _toggleText.text = settings.VibrationLabel;
                    UiKit.PlaceTopCenter(_toggle.rectTransform, h * 0.49f, bw, bh);
                    _info.text = "Qualité graphique : bouton « Qualité » à gauche pendant la conduite (touche G)";
                    UiKit.PlaceTopCenter(_info.rectTransform, h * 0.63f, Screen.width, h * 0.06f);
                    _primaryText.text = "Retour";
                    UiKit.PlaceTopCenter(_primary.rectTransform, h * 0.72f, bw, bh);
                    break;
            }
        }
    }
}
