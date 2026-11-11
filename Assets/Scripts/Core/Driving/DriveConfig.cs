using System;

namespace TaxiKmer.Core.Driving
{
    /// <summary>
    /// Réglages de conduite du taxi (données : Assets/Resources/Data/driving.json).
    /// Valeurs par défaut de la tâche 1.3, à ajuster après essai sur téléphone.
    /// </summary>
    [Serializable]
    public class DriveConfig
    {
        public SteeringConfig steering = new SteeringConfig();
        public PedalConfig pedals = new PedalConfig();
        public CarConfig car = new CarConfig();
        public ChaseCameraConfig camera = new ChaseCameraConfig();
        public ControlLayout layout = ControlLayout.Default();
    }

    /// <summary>Direction : les flèches tournent progressivement le volant, et l'angle diminue avec la vitesse.</summary>
    [Serializable]
    public class SteeringConfig
    {
        /// <summary>Angle des roues avant à l'arrêt et à basse vitesse (degrés).</summary>
        public float maxAngleDeg = 35f;
        /// <summary>Angle maximal à <see cref="highSpeedKmh"/> et au-delà (degrés) : évite les têtes-à-queue.</summary>
        public float highSpeedAngleDeg = 10f;
        public float highSpeedKmh = 70f;
        /// <summary>Vitesse à laquelle le volant tourne quand on appuie sur une flèche (braquage complet / s).</summary>
        public float riseRate = 2.5f;
        /// <summary>Vitesse de retour du volant au centre quand on lâche (braquage complet / s).</summary>
        public float returnRate = 4f;
    }

    /// <summary>Pédales : accélérateur, frein qui devient marche arrière quand on le maintient à l'arrêt.</summary>
    [Serializable]
    public class PedalConfig
    {
        public float maxForwardKmh = 80f;
        public float maxReverseKmh = 15f;
        /// <summary>En dessous de cette vitesse, le taxi est considéré à l'arrêt (km/h).</summary>
        public float stoppedKmh = 2f;
        /// <summary>Durée de frein maintenu à l'arrêt avant de passer la marche arrière (s).</summary>
        public float reverseHoldSeconds = 0.35f;
        /// <summary>Frein léger appliqué quand aucune pédale n'est pressée et que le taxi est à l'arrêt (0 à 1).</summary>
        public float parkingBrake = 0.3f;
        /// <summary>Le moteur arrête de pousser progressivement sur cette fraction de la vitesse maximale.</summary>
        public float limiterBand = 0.1f;
    }

    /// <summary>Physique du véhicule (lue par la présentation Unity, WheelCollider).</summary>
    [Serializable]
    public class CarConfig
    {
        public float massKg = 1150f;
        /// <summary>Couple total du moteur réparti sur les roues avant (traction, comme une Corolla).</summary>
        public float motorTorqueNm = 1100f;
        public float brakeTorqueNm = 1700f;
        /// <summary>Frein moteur quand on lève le pied (couple par roue).</summary>
        public float coastBrakeTorqueNm = 80f;
        /// <summary>Appui aérodynamique fictif, stabilise à vitesse élevée (N par (m/s)²).</summary>
        public float downforce = 4f;
        /// <summary>Raideur de la barre antiroulis (N).</summary>
        public float antiRoll = 6000f;
        /// <summary>Hauteur du centre de gravité par rapport au bas de la caisse (m) : bas = plus stable.</summary>
        public float centerOfMassHeight = 0.25f;
        public float wheelRadius = 0.32f;
        public float suspensionDistance = 0.18f;
        public float springStrength = 35000f;
        public float damper = 4500f;
        /// <summary>Adhérence latérale (raideur de friction WheelCollider) : plus haut = plus collé à la route.</summary>
        public float sidewaysStiffness = 1.6f;
        public float forwardStiffness = 1.8f;
        /// <summary>Après ce temps sur le toit ou sur le flanc, le taxi est remis sur la route (s).</summary>
        public float flippedResetSeconds = 3f;
    }

    /// <summary>Caméra derrière le taxi, que le joueur peut tourner en glissant le doigt.</summary>
    [Serializable]
    public class ChaseCameraConfig
    {
        public float distance = 7f;
        /// <summary>Hauteur du point visé au-dessus du taxi (m) ; la caméra tourne autour de ce point.</summary>
        public float lookHeight = 1.4f;
        public float defaultPitchDeg = 12f;
        public float minPitchDeg = 2f;
        public float maxPitchDeg = 60f;
        /// <summary>Degrés de rotation pour un glissement sur toute la largeur de l'écran.</summary>
        public float dragDegreesPerScreen = 270f;
        /// <summary>Après ce temps sans glisser, la caméra revient derrière le taxi (s).</summary>
        public float recenterDelaySeconds = 2f;
        /// <summary>Vitesse de retour derrière le taxi (degrés / s).</summary>
        public float recenterRateDeg = 120f;
        /// <summary>Raideur du suivi (1/s) : plus haut = caméra plus collée au taxi.</summary>
        public float followSharpness = 6f;
    }
}
