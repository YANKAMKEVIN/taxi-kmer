using TaxiKmer.Core.Radio;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Radio du taxi à l'écran (affichage minimal) : un bouton en haut à droite, sous les jauges, qui fait défiler
    /// « Radio coupée » → station 1 → station 2 → … (touche N au clavier), et une source audio sur le taxi qui joue la
    /// piste en cours si un son est fourni (Resources), muette sinon. Toute la règle est dans <see cref="CarRadio"/>.
    /// Créé par <see cref="StreetView"/> seulement si radio.json a enabled = true. Non exécuté dans Unity (session cloud).
    /// </summary>
    public sealed class RadioView
    {
        public CarRadio Radio { get; }

        private readonly Image _button;
        private readonly Text _label;
        private readonly AudioSource _source;
        private readonly Text _subtitle;
        private string _playingKey = "";

        public RadioView(RectTransform overlay, Font font, GameObject taxi, RadioConfig config)
        {
            Radio = new CarRadio(config);
            _button = UiKit.NewButton(overlay, font, "", new Color(0.12f, 0.12f, 0.16f, 0.85f), out _label);
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _source = taxi.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = Mathf.Clamp01(config.volume);
            // Annonces, publicités et flashs en texte à l'écran (pistes vides : c'est tout ce que diffuse la radio).
            _subtitle = UiKit.NewText(overlay, font, TextAnchor.UpperRight, new Color(1f, 0.95f, 0.75f));
        }

        /// <param name="inTaxi">Le chauffeur conduit (false : pause, bilan).</param>
        /// <param name="dialogue">Un passager parle : le volume baisse (décision de Kevin du 21/11).</param>
        public void Update(float dt, bool inTaxi, bool dialogue)
        {
            Radio.InTaxi = inTaxi;
            Radio.DialogueActive = dialogue;
            Radio.Update(dt);

            _button.gameObject.SetActive(inTaxi);
            if (inTaxi && (UiKit.Tapped(_button.rectTransform) || UnityEngine.Input.GetKeyDown(KeyCode.N))) Radio.NextStation();

            var h = Screen.height;
            var r = _button.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-h * 0.04f, -h * (ShiftGaugesView.BottomFraction + 0.02f));
            r.sizeDelta = new Vector2(h * 0.5f, h * 0.08f);
            _label.fontSize = Mathf.RoundToInt(h * 0.026f);
            var playing = Radio.NowPlaying;
            _label.text = playing.Length > 0 ? $"{Radio.ButtonLabel}\n<size={Mathf.RoundToInt(h * 0.02f)}>{playing}</size>" : Radio.ButtonLabel;
            _label.supportRichText = true;

            var text = Radio.ScreenText;
            _subtitle.gameObject.SetActive(inTaxi && text.Length > 0);
            if (text.Length > 0)
            {
                var sr = _subtitle.rectTransform;
                sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1f, 1f);
                sr.anchoredPosition = new Vector2(-h * 0.04f, -h * (ShiftGaugesView.BottomFraction + 0.11f));
                sr.sizeDelta = new Vector2(h * 0.7f, h * 0.12f);
                _subtitle.fontSize = Mathf.RoundToInt(h * 0.024f);
                _subtitle.text = $"« {text} »";
            }
            _source.volume = Radio.Volume;

            UpdateSound();
        }

        /// <summary>Joue la piste en cours, reprise au bon endroit (la station est en direct) ; silence sinon.</summary>
        private void UpdateSound()
        {
            var programme = Radio.HasSound ? Radio.CurrentProgramme : null;
            var key = programme != null ? Radio.Station.id + "/" + programme.id : "";
            if (key == _playingKey) return;
            _playingKey = key;
            _source.Stop();
            if (programme == null) return;
            var clip = Resources.Load<AudioClip>(programme.clip);
            if (clip == null)
            {
                Debug.LogWarning($"[TaxiKmer] Radio : son Resources/{programme.clip} introuvable (à fournir par Kevin).");
                return;
            }
            _source.clip = clip;
            _source.time = Mathf.Clamp(Radio.ProgrammeOffset, 0f, Mathf.Max(0f, clip.length - 0.05f));
            _source.Play();
        }
    }
}
