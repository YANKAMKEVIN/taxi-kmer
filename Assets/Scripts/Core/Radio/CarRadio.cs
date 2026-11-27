using System;

namespace TaxiKmer.Core.Radio
{
    /// <summary>Une piste ou une émission d'une station. Aucun son n'est fourni : <see cref="clip"/> vide = à fournir par Kevin.</summary>
    [Serializable]
    public class RadioProgramData
    {
        public string id = "";
        /// <summary>Titre affiché (« Piste 1 (à fournir par Kevin) »).</summary>
        public string title = "";
        /// <summary>musique, emission, publicite, flash (information) : pour l'affichage et les questions de licence.</summary>
        public string kind = "musique";
        /// <summary>Chemin du son sous Resources (sans extension) ; vide : emplacement vide, la radio reste muette.</summary>
        public string clip = "";
        /// <summary>Durée (s réelles) ; 0 : <see cref="RadioConfig.defaultProgramSeconds"/>.</summary>
        public float seconds;
        /// <summary>
        /// Texte de l'animateur ou de la publicité, affiché à l'écran (sous-titre) ; sans son, c'est tout ce qu'on a de
        /// l'émission (la radio diffuse alors seulement les annonces, en texte).
        /// </summary>
        public string text = "";
        /// <summary>Langue du texte (fr, camfranglais, pidgin).</summary>
        public string register = "";
        /// <summary>« proposée » : texte écrit par Claude, à valider par Kevin.</summary>
        public string statut = "";
        /// <summary>« à traduire » : écrit en français faute de tournure sûre dans le registre visé.</summary>
        public string traduction = "";
        /// <summary>Musique ou son fourni : licence (CC0 ou CC-BY seulement, voir radio.md), crédit (auteur, titre) et source (adresse).</summary>
        public string licence = "";
        public string credit = "";
        public string source = "";
    }

    /// <summary>
    /// Un flash d'information lié à un événement du jeu (embouteillage, contrôle, pluie, marché), proposé par Claude.
    /// <see cref="text"/> peut contenir « {lieu} », remplacé par l'endroit de l'événement.
    /// </summary>
    [Serializable]
    public class RadioFlashData
    {
        public string id = "";
        /// <summary>Événement : <see cref="RadioEvents"/>.</summary>
        public string @event = "";
        public string text = "";
        public string register = "";
        public string statut = "";
        public string traduction = "";
    }

    /// <summary>Événements du jeu qui déclenchent un flash d'information.</summary>
    public static class RadioEvents
    {
        public const string TrafficJam = "embouteillage";
        public const string Control = "controle";
        public const string Rain = "pluie";
        public const string Market = "marche";
        public static readonly string[] All = { TrafficJam, Control, Rain, Market };
    }

    /// <summary>Une station de radio : nom, fréquence, grille de pistes ou d'émissions qui tourne en boucle.</summary>
    [Serializable]
    public class RadioStationData
    {
        public string id = "";
        public string name = "";
        /// <summary>Fréquence FM (MHz) ; 0 : à fournir par Kevin (affichée « FM ? »).</summary>
        public float frequencyMHz;
        /// <summary>« proposé » : station fictive proposée par Claude (nom et fréquence inventés), à valider par Kevin.</summary>
        public string statut = "";
        public RadioProgramData[] programmes = Array.Empty<RadioProgramData>();
    }

    /// <summary>
    /// Radio du taxi (priorité cochée par Kevin le 2026-11-21 ; données : Assets/Resources/Data/radio.json). Décisions de
    /// Kevin du 21/11 : stations fictives, musique libre (CC0 ou CC-BY), animateurs en trois langues, publicités et flashs
    /// liés au jeu, volume baissé quand un passager parle ; activée avec des pistes vides (textes à l'écran).
    /// </summary>
    [Serializable]
    public class RadioConfig
    {
        /// <summary>Bouton radio dans le taxi (false : mécanisme prêt, coupé en attendant stations et musiques).</summary>
        public bool enabled;
        /// <summary>La radio est allumée (première station) au début de la journée.</summary>
        public bool startsOn;
        /// <summary>Durée d'une piste dont la durée n'est pas donnée (s réelles).</summary>
        public float defaultProgramSeconds = 180f;
        /// <summary>Volume de la radio (0 à 1), sous le volume général du jeu.</summary>
        public float volume = 0.6f;
        /// <summary>Quand un passager parle, le volume baisse à cette part (tranché par Kevin le 21/11 : baisser, pas se taire).</summary>
        public float duckVolume = 0.3f;
        /// <summary>
        /// La grille saute les programmes sans son ni texte (pistes vides) : avec des pistes vides, la radio diffuse
        /// seulement les annonces et les flashs, en texte à l'écran (décision de Kevin du 21/11).
        /// </summary>
        public bool skipSilentProgrammes;
        /// <summary>Un flash d'information reste … s réelles à l'écran (il passe devant le programme en cours).</summary>
        public float flashSeconds = 12f;
        public RadioStationData[] stations = Array.Empty<RadioStationData>();
        /// <summary>Flashs d'information liés au jeu (embouteillages, contrôles, pluie, marché).</summary>
        public RadioFlashData[] flashes = Array.Empty<RadioFlashData>();
    }

