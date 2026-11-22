namespace TaxiKmer.Core.Police
{
    /// <summary>
    /// Postes de contrôle dans le monde (jalon 4, tâche 4.5) : quand dessiner un poste et ce qu'on lit au-dessus. Un
    /// poste fixe est toujours là ; un contrôle volant n'apparaît que pendant qu'il est en place. Le policier s'avance sur
    /// la chaussée pendant qu'il arrête le taxi. Le dessin (primitives provisoires) est dans StreetView.
    /// </summary>
    public static class PoliceMarkers
    {
        /// <summary>Le poste se voit dans le monde à cette minute de jeu (fixe : toujours ; volant : pendant qu'il est en place).</summary>
        public static bool Visible(ControlPost post, int minute) => post != null && (!post.Flying || post.ActiveAt(minute));

        /// <summary>Ce poste est celui qui arrête le taxi en ce moment (le policier s'avance sur la chaussée).</summary>
        public static bool Holding(ControlPost post, ControlPost current) => post != null && current != null && ReferenceEquals(post, current);

        /// <summary>
        /// Texte au-dessus du poste : « Contrôle » (fixe) ou « Contrôle volant ». Le nom complet du poste reste dans le
        /// panneau du contrôle ; au-dessus de la route, un mot suffit à le repérer de loin.
        /// </summary>
        public static string Label(ControlPost post) => post != null && post.Flying ? "Contrôle volant" : "Contrôle";

        /// <summary>
        /// Le poste est assez proche pour qu'on lise son texte (le long de l'itinéraire, devant ou derrière le taxi) :
        /// <paramref name="meters"/> mètres au plus.
        /// </summary>
        public static bool LabelShown(ControlPost post, float taxiAlong, float meters = 250f)
            => post != null && System.Math.Abs(post.Along - taxiAlong) <= meters;
    }
}
