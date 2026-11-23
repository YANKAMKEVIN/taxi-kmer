using System.Collections.Generic;
using TaxiKmer.Core.Police;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Contrôle de police à l'écran (jalon 4, activé le 21/11 après les décisions de Kevin ; affichage minimal) : quand
    /// le taxi passe devant un contrôle qui l'arrête, un panneau au centre montre la phrase du policier et un bouton par
    /// choix (payer, négocier, contester, appeler quelqu'un, fuir ; touches 1 à 5). Le contrôle fini, son effet est
    /// appliqué à la journée (<see cref="Street.ApplyPoliceControl"/>) et la phrase de fin s'affiche. Toute la règle est
    /// dans <see cref="PoliceDay"/> et <see cref="PoliceControl"/>. Créé par <see cref="StreetView"/> seulement si
    /// police.json a enabled = true. Non exécuté dans Unity (session cloud).
    /// </summary>
    public sealed class PoliceView
    {
        public PoliceDay Police { get; }
        /// <summary>Un contrôle est en cours : la conduite est bloquée (sauf pendant une poursuite, idée n° 9).</summary>
        public bool Active => Police.Current != null && !Police.Current.Chasing;
        /// <summary>Poursuite en cours (null sinon) : la voiture de police est à <see cref="PoliceChase.PoliceAlong"/>.</summary>
        public PoliceChase Chase { get; private set; }

        private readonly PoliceConfig _config;
        private readonly Image _panel;
        private readonly Text _text;
        private readonly List<Image> _buttons = new List<Image>();
        private readonly List<Text> _labels = new List<Text>();
        private readonly List<ControlChoice> _choices = new List<ControlChoice>();
        private float _lastAlong = -1f;
        private readonly Text _banner;

        /// <param name="papers">Papiers de la carrière (sauvegardés, renouvelés au début de la journée) ; null : ceux du début de carrière.</param>
        public PoliceView(RectTransform overlay, Font font, PoliceConfig config, PaperSet papers, int day, float routeLength, int startMinute, int endMinute, int seed)
        {
            _config = config;
            Police = new PoliceDay(config, papers ?? new PaperSet(config), new PoliceContacts(config), day, routeLength, startMinute, endMinute, new System.Random(seed));
            _panel = UiKit.NewImage(overlay, "Contrôle de police", new Color(0.08f, 0.1f, 0.18f, 0.92f), UiKit.Rounded);
            _text = UiKit.NewText(_panel.transform, font, TextAnchor.UpperCenter, Color.white);
            for (var i = 0; i < 5; i++)
            {
                _buttons.Add(UiKit.NewButton(_panel.transform, font, "", new Color(0.2f, 0.3f, 0.5f, 0.95f), out var label));
                _labels.Add(label);
            }
            _panel.gameObject.SetActive(false);
            _banner = UiKit.NewText(overlay, font, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.3f));
            _banner.gameObject.AddComponent<Outline>().effectColor = Color.black;
            _banner.gameObject.SetActive(false);
        }

        /// <summary>
        /// À chaque image : cherche un contrôle sur le trajet du taxi, puis fait choisir le joueur. Rend le message de fin
        /// d'un contrôle terminé (vide sinon), pour les messages à l'écran.
        /// </summary>
        public string Update(Street street, TaxiOnRoute state, bool driving)
        {
            var done = "";
            if (street.DayFinished || !driving)
            {
                _panel.gameObject.SetActive(false);
                _lastAlong = state.Along;
                return done;
            }
            if (!Active && _lastAlong >= 0f)
            {
                var cabin = street.Shift.Cabin;
                var factor = street.Reputation?.PoliceStopFactor ?? 1f; // effet de la réputation (Kevin, 21/11)
                Police.Update(_lastAlong, state.Along, street.Shift.Clock.GameMinutes, cabin.PassengerCount, cabin.Front.Passengers.Count, factor);
            }
            _lastAlong = state.Along;

            var control = Police.Current;
            if (control != null && control.Chasing) UpdateChase(control, state);
            if (control != null && control.Closed) done = Finish(street);
            else if (control != null)
            {
                _choices.Clear();
                _choices.AddRange(control.AvailableChoices());
                for (var i = 0; i < _buttons.Count; i++)
                {
                    var visible = i < _choices.Count;
                    _buttons[i].gameObject.SetActive(visible);
                    if (!visible) continue;
                    _labels[i].text = PoliceTexts.Choice(_choices[i]);
                    if (UiKit.Tapped(_buttons[i].rectTransform) || UiKit.KeyDown(KeyAction.Police1 + i))
                    {
                        control.Choose(_choices[i]);
                        break;
                    }
                }
                if (control.Closed) done = Finish(street);
            }

            _panel.gameObject.SetActive(Active);
            if (Active) Layout(Police.Current);
            UpdateBanner();
            return done;
        }

        /// <summary>Poursuite (idée n° 9) : la police suit le taxi ; rattrapé ou échappé, le contrôle se termine.</summary>
        private void UpdateChase(PoliceControl control, TaxiOnRoute state)
        {
            if (Chase == null) Chase = new PoliceChase(_config.chase, state.Along, state.Direction);
            var result = Chase.Update(Time.deltaTime, state.Along);
            if (result == ChaseState.Running) return;
            control.EndChase(result == ChaseState.Caught);
            Chase = null;
        }

        private void UpdateBanner()
        {
            var text = Chase != null ? PoliceChase.Banner(Chase.Gap) : Police.Schedule.Wanted ? PoliceChase.WantedBanner : "";
            _banner.gameObject.SetActive(text.Length > 0);
            if (text.Length == 0) return;
            _banner.text = text;
            var h = Screen.height;
            _banner.fontSize = Mathf.RoundToInt(h * 0.035f);
            _banner.color = Chase != null ? new Color(1f, 0.35f, 0.3f, 0.6f + 0.4f * UiKit.Pulse(8f)) : new Color(1f, 0.6f, 0.3f);
            UiKit.PlaceTopCenter(_banner.rectTransform, h * 0.3f, h * 1.2f, h * 0.06f);
        }

        private string Finish(Street street)
        {
            var control = Police.Close();
            if (control == null) return "";
            street.ApplyPoliceControl(control, _config.impoundFee);
            return PoliceTexts.Closed(control);
        }

        private void Layout(PoliceControl control)
        {
            var h = Screen.height;
            var w = h * 1.1f;
            UiKit.PlaceTopCenter(_panel.rectTransform, h * 0.18f, w, h * 0.5f);
            var post = Police.CurrentPost != null ? Police.CurrentPost.Name : "Contrôle";
            _text.text = $"{post}\n{PoliceTexts.Demand(control)}";
            _text.fontSize = Mathf.RoundToInt(h * 0.032f);
            UiKit.PlaceTopCenter(_text.rectTransform, h * 0.03f, w * 0.92f, h * 0.2f);
            var bw = w * 0.3f;
            var bh = h * 0.09f;
            for (var i = 0; i < _buttons.Count; i++)
            {
                var r = _buttons[i].rectTransform;
                var row = i / 3;
                var col = i % 3;
                UiKit.PlaceTopCenter(r, h * 0.25f + row * (bh + h * 0.02f), bw, bh);
                r.anchoredPosition = new Vector2((col - 1) * (bw + w * 0.02f), r.anchoredPosition.y);
                _labels[i].fontSize = Mathf.RoundToInt(h * 0.028f);
            }
        }
    }
}