    /// <summary>
    /// Logique de la radio : on change de station dans le taxi (coupée → station 1 → station 2 → … → coupée) ; chaque
    /// station est « en direct » (sa grille avance même quand on ne l'écoute pas : on retombe au milieu d'une piste) ;
    /// la radio se tait quand on sort du taxi, en pause, au bilan, et pendant qu'un passager parle. La présentation lit
    /// <see cref="Audible"/>, <see cref="CurrentProgramme"/> et <see cref="ProgrammeOffset"/>.
    /// </summary>
    public sealed class CarRadio
    {
        private readonly RadioConfig _config;
        private float _broadcastSeconds;

        public CarRadio(RadioConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            StationIndex = config.startsOn && StationCount > 0 ? 0 : -1;
        }

        public RadioConfig Config => _config;
        public bool Enabled => _config.enabled;
        public int StationCount => _config.stations?.Length ?? 0;

        /// <summary>Station écoutée (-1 : radio coupée).</summary>
        public int StationIndex { get; private set; }
        public bool On => StationIndex >= 0;
        public RadioStationData Station => On ? _config.stations[StationIndex] : null;

        /// <summary>Le chauffeur est dans son taxi (false : dehors, en pause, au bilan).</summary>
        public bool InTaxi { get; set; } = true;
        /// <summary>Un passager parle (bulle de dialogue) : le volume baisse le temps de la réplique (décision de Kevin du 21/11).</summary>
        public bool DialogueActive { get; set; }

        /// <summary>On doit entendre la radio en ce moment (même quand un passager parle : elle baisse seulement).</summary>
        public bool Audible => _config.enabled && On && InTaxi;

        /// <summary>Volume à donner à la source (0 si on ne l'entend pas ; baissé quand un passager parle).</summary>
        public float Volume => !Audible ? 0f : Math.Max(0f, Math.Min(1f, _config.volume)) * (DialogueActive ? Math.Max(0f, Math.Min(1f, _config.duckVolume)) : 1f);

        /// <summary>Le programme en cours a un son fourni (sinon : emplacement vide, silence).</summary>
        public bool HasSound => Audible && CurrentProgramme != null && !string.IsNullOrEmpty(CurrentProgramme.clip);

        /// <summary>Bouton : station suivante (coupée après la dernière). Rend la station écoutée (null : coupée).</summary>
        public RadioStationData NextStation()
        {
            if (StationCount == 0) { StationIndex = -1; return null; }
            StationIndex = StationIndex + 1 >= StationCount ? -1 : StationIndex + 1;
            return Station;
        }

        public void TurnOff() => StationIndex = -1;

        /// <summary>Le temps passe (s réelles) : les grilles des stations avancent, qu'on les écoute ou non.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f) return;
            _broadcastSeconds += dt;
            if (_flashLeft > 0f) _flashLeft = Math.Max(0f, _flashLeft - dt);
        }

        private float _flashLeft;
        private readonly System.Collections.Generic.Dictionary<string, int> _flashTurn = new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>Flash d'information en cours (vide sinon).</summary>
        public string Flash { get; private set; } = "";
        public bool FlashActive => _flashLeft > 0f && Flash.Length > 0;

        /// <summary>
        /// Un événement du jeu (embouteillage, contrôle, pluie, marché) : la radio lance un flash d'information (les flashs
        /// de cet événement tournent). Rend le texte du flash, vide s'il n'y en a pas dans les données.
        /// </summary>
        public string PushEvent(string evt, string place = "")
        {
            if (!_config.enabled || _config.flashes == null) return "";
            var list = new System.Collections.Generic.List<RadioFlashData>();
            foreach (var f in _config.flashes) if (f.@event == evt && !string.IsNullOrEmpty(f.text)) list.Add(f);
            if (list.Count == 0) return "";
            _flashTurn.TryGetValue(evt, out var turn);
            _flashTurn[evt] = turn + 1;
            var text = list[turn % list.Count].text.Replace("{lieu}", string.IsNullOrEmpty(place) ? "sur la route" : place);
            Flash = text;
            _flashLeft = Math.Max(1f, _config.flashSeconds);
            return text;
        }

