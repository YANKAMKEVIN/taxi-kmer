using System;
using System.Collections.Generic;
using System.Text;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Actions au clavier (éditeur, démo web) ; sur téléphone, tout passe par les boutons.</summary>
    public enum KeyAction
    {
        Accelerate,
        Brake,
        Left,
        Right,
        Horn,
        /// <summary>Bouton contextuel (faire monter, descendre, terminer…), aussi « Commencer » et « Continuer » des écrans.</summary>
        Action,
        /// <summary>Pause, et retour dans les menus (aussi le bouton Retour d'Android).</summary>
        Pause,
        Quality,
        /// <summary>Frein à main (idée n° 26).</summary>
        Handbrake,
        ResetCar,
        Radio,
        HaggleAccept,
        HaggleRefuse,
        RefusalLetGo,
        RefusalCallPolice,
        PushTaxi,
        CallTechnician,
        SpareWheel,
        CallMechanic,
        CrashYes,
        CrashNo,
        Reply1,
        Reply2,
        Reply3,
        Police1,
        Police2,
        Police3,
        Police4,
        Police5,
    }

    /// <summary>Une action et ses touches (noms de KeyCode d'Unity), avec le libellé de l'aide.</summary>
    public sealed class KeyBinding
    {
        public KeyAction Action;
        /// <summary>Noms de KeyCode d'Unity (« UpArrow », « Z », « Alpha1 »).</summary>
        public string[] Keys;
        /// <summary>Ce que fait la touche, pour l'aide (« klaxon »).</summary>
        public string Label;
        /// <summary>Touche telle qu'on l'affiche dans l'aide (« ↑ / Z / W »).</summary>
        public string Shown;
    }

    /// <summary>
    /// Table des touches du clavier (décision de Kevin du 23/11 : une touche différente pour chaque action, plus de
    /// doublon N / P / W). Lue par la présentation (UiKit.KeyDown) et affichée dans l'aide de la pause.
    /// </summary>
    public static class KeyMap
    {
        public static readonly KeyBinding[] All =
        {
            B(KeyAction.Accelerate, "accélérer", "↑ / Z / W", "UpArrow", "Z", "W"),
            B(KeyAction.Brake, "freiner, reculer", "↓ / S / Espace", "DownArrow", "S", "Space"),
            B(KeyAction.Left, "gauche", "← / Q / A", "LeftArrow", "Q", "A"),
            B(KeyAction.Right, "droite", "→ / D", "RightArrow", "D"),
            B(KeyAction.Horn, "klaxon", "H", "H"),
            B(KeyAction.Action, "action, valider", "E / Entrée", "E", "Return"),
            B(KeyAction.Pause, "pause, retour", "Échap", "Escape"),
            B(KeyAction.Quality, "qualité", "G", "G"),
            B(KeyAction.Handbrake, "frein à main", "C", "C"),
            B(KeyAction.ResetCar, "remettre sur la route", "R", "R"),
            B(KeyAction.Radio, "radio", "B", "B"),
            B(KeyAction.HaggleAccept, "accepter la contre-offre", "O", "O"),
            B(KeyAction.HaggleRefuse, "refuser la contre-offre", "N", "N"),
            B(KeyAction.RefusalLetGo, "laisser partir le mauvais payeur", "L", "L"),
            B(KeyAction.RefusalCallPolice, "appeler la police", "P", "P"),
            B(KeyAction.PushTaxi, "pousser le taxi", "U", "U"),
            B(KeyAction.CallTechnician, "appeler le technicien", "T", "T"),
            B(KeyAction.SpareWheel, "changer la roue", "J", "J"),
            B(KeyAction.CallMechanic, "appeler le mécanicien", "M", "M"),
            B(KeyAction.CrashYes, "s'arrêter / poursuivre (accrochage)", "K", "K"),
            B(KeyAction.CrashNo, "partir / laisser filer (accrochage)", "X", "X"),
            B(KeyAction.Reply1, "réponse 1", "F1", "F1"),
            B(KeyAction.Reply2, "réponse 2", "F2", "F2"),
            B(KeyAction.Reply3, "réponse 3", "F3", "F3"),
            B(KeyAction.Police1, "choix 1 (contrôle)", "1", "Alpha1"),
            B(KeyAction.Police2, "choix 2 (contrôle)", "2", "Alpha2"),
            B(KeyAction.Police3, "choix 3 (contrôle)", "3", "Alpha3"),
            B(KeyAction.Police4, "choix 4 (contrôle)", "4", "Alpha4"),
            B(KeyAction.Police5, "choix 5 (contrôle)", "5", "Alpha5"),
        };

        private static KeyBinding B(KeyAction a, string label, string shown, params string[] keys) =>
            new KeyBinding { Action = a, Label = label, Shown = shown, Keys = keys };

        /// <summary>Touches d'une action (noms de KeyCode) ; vide si l'action n'a pas de touche.</summary>
        public static string[] KeysFor(KeyAction action)
        {
            foreach (var b in All) if (b.Action == action) return b.Keys;
            return Array.Empty<string>();
        }

        /// <summary>
        /// Aide du clavier pour la pause (une ligne par groupe) : « ↑ / Z / W accélérer · … ». Les choix numérotés sont
        /// regroupés (« F1-F3 réponses aux passagers », « 1-5 choix au contrôle »).
        /// </summary>
        public static string Help()
        {
            var sb = new StringBuilder();
            void Line(params KeyAction[] actions)
            {
                var parts = new List<string>();
                foreach (var a in actions)
                    foreach (var b in All)
                        if (b.Action == a) parts.Add($"{b.Shown} {b.Label}");
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.Join(" · ", parts));
            }
            Line(KeyAction.Accelerate, KeyAction.Brake, KeyAction.Left, KeyAction.Right, KeyAction.Handbrake);
            Line(KeyAction.Action, KeyAction.Horn, KeyAction.Radio, KeyAction.Quality, KeyAction.ResetCar, KeyAction.Pause);
            Line(KeyAction.HaggleAccept, KeyAction.HaggleRefuse, KeyAction.RefusalLetGo, KeyAction.RefusalCallPolice);
            Line(KeyAction.PushTaxi, KeyAction.CallTechnician, KeyAction.SpareWheel, KeyAction.CallMechanic, KeyAction.CrashYes, KeyAction.CrashNo);
            sb.Append("\nF1-F3 réponses aux passagers · 1-5 choix au contrôle de police");
            return sb.ToString();
        }

        /// <summary>Touches utilisées par plusieurs actions (« N : radio, refuser la contre-offre ») ; vide si aucune.</summary>
        public static List<string> Conflicts()
        {
            var byKey = new Dictionary<string, List<string>>();
            foreach (var b in All)
                foreach (var k in b.Keys)
                {
                    if (!byKey.TryGetValue(k, out var list)) byKey[k] = list = new List<string>();
                    list.Add(b.Label);
                }
            var conflicts = new List<string>();
            foreach (var kv in byKey)
                if (kv.Value.Count > 1) conflicts.Add($"{kv.Key} : {string.Join(", ", kv.Value)}");
            return conflicts;
        }
    }
}
