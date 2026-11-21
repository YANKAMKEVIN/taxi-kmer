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
    }

    /// <summary>Une station de radio : nom, fréquence, grille de pistes ou d'émissions qui tourne en boucle.</summary>
    [Serializable]
    public class RadioStationData
    {
        public string id = "";
        public string name = "";
        /// <summary>Fréquence FM (MHz) ; 0 : à fournir par Kevin (affichée « FM ? »).</summary>
        public float frequencyMHz;
        public RadioProgramData[] programmes = Array.Empty<RadioProgramData>();
    }

    /// <summary>
    /// Radio du taxi (priorité cochée par Kevin le 2026-11-21 ; données : Assets/Resources/Data/radio.json). Mécanisme
    /// seulement : stations et pistes sont des emplacements vides tant que Kevin n'a pas répondu (docs/tech/radio.md).
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
        public RadioStationData[] stations = Array.Empty<RadioStationData>();
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
        /// <summary>Un passager parle (bulle de dialogue) : la radio se tait le temps de la réplique.</summary>
        public bool DialogueActive { get; set; }

        /// <summary>On doit entendre la radio en ce moment.</summary>
        public bool Audible => _config.enabled && On && InTaxi && !DialogueActive;

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
            if (dt > 0f) _broadcastSeconds += dt;
        }

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
            foreach (var p in station.programmes) cycle += DurationOf(p);
            var t = _broadcastSeconds % cycle;
            foreach (var p in station.programmes)
            {
                var d = DurationOf(p);
                if (t < d) { offset = t; return p; }
                t -= d;
            }
            return station.programmes[station.programmes.Length - 1];
        }

        /// <summary>Texte du bouton : « Radio coupée », « Station 1 · FM ? » ou « Radio Kmer · 94,5 FM ».</summary>
        public string ButtonLabel
        {
            get
            {
                var s = Station;
                if (s == null) return "Radio coupée";
                var freq = s.frequencyMHz > 0f ? $"{s.frequencyMHz.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} FM" : "FM ?";
                return $"{s.name} · {freq}";
            }
        }

        /// <summary>Ce qui passe : « Piste 1 (à fournir par Kevin) » ; vide si coupée.</summary>
        public string NowPlaying => CurrentProgramme?.title ?? "";
    }
}