        /// <summary>
        /// Texte à l'écran (sous-titre) : le flash en cours, sinon le texte du programme (animateur, publicité) ; vide si la
        /// radio est coupée, hors du taxi, ou sur une musique.
        /// </summary>
        public string ScreenText
        {
            get
            {
                if (!Audible) return "";
                if (FlashActive) return "Flash info : " + Flash;
                return CurrentProgramme?.text ?? "";
            }
        }

        /// <summary>Le programme compte dans la grille (avec skipSilentProgrammes, seulement s'il a un son ou un texte).</summary>
        public bool Playable(RadioProgramData p) =>
            p != null && (!_config.skipSilentProgrammes || !string.IsNullOrEmpty(p.clip) || !string.IsNullOrEmpty(p.text));

        /// <summary>Durée d'un programme (s), avec la valeur par défaut des données.</summary>
        public float DurationOf(RadioProgramData p) =>
            p != null && p.seconds > 0f ? p.seconds : Math.Max(1f, _config.defaultProgramSeconds);

        /// <summary>Programme en cours sur la station écoutée (null si coupée ou station vide).</summary>
        public RadioProgramData CurrentProgramme => Locate(out _);

        /// <summary>Secondes déjà jouées du programme en cours (pour reprendre au bon endroit : la radio est en direct).</summary>
        public float ProgrammeOffset
        {
            get
            {
                Locate(out var offset);
                return offset;
            }
        }

        private RadioProgramData Locate(out float offset)
        {
            offset = 0f;
            var station = Station;
            if (station?.programmes == null || station.programmes.Length == 0) return null;
            var cycle = 0f;
            RadioProgramData last = null;
            foreach (var p in station.programmes) if (Playable(p)) { cycle += DurationOf(p); last = p; }
            if (last == null) return null;
            var t = _broadcastSeconds % cycle;
            foreach (var p in station.programmes)
            {
                if (!Playable(p)) continue;
                var d = DurationOf(p);
                if (t < d) { offset = t; return p; }
                t -= d;
            }
            return last;
        }

        private object _labelFor;
        private string _label;

        /// <summary>Texte du bouton : « Radio coupée », « Station 1 · FM ? » ou « Radio Kmer · 94,5 FM ».</summary>
        public string ButtonLabel
        {
            get
            {
                var s = Station;
                if (s == null) return "Radio coupée";
                if (ReferenceEquals(s, _labelFor)) return _label; // lu à chaque image : calculé une fois par station
                var freq = s.frequencyMHz > 0f ? $"{s.frequencyMHz.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} FM" : "FM ?";
                _labelFor = s;
                return _label = $"{s.name} · {freq}";
            }
        }

        /// <summary>Ce qui passe : « Piste 1 (à fournir par Kevin) » ; vide si coupée.</summary>
        public string NowPlaying => CurrentProgramme?.title ?? "";
    }

    /// <summary>
    /// La rédaction de la radio : transforme ce qui se passe dans le jeu en flashs d'information (décision de Kevin du
    /// 21/11) : début de la pluie, contrôle de police volant qui s'installe, heures de pointe (embouteillages), jour de
    /// marché. Chaque événement n'est annoncé qu'une fois. Logique pure, testée.
    /// </summary>
    public sealed class RadioNewsDesk
    {
        private readonly System.Collections.Generic.HashSet<string> _announced = new System.Collections.Generic.HashSet<string>();
        private bool _wasRaining;

        /// <summary>Heures de pointe (début inclus, fin exclue, en heures de jeu) : valeurs par défaut de Claude.</summary>
        public int[] RushHours = { 7, 9, 17, 19 };

        public struct NewsItem
        {
            public string Event;
            public string Place;
        }

        /// <param name="minute">Heure de jeu (minutes depuis minuit).</param>
        /// <param name="controls">Contrôles volants en place : (identifiant, endroit).</param>
        public System.Collections.Generic.List<NewsItem> Update(int minute, bool raining, bool marketDay, string marketPlace,
            System.Collections.Generic.IEnumerable<(string id, string place)> controls, string jamPlace)
        {
            var news = new System.Collections.Generic.List<NewsItem>();
            if (raining && !_wasRaining) news.Add(new NewsItem { Event = RadioEvents.Rain });
            _wasRaining = raining;
            if (marketDay && _announced.Add("marche")) news.Add(new NewsItem { Event = RadioEvents.Market, Place = marketPlace ?? "" });
            if (controls != null)
                foreach (var c in controls)
                    if (_announced.Add("controle:" + c.id)) news.Add(new NewsItem { Event = RadioEvents.Control, Place = c.place ?? "" });
            var hour = minute / 60;
            for (var i = 0; i + 1 < RushHours.Length; i += 2)
                if (hour >= RushHours[i] && hour < RushHours[i + 1] && _announced.Add("pointe:" + i))
                    news.Add(new NewsItem { Event = RadioEvents.TrafficJam, Place = jamPlace ?? "" });
            return news;
        }
    }
}
