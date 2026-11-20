using TaxiKmer.Core.Day;
using TaxiKmer.Core.Driving;

namespace TaxiKmer.Core.Ux
{
    public enum TutorialStep
    {
        Accelerate,
        Honk,
        Board,
        Drop,
        Versement,
        Done,
    }

    /// <summary>Ce que le tutoriel observe de la partie à chaque image.</summary>
    public struct TutorialObservation
    {
        public float SpeedKmh;
        /// <summary>Un client a été accepté d'un coup de klaxon et attend que le taxi s'arrête.</summary>
        public bool HasAcceptedClient;
        public int Passengers;
        /// <summary>Courses payées depuis le début de la journée.</summary>
        public int RidesPaid;
        /// <summary>Le bouton contextuel est affiché (« Faire monter », « Faire descendre »).</summary>
        public bool ActionVisible;
    }

    /// <summary>
    /// Tutoriel de la première journée : quatre consignes qui s'enchaînent quand le joueur réussit le geste
    /// (accélérer, klaxonner un client, s'arrêter et le faire monter, le déposer à son repère), puis une phrase sur le
    /// versement. L'étape se déduit de l'état de la partie, donc le joueur peut aller plus vite que les consignes, et
    /// revenir en arrière si le client accepté est dépassé. La présentation met en avant <see cref="Highlight"/>.
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>Vitesse à atteindre pour valider « accélère » (km/h).</summary>
        public const float StartSpeedKmh = 10f;
        /// <summary>Durée d'affichage de la phrase sur le versement (secondes réelles).</summary>
        public const float VersementSeconds = 9f;

        private readonly int _versement;
        private bool _started;
        private float _versementShown;

        public TutorialStep Step { get; private set; }
        public bool Active => Step != TutorialStep.Done;
        /// <summary>Commande à mettre en avant à l'écran (None : aucune).</summary>
        public DriveControl Highlight { get; private set; }
        public string Instruction { get; private set; } = "";

        /// <param name="active">false : tutoriel déjà fait (il ne montre rien).</param>
        /// <param name="versement">Versement du jour (FCFA), cité dans la dernière phrase.</param>
        public Tutorial(bool active, int versement)
        {
            _versement = versement;
            Step = active ? TutorialStep.Accelerate : TutorialStep.Done;
            Refresh(false);
        }

        public void Update(float dt, TutorialObservation o)
        {
            if (!Active) return;
            if (o.SpeedKmh >= StartSpeedKmh || o.HasAcceptedClient || o.Passengers > 0) _started = true;

            if (o.RidesPaid > 0)
            {
                if (Step != TutorialStep.Versement) _versementShown = 0f;
                Step = TutorialStep.Versement;
                _versementShown += dt;
                if (_versementShown >= VersementSeconds) Step = TutorialStep.Done;
            }
            else if (o.Passengers > 0) Step = TutorialStep.Drop;
            else if (o.HasAcceptedClient) Step = TutorialStep.Board;
            else Step = _started ? TutorialStep.Honk : TutorialStep.Accelerate;
            Refresh(o.ActionVisible);
        }

        private void Refresh(bool actionVisible)
        {
            switch (Step)
            {
                case TutorialStep.Accelerate:
                    Highlight = DriveControl.Accelerate;
                    Instruction = "Appuie sur « Accélérer » pour partir";
                    break;
                case TutorialStep.Honk:
                    Highlight = DriveControl.Horn;
                    Instruction = "Un client annonce sa destination et son prix : klaxonne pour l'accepter";
                    break;
                case TutorialStep.Board:
                    Highlight = actionVisible ? DriveControl.Action : DriveControl.Brake;
                    Instruction = actionVisible ? "Appuie sur « Faire monter »" : "Freine et arrête-toi près de lui";
                    break;
                case TutorialStep.Drop:
                    Highlight = actionVisible ? DriveControl.Action : DriveControl.None;
                    Instruction = actionVisible
                        ? "Appuie sur « Faire descendre » pour encaisser"
                        : "Conduis-le jusqu'au repère vert de sa destination et arrête-toi";
                    break;
                case TutorialStep.Versement:
                    Highlight = DriveControl.None;
                    Instruction = $"Bravo ! Chaque soir, tu verses {DaySummary.Money(_versement)} au propriétaire du taxi " +
                                  "et tu paies ton carburant : le reste est pour toi";
                    break;
                default:
                    Highlight = DriveControl.None;
                    Instruction = "";
                    break;
            }
        }
    }
}
