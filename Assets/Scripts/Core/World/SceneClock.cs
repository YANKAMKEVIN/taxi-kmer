namespace TaxiKmer.Core.World
{
    /// <summary>
    /// Heure du jeu vue par le rendu (lumière du jour) : posée à chaque image par la scène de conduite, lue par
    /// l'éclairage, qui ne dépend ainsi pas de la logique de conduite. Midi par défaut (scènes sans journée).
    /// </summary>
    public static class SceneClock
    {
        public const float Noon = 12 * 60f;

        /// <summary>Minutes depuis minuit, avec la fraction.</summary>
        public static float Minute = Noon;
    }
}
