using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Repères du monde : texte au-dessus d'une destination et pulsation de l'anneau du client accepté.</summary>
    public static class WorldMarkers
    {
        /// <summary>« Emana · 1 passager », « Étoudi · 3 passagers ».</summary>
        public static string DestinationLabel(string stopName, int passengers)
        {
            var who = passengers == 1 ? "1 passager" : $"{passengers} passagers";
            return $"{StopNames.Short(stopName)} · {who}";
        }

        /// <summary>Taille de l'anneau au sol, qui pulse entre 0,85 et 1,15 environ une fois par seconde.</summary>
        public static float RingPulse(float time) => 1f + 0.15f * (float)Math.Sin(time * 2 * Math.PI * 1.1);
    }
}
