using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Talk;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Passagers qui parlent (jalon 5, activé le 21/11 ; affichage minimal) : une bulle en bas au centre avec la réplique
    /// et 2 ou 3 réponses rapides (touches F1 à F3), le petit son de voix s'il est fourni (emplacements « à fournir »),
    /// et deux panneaux de dispute d'argent : la contre-offre d'un client (accepter / refuser, touches O / N) et le
    /// passager qui refuse de payer (laisser partir / appeler la police, touches L / P). Toute la règle est dans
    /// <see cref="DialogueDirector"/> et <see cref="Street"/>. Créé par <see cref="StreetView"/> seulement si
    /// dialogues.json a enabled = true. Non exécuté dans Unity (session cloud).
    /// </summary>
    public sealed class TalkView
    {
        public DialogueDirector Director { get; }
        /// <summary>Un passager parle (la radio baisse le volume).</summary>
        public bool Speaking => Director.Current != null;

        private readonly DialogueConfig _config;
        private readonly DetourWatch _detour;
        private readonly Image _bubble;
        private readonly Text _text;
        private readonly List<Image> _replies = new List<Image>();
        private readonly List<Text> _replyLabels = new List<Text>();
        private readonly Image _dispute;
        private readonly Text _disputeText;
        private readonly Image _yes, _no;
        private readonly Text _yesText, _noText;
        private readonly AudioSource _voice;
        private Bubble _voiced;
        private readonly Dictionary<string, AudioClip> _provisional = new Dictionary<string, AudioClip>();
        private HaggleOffer _haggleShown;
        private PaymentRefusal _refusalShown;

        public TalkView(RectTransform overlay, Font font, GameObject taxi, DialogueConfig config, int seed)
        {
            _config = config;
            Director = new DialogueDirector(config, new System.Random(seed));
            _detour = new DetourWatch(config.detourMeters);
            _bubble = UiKit.NewImage(overlay, "Bulle de passager", new Color(0.05f, 0.05f, 0.08f, 0.85f), UiKit.Rounded);
            _text = UiKit.NewText(_bubble.transform, font, TextAnchor.UpperCenter, Color.white);
            for (var i = 0; i < 3; i++)
            {
                _replies.Add(UiKit.NewButton(_bubble.transform, font, "", new Color(0.2f, 0.35f, 0.55f, 0.95f), out var label));
                _replyLabels.Add(label);
            }
            _dispute = UiKit.NewImage(overlay, "Dispute d'argent", new Color(0.1f, 0.08f, 0.05f, 0.92f), UiKit.Rounded);
            _disputeText = UiKit.NewText(_dispute.transform, font, TextAnchor.UpperCenter, Color.white);
            _yes = UiKit.NewButton(_dispute.transform, font, "", new Color(0.12f, 0.55f, 0.25f, 0.95f), out _yesText);
            _no = UiKit.NewButton(_dispute.transform, font, "", new Color(0.55f, 0.2f, 0.15f, 0.95f), out _noText);
            _bubble.gameObject.SetActive(false);
            _dispute.gameObject.SetActive(false);
            _voice = taxi.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
        }

        /// <summary>Une situation se produit (montée, paiement, panne, contrôle, pluie…) : un passager parle peut-être.</summary>
        /// <param name="values">Valeurs que la réplique peut citer (destination, prix, offre : <see cref="TalkValues"/>).</param>
        public void Say(string trigger, string passengerId = "", IDictionary<string, string> values = null) => Director.Trigger(trigger, passengerId ?? "", "", values);

        /// <summary>
        /// Valeurs d'une course pour les répliques : destination sous ses formes parlées (« au dispensaire de Messassi »,
        /// décision de Kevin du 23/11) et prix, plus la contre-offre si elle existe (<see cref="TalkValues.ForRide"/>).
        /// </summary>
        public static Dictionary<string, string> RideValues(Street street, string destinationStopId, int fare, int offer = 0) =>
            TalkValues.ForRide(street.FindStop(destinationStopId), destinationStopId, fare, offer);

        /// <summary>Un passager à bord, n'importe lequel (pour panne, contrôle, pluie).</summary>
        public static string AnyPassenger(Street street)
        {
            foreach (var p in street.Shift.Cabin.Passengers) return p.Id;
            return "";
        }

        /// <summary>À chaque image. Rend un message pour l'écran (issue d'une dispute), vide sinon.</summary>
        public string Update(float dt, Street street, TaxiOnRoute state, bool driving)
        {
            var message = "";
            var cabin = street.Shift.Cabin;
            var ids = new List<string>();
            foreach (var p in cabin.Passengers) ids.Add(p.Id);
            Director.SetPassengers(ids);
            Director.MinuteOfDay = street.Shift.Clock.GameMinutes; // répliques liées à l'heure (« Bonsoir » le soir)

            if (driving && !street.DayFinished)
            {
                var gone = Director.Update(dt, cabin.PassengerCount);
                // Effet des réponses du chauffeur (Kevin, 21/11) : réputation et pourboire.
                if (gone?.Reply != null) street.ApplyReply(gone.SpeakerId, gone.Reply.reputation, gone.Reply.tip);
                foreach (var p in cabin.Passengers)
                {
                    var stop = street.FindStop(p.DestinationStopId);
                    if (stop != null && _detour.Update(p.Id, stop.distanceAlongRouteMeters, state.Along)) Say(TalkTriggers.Detour, p.Id, RideValues(street, p.DestinationStopId, p.ProposedFare));
                }
                message = UpdateDisputes(street);
            }
            UpdateBubble(driving && !street.DayFinished);
            UpdateVoice();
            return message;
        }

        /// <summary>Un passager est descendu : on oublie son détour.</summary>
        public void Forget(string passengerId) => _detour.Forget(passengerId);

        private string UpdateDisputes(Street street)
        {
            var h = street.PendingHaggle;
            var r = street.PendingRefusal;
            var show = h != null || r != null;
            _dispute.gameObject.SetActive(show);
            if (!show) { _haggleShown = null; _refusalShown = null; return ""; }
            var hgt = Screen.height;
            UiKit.PlaceTopCenter(_dispute.rectTransform, hgt * 0.2f, hgt * 0.9f, hgt * 0.26f);
            UiKit.PlaceTopCenter(_disputeText.rectTransform, hgt * 0.02f, hgt * 0.85f, hgt * 0.1f);
            _disputeText.fontSize = _yesText.fontSize = _noText.fontSize = Mathf.RoundToInt(hgt * 0.03f);
            UiKit.PlaceTopCenter(_yes.rectTransform, hgt * 0.14f, hgt * 0.38f, hgt * 0.09f);
            _yes.rectTransform.anchoredPosition = new Vector2(-hgt * 0.21f, -hgt * 0.14f);
            UiKit.PlaceTopCenter(_no.rectTransform, hgt * 0.14f, hgt * 0.38f, hgt * 0.09f);
            _no.rectTransform.anchoredPosition = new Vector2(hgt * 0.21f, -hgt * 0.14f);

            if (r != null)
            {
                if (_refusalShown != r) { _refusalShown = r; Say(TalkTriggers.RefusesToPay, r.Client.Id, RideValues(street, r.Client.DestinationStopId, r.Fare)); }
                _disputeText.text = $"Le passager refuse de payer {DaySummary.Money(r.Fare)}.";
                _yesText.text = "Laisser partir";
                _noText.text = "Appeler la police";
                if (UiKit.Tapped(_yes.rectTransform) || UiKit.KeyDown(KeyAction.RefusalLetGo)) { street.ResolveRefusal(false); return "Il part sans payer."; }
                if (UiKit.Tapped(_no.rectTransform) || UiKit.KeyDown(KeyAction.RefusalCallPolice))
                {
                    var o = street.ResolveRefusal(true);
                    return o.Paid ? $"La police est venue : il a payé {DaySummary.Money(o.Amount)}." : "La police est venue, mais il n'a rien payé.";
                }
                return "";
            }

            if (_haggleShown != h) { _haggleShown = h; Say(TalkTriggers.Haggle, h.Client.Client.Id, RideValues(street, h.Client.Client.DestinationStopId, h.Asked, h.Offer)); }
            _disputeText.text = $"Le client propose {DaySummary.Money(h.Offer)} au lieu de {DaySummary.Money(h.Asked)}.";
            _yesText.text = "Accepter";
            _noText.text = "Refuser";
            if (UiKit.Tapped(_yes.rectTransform) || UiKit.KeyDown(KeyAction.HaggleAccept)) { street.AnswerHaggle(true); return $"D'accord pour {DaySummary.Money(h.Offer)}."; }
            if (UiKit.Tapped(_no.rectTransform) || UiKit.KeyDown(KeyAction.HaggleRefuse)) { street.AnswerHaggle(false); return "Refusé : il attend un autre taxi."; }
            return "";
        }

        private void UpdateBubble(bool visible)
        {
            var b = Director.Current;
            _bubble.gameObject.SetActive(visible && b != null);
            if (!visible || b == null) return;
            var h = Screen.height;
            // Entre les flèches (gauche) et les pédales (droite) : la bulle les recouvrait (vu le 27/11 au format du S24).
            var w = Mathf.Clamp(Screen.width - h * 1.3f, h * 0.8f, h * 1.2f);
            var r = _bubble.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, h * 0.2f);
            r.sizeDelta = new Vector2(w, h * 0.2f);
            _text.text = b.Text;
            _text.fontSize = Mathf.RoundToInt(h * 0.032f);
            _text.resizeTextForBestFit = true;
            _text.resizeTextMaxSize = _text.fontSize;
            _text.resizeTextMinSize = Mathf.RoundToInt(h * 0.022f);
            UiKit.PlaceTopCenter(_text.rectTransform, h * 0.015f, w * 0.94f, h * 0.08f);
            var open = b.RepliesOpen(_config.replySeconds);
            var replies = b.Line.replies ?? new ReplyData[0];
            for (var i = 0; i < _replies.Count; i++)
            {
                var show = open && i < replies.Length;
                _replies[i].gameObject.SetActive(show);
                if (!show) continue;
                _replyLabels[i].text = replies[i].text;
                _replyLabels[i].fontSize = Mathf.RoundToInt(h * 0.026f);
                var bw = w * 0.31f;
                UiKit.PlaceTopCenter(_replies[i].rectTransform, h * 0.1f, bw, h * 0.08f);
                _replies[i].rectTransform.anchoredPosition = new Vector2((i - (replies.Length - 1) * 0.5f) * (bw + w * 0.02f), -h * 0.1f);
                if (UiKit.Tapped(_replies[i].rectTransform) || UiKit.KeyDown(KeyAction.Reply1 + i)) Director.Answer(replies[i].id);
            }
        }

        /// <summary>Petit son de voix (« hmm », « eh ! ») au début d'une bulle, s'il est fourni (sinon rien).</summary>
        private void UpdateVoice()
        {
            var b = Director.Current;
            if (b == _voiced) return;
            _voiced = b;
            if (b?.Voice == null) return;
            if (string.IsNullOrEmpty(b.Voice.clip))
            {
                // Son « à fournir » : voix synthétique provisoire (SoundSynth.Voice), en attendant l'enregistrement.
                if (!_provisional.TryGetValue(b.Voice.id, out var synth))
                {
                    var data = TaxiKmer.Core.Ux.SoundSynth.Voice(b.Voice.id);
                    synth = null;
                    if (data != null)
                    {
                        synth = AudioClip.Create("Voix provisoire " + b.Voice.id, data.Length, 1, TaxiKmer.Core.Ux.SoundSynth.Rate, false);
                        synth.SetData(data, 0);
                    }
                    _provisional[b.Voice.id] = synth;
                }
                if (synth != null) _voice.PlayOneShot(synth);
                return;
            }
            var clip = Resources.Load<AudioClip>(b.Voice.clip);
            if (clip == null) { Debug.LogWarning($"[TaxiKmer] Voix : son Resources/{b.Voice.clip} introuvable (à fournir)."); return; }
            _voice.PlayOneShot(clip);
        }
    }
}
