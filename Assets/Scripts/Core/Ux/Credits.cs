using System;
using System.Collections.Generic;
using TaxiKmer.Core.Radio;

namespace TaxiKmer.Core.Ux
{
    [Serializable]
    public class CreditData
    {
        /// <summary>Ce qui est crédité (« Routes et bâtiments »).</summary>
        public string what = "";
        /// <summary>Auteurs ou détenteurs (« © contributeurs OpenStreetMap »).</summary>
        public string who = "";
        public string licence = "";
        public string source = "";
    }

    /// <summary>
    /// Écran « Crédits et licences » (idée n° 42 ; données : Assets/Resources/Data/credits.json) : données OSM (ODbL),
    /// relief Copernicus, outils, et chaque morceau CC-BY de la radio, lu depuis radio.json (attribution obligatoire).
    /// </summary>
    [Serializable]
    public class CreditsConfig
    {
        public string title = "Crédits et licences";
        public string intro = "Taxi Kmer, un jeu de Kevin Yankam.";
        public CreditData[] entries =
        {
            new CreditData { what = "Routes et bâtiments de Yaoundé", who = "© contributeurs OpenStreetMap", licence = "ODbL 1.0", source = "openstreetmap.org/copyright" },
            new CreditData { what = "Relief", who = "Copernicus DEM GLO-30 : © DLR e.V. 2010-2014 et © Airbus Defence and Space GmbH 2014-2018, fourni dans le cadre de COPERNICUS par l'Union européenne et l'ESA", licence = "licence Copernicus DEM", source = "spacedata.copernicus.eu" },
            new CreditData { what = "Moteur", who = "Unity 6 (Unity Technologies)", licence = "", source = "" },
        };
        /// <summary>Titre de la partie des musiques de la radio (affiché seulement s'il y en a).</summary>
        public string musicTitle = "Musiques de la radio";
    }

    public static class Credits
    {
        /// <summary>Lignes de l'écran : crédits des données, puis un morceau de radio par ligne (crédit, licence, source).</summary>
        public static List<string> Lines(CreditsConfig config, RadioConfig radio)
        {
            config = config ?? new CreditsConfig();
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(config.intro)) lines.Add(config.intro);
            foreach (var e in config.entries ?? Array.Empty<CreditData>()) lines.Add(Line(e.what, e.who, e.licence, e.source));
            var music = new List<string>();
            var seen = new HashSet<string>();
            foreach (var station in radio?.stations ?? Array.Empty<RadioStationData>())
                foreach (var p in station.programmes ?? Array.Empty<RadioProgramData>())
                {
                    if (string.IsNullOrEmpty(p.clip) || string.IsNullOrEmpty(p.credit)) continue;
                    var line = Line(p.title, p.credit, p.licence, p.source);
                    if (seen.Add(line)) music.Add(line);
                }
            if (music.Count > 0)
            {
                lines.Add(config.musicTitle);
                lines.AddRange(music);
            }
            return lines;
        }

        private static string Line(string what, string who, string licence, string source)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(what)) parts.Add(what + " :");
            if (!string.IsNullOrEmpty(who)) parts.Add(who);
            var tail = new List<string>();
            if (!string.IsNullOrEmpty(licence)) tail.Add(licence);
            if (!string.IsNullOrEmpty(source)) tail.Add(source);
            var text = string.Join(" ", parts);
            return tail.Count > 0 ? $"{text} ({string.Join(", ", tail)})" : text;
        }
    }
}
