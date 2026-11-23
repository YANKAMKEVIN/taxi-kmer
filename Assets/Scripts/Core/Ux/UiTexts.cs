using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Textes de l'interface de la rue (idée n° 32, règle 1 du projet ; données : Assets/Resources/Data/ui_texts.json) :
    /// messages et boutons de <c>StreetView</c>, relus par Kevin comme les répliques. {nom} = valeur donnée par le jeu
    /// (voir <see cref="Allowed"/>).
    /// </summary>
    [Serializable]
    public class UiTextsConfig
    {
        public string dayResumed = "Jour {jour} ({semaine}) repris à {heure}";
        public string newCareer = "Nouvelle carrière. ";
        public string dayStart = "{carriere}Jour {jour}, {semaine} : le patron te veut sur la route de {debut} h à {fin} h";
        public string dropOne = "Faire descendre";
        public string dropMany = "Faire descendre ({n})";
        public string board = "Faire monter";
        public string moveAndBoard = "Changer de place et monter";
        public string endDay = "Terminer la journée";
        public string dayOverDropPassengers = "Fin de la journée : dépose tes passagers, ou arrête-toi pour terminer";
        public string honkAccepted = "OK pour {lieu} ({prix}) : arrête-toi";
        public string honkNeedsMove = "OK, mais il faudra faire changer un passager de place";
        public string honkNoRoom = "Pas de place pour ce client ({carrure})";
        public string pedestrianLight = "blessé léger";
        public string pedestrianSerious = "blessé grave";
        public string pedestrianHit = "Piéton renversé ({blessure}) : hôpital et soins -{prix}, {duree} perdues, tes passagers sont partis sans payer";
        public string clientGaveUp = "Ton client en a eu marre d'attendre : il fait signe aux autres taxis";
        public string clientTakenByMoto = "Un bendskin a pris ton client avant que tu arrives !";
        public string rivalMoto = "Un bendskin";
        public string rivalTaxi = "Un autre taxi";
        public string clientTakenNearby = "{qui} a pris le client pour {lieu}";
        public string passengerMoved = "Un passager a changé de place. ";
        public string boarded = "{deplace}Monté : {lieu}, {prix}";
        public string dayIsOver = "La journée est finie";
        public string doesNotFit = "Il ne rentre pas";
        public string towards = "vers {lieu}";
        public string goalDone = "Objectif réussi : {objectif}";
        public string slim = "mince";
        public string big = "costaud";
        public string normalBuild = "carrure normale";

        /// <summary>Valeurs que chaque texte peut citer (les autres n'en citent aucune).</summary>
        public static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            ["dayResumed"] = new[] { "jour", "semaine", "heure" },
            ["dayStart"] = new[] { "carriere", "jour", "semaine", "debut", "fin" },
            ["dropMany"] = new[] { "n" },
            ["honkAccepted"] = new[] { "lieu", "prix" },
            ["honkNoRoom"] = new[] { "carrure" },
            ["pedestrianHit"] = new[] { "blessure", "prix", "duree" },
            ["clientTakenNearby"] = new[] { "qui", "lieu" },
            ["boarded"] = new[] { "deplace", "lieu", "prix" },
            ["towards"] = new[] { "lieu" },
            ["goalDone"] = new[] { "objectif" },
        };
    }

    public static class UiText
    {
        /// <summary>Remplace {nom} par sa valeur (paires nom, valeur à la suite) ; un nom inconnu reste tel quel.</summary>
        public static string Fill(string template, params string[] pairs)
        {
            var text = template ?? "";
            for (var i = 0; i + 1 < pairs.Length; i += 2) text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? "");
            return text;
        }
    }
}
