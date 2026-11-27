using System.Collections.Generic;
using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Entrées jouées par un programme (pilote automatique des tests de parcours) : elles passent par la même porte que
    /// le clavier (<see cref="UiKit.KeyDown"/>, <see cref="UiKit.KeyHeld"/>), donc tout le jeu les lit comme un joueur.
    /// Vide en jeu normal.
    /// </summary>
    public static class BotInput
    {
        private static readonly HashSet<KeyAction> HeldKeys = new HashSet<KeyAction>();
        private static readonly Dictionary<KeyAction, int> PressedAt = new Dictionary<KeyAction, int>();

        /// <summary>Tient (ou lâche) une touche jusqu'au prochain changement.</summary>
        public static void Hold(KeyAction action, bool held)
        {
            if (held) HeldKeys.Add(action);
            else HeldKeys.Remove(action);
        }

        /// <summary>Appuie une fois : la touche est « enfoncée » pendant toute l'image suivante.</summary>
        public static void Press(KeyAction action) => PressedAt[action] = Time.frameCount + 1;

        /// <summary>Lâche tout (fin de test).</summary>
        public static void Clear()
        {
            HeldKeys.Clear();
            PressedAt.Clear();
        }

        internal static bool Held(KeyAction action) => HeldKeys.Contains(action) || Down(action);

        internal static bool Down(KeyAction action) => PressedAt.TryGetValue(action, out var f) && f == Time.frameCount;
    }
}
