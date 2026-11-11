using System;

namespace TaxiKmer.Core.Driving
{
    /// <summary>
    /// Orientation de la caméra derrière le taxi : le joueur la tourne en glissant le doigt (lacet et tangage),
    /// et elle revient seule derrière le taxi après un moment sans glisser.
    /// </summary>
    public sealed class ChaseCameraLogic
    {
        private readonly ChaseCameraConfig _config;
        private float _idle;

        /// <summary>Décalage de lacet par rapport à l'arrière du taxi (degrés, -180 à 180).</summary>
        public float YawOffsetDeg { get; private set; }
        public float PitchDeg { get; private set; }

        public ChaseCameraLogic(ChaseCameraConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            PitchDeg = config.defaultPitchDeg;
        }

        /// <summary>Glissement du doigt, en fractions de la largeur d'écran (dx vers la droite, dy vers le haut).</summary>
        public void Drag(float dxScreens, float dyScreens)
        {
            YawOffsetDeg = Wrap(YawOffsetDeg + dxScreens * _config.dragDegreesPerScreen);
            PitchDeg = Clamp(PitchDeg - dyScreens * _config.dragDegreesPerScreen, _config.minPitchDeg, _config.maxPitchDeg);
            _idle = 0f;
        }

        public void Update(float dt, bool dragging)
        {
            if (dragging) { _idle = 0f; return; }
            _idle += dt;
            if (_idle < _config.recenterDelaySeconds) return;
            var step = _config.recenterRateDeg * dt;
            YawOffsetDeg = Math.Abs(YawOffsetDeg) <= step ? 0f : YawOffsetDeg - Math.Sign(YawOffsetDeg) * step;
            var dp = PitchDeg - _config.defaultPitchDeg;
            PitchDeg = Math.Abs(dp) <= step ? _config.defaultPitchDeg : PitchDeg - Math.Sign(dp) * step;
        }

        private static float Wrap(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg < -180f) deg += 360f;
            return deg;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
