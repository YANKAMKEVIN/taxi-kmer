using System.Text;
using TaxiKmer.Core.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Téléphone du chauffeur (idée n° 11, première tranche) : bouton « Téléphone (n) » à gauche, sous les jauges ; un
    /// appui ouvre l'écran des messages (le plus récent en haut) et les marque lus, un second le referme. Règles dans
    /// <see cref="PhoneInbox"/> et <see cref="PhoneMessages"/>. Créé par <see cref="StreetView"/> si phone.json a
    /// enabled = true ; non exécuté dans Unity (session cloud).
    /// </summary>
    public sealed class PhoneView
    {
        public PhoneInbox Inbox { get; } = new PhoneInbox();
        public bool Open { get; private set; }

        private readonly Image _button;
        private readonly Text _buttonText;
        private readonly Image _screen;
        private readonly Text _screenText;

        public PhoneView(RectTransform overlay, Font font)
        {
            _button = UiKit.NewButton(overlay, font, "Téléphone", new Color(0.12f, 0.14f, 0.2f, 0.9f), out _buttonText);
            _screen = UiKit.NewImage(overlay, "Écran du téléphone", new Color(0.05f, 0.06f, 0.09f, 0.96f), UiKit.Rounded);
            _screenText = UiKit.NewText(_screen.transform, font, TextAnchor.UpperLeft, Color.white);
            _screen.gameObject.SetActive(false);
        }

        public void Update(bool visible)
        {
            _button.gameObject.SetActive(visible);
            if (!visible)
            {
                _screen.gameObject.SetActive(false);
                Open = false;
                return;
            }
            var h = Screen.height;
            UiKit.PlaceTopLeft(_button.rectTransform, h * 0.04f, h * 0.46f, h * 0.3f, h * 0.075f);
            _buttonText.text = Inbox.ButtonLabel;
            _buttonText.fontSize = Mathf.RoundToInt(h * 0.032f);
            _button.color = Inbox.Unread > 0 ? new Color(0.1f, 0.45f, 0.2f, 0.75f + 0.25f * UiKit.Pulse(4f)) : new Color(0.12f, 0.14f, 0.2f, 0.9f);
            if (UiKit.Tapped(_button.rectTransform))
            {
                Open = !Open;
                if (Open) Inbox.MarkAllRead();
            }
            _screen.gameObject.SetActive(Open);
            if (!Open) return;
            UiKit.PlaceTopLeft(_screen.rectTransform, h * 0.36f, h * 0.2f, h * 0.62f, h * 0.62f);
            UiKit.Fill(_screenText.rectTransform, h * 0.03f);
            _screenText.fontSize = Mathf.RoundToInt(h * 0.03f);
            var sb = new StringBuilder();
            if (Inbox.Messages.Count == 0) sb.Append("Aucun message.");
            foreach (var m in Inbox.Messages)
                sb.Append("<b>").Append(m.FromName).Append("</b> · jour ").Append(m.Day).Append('\n').Append(m.Text).Append("\n\n");
            _screenText.supportRichText = true;
            _screenText.text = sb.ToString();
        }
    }
}
