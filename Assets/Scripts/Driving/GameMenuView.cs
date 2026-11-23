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
        /// <summary>« Nouvelle partie » sur l'écran titre (idée n° 10) : premier appui = demande de confirmation.</summary>
        NewCareer,
        /// <summary>« Mode photo » dans la pause (idée n° 27).</summary>
        OpenPhoto,
        /// <summary>Retour du mode photo à la pause.</summary>
        ClosePhoto,
        /// <summary>Une ligne des réglages (idées n° 29 à 31) : <see cref="GameMenuView.RowId"/> dit laquelle.</summary>
        SettingRow,
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
        private readonly Image _photo;
        private readonly Font _font;
        private readonly System.Collections.Generic.List<(Image button, Text text)> _rows = new System.Collections.Generic.List<(Image, Text)>();
        private System.Collections.Generic.List<SettingRow> _rowData = new System.Collections.Generic.List<SettingRow>();

        /// <summary>Ligne des réglages touchée (avec <see cref="MenuChoice.SettingRow"/>).</summary>
        public string RowId { get; private set; } = "";

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
            _photo = UiKit.NewButton(_root, font, "Mode photo", Grey, out _);
            _font = font;
            _toggle.gameObject.SetActive(false); // remplacé par les lignes des réglages
        }

        /// <param name="pauseNote">Ligne sous le titre de la pause (état des papiers, jalon 4) ; vide : rien.</param>
        /// <param name="pending">Choix en attente : « Terminer la journée » dit quoi régler d'abord et ne fait rien (Kevin, 23/11).</param>
        /// <summary>Carrière lue dans la sauvegarde, pour l'écran titre (idée n° 10) ; null : première partie.</summary>
        public Core.Save.SaveData Career { get; set; }
        /// <summary>« Nouvelle partie » attend sa confirmation.</summary>
        public bool ConfirmingNewCareer { get; set; }

        public MenuChoice Update(MenuScreen screen, GameSettings settings, int passengersOnBoard, string pauseNote = "", PendingChoices pending = default)
        {
            var open = screen != MenuScreen.None && screen != MenuScreen.Photo; // le mode photo a sa propre interface
            _root.gameObject.SetActive(open);
            if (!open) return MenuChoice.None;
            Layout(screen, settings, passengersOnBoard, pauseNote, pending);

            var back = UiKit.KeyDown(KeyAction.Pause);
            switch (screen)
            {
                case MenuScreen.Title:
                    if (UiKit.Tapped(_primary.rectTransform) || UiKit.KeyDown(KeyAction.Action)) return MenuChoice.StartDay;
                    if (UiKit.Tapped(_secondary.rectTransform)) return MenuChoice.OpenSettings;
                    if (TitleScreen.HasCareer(Career) && UiKit.Tapped(_tertiary.rectTransform)) return MenuChoice.NewCareer;
                    break;
                case MenuScreen.Pause:
                    if (UiKit.Tapped(_primary.rectTransform) || back) return MenuChoice.Resume;
                    if (UiKit.Tapped(_secondary.rectTransform)) return MenuChoice.OpenSettings;
                    if (UiKit.Tapped(_tertiary.rectTransform)) return MenuChoice.EndDay;
                    if (UiKit.Tapped(_photo.rectTransform)) return MenuChoice.OpenPhoto;
                    break;
                case MenuScreen.Settings:
                    if (UiKit.Tapped(_primary.rectTransform) || back) return MenuChoice.CloseSettings;
                    if (UiKit.Tapped(_minus.rectTransform)) return MenuChoice.VolumeDown;
                    if (UiKit.Tapped(_plus.rectTransform)) return MenuChoice.VolumeUp;
                    for (var i = 0; i < _rowData.Count && i < _rows.Count; i++)
                        if (UiKit.Tapped(_rows[i].button.rectTransform))
                        {
                            RowId = _rowData[i].Id;
                            return MenuChoice.SettingRow;
                        }
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
            _tertiary.gameObject.SetActive(screen == MenuScreen.Pause || (screen == MenuScreen.Title && TitleScreen.HasCareer(Career)));
            _minus.gameObject.SetActive(screen == MenuScreen.Settings);
            _plus.gameObject.SetActive(screen == MenuScreen.Settings);
            // Aide du clavier dans la pause, hors téléphone (éditeur, démo web) : table KeyMap, une touche par action.
            _info.gameObject.SetActive(screen == MenuScreen.Settings || (screen == MenuScreen.Pause && !Application.isMobilePlatform));
            _toggle.gameObject.SetActive(false);
            _rowData = screen == MenuScreen.Settings ? settings.Rows() : new System.Collections.Generic.List<SettingRow>();
            while (_rows.Count < _rowData.Count)
            {
                var b = UiKit.NewButton(_root, _font, "", Grey, out var t);
                _rows.Add((b, t));
            }
            for (var i = 0; i < _rows.Count; i++) _rows[i].button.gameObject.SetActive(i < _rowData.Count);
            _photo.gameObject.SetActive(screen == MenuScreen.Pause);
            if (screen == MenuScreen.Pause)
            {
                UiKit.PlaceTopLeft(_photo.rectTransform, Screen.width - h * 0.34f, h * 0.04f, h * 0.3f, h * 0.1f);
                _photo.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(h * 0.035f);
            }
            _toggleText.fontSize = font;

            switch (screen)
            {
                case MenuScreen.Title:
                    _title.text = "Taxi Kmer";
                    _subtitle.text = TitleScreen.Subtitle(Career);
                    _primaryText.text = TitleScreen.PrimaryLabel(Career);
                    _secondaryText.text = "Réglages";
                    _tertiaryText.text = TitleScreen.NewCareerLabel(ConfirmingNewCareer);
                    _tertiaryText.fontSize = Mathf.RoundToInt(h * (ConfirmingNewCareer ? 0.03f : 0.04f));
                    _tertiary.color = Red;
                    UiKit.PlaceTopCenter(_primary.rectTransform, h * 0.4f, bw, bh);
                    UiKit.PlaceTopCenter(_secondary.rectTransform, h * 0.55f, bw, bh);
                    UiKit.PlaceTopCenter(_tertiary.rectTransform, h * 0.72f, bw, bh * 0.8f);
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
                    // Lignes des réglages sur deux colonnes (idées n° 29 à 31) : un appui fait défiler la valeur.
                    var cw = Mathf.Min(h * 0.75f, Screen.width * 0.46f);
                    var rh = h * 0.08f;
                    for (var i = 0; i < _rowData.Count; i++)
                    {
                        var (button, text) = _rows[i];
                        text.text = _rowData[i].Label;
                        text.fontSize = Mathf.RoundToInt(h * 0.03f);
                        UiKit.PlaceTopCenter(button.rectTransform, h * 0.47f + (i / 2) * (rh + h * 0.015f), cw, rh);
                        button.rectTransform.anchoredPosition += new Vector2((i % 2 == 0 ? -1f : 1f) * (cw / 2f + h * 0.01f), 0f);
                    }
                    _info.gameObject.SetActive(false);
                    _primaryText.text = "Retour";
                    UiKit.PlaceTopCenter(_primary.rectTransform, h * 0.47f + ((_rowData.Count + 1) / 2) * (rh + h * 0.015f) + h * 0.01f, bw, bh * 0.8f);
                    break;
            }
        }
    }
}
