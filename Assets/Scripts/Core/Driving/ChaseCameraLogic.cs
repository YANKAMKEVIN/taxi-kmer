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
        /// <summary>Champ de vision vertical (degrés), élargi avec la vitesse (idée n° 16).</summary>
        public float FovDeg { get; private set; }
        /// <summary>Force de la secousse en cours (m), amortie jusqu'à 0.</summary>
        public float ShakeAmplitude { get; private set; }
        private float _shakeTime;

        public ChaseCameraLogic(ChaseCameraConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            PitchDeg = config.defaultPitchDeg;
            FovDeg = config.fovDeg;
        }

        /// <summary>Champ de vision visé à cette vitesse (sans le lissage).</summary>
        public float TargetFov(float speedKmh)
        {
            var t = _config.fovFullKmh > 0f ? Clamp(Math.Abs(speedKmh) / _config.fovFullKmh, 0f, 1f) : 0f;
            return _config.fovDeg + (_config.fovFastDeg - _config.fovDeg) * t;
        }

        /// <summary>Lisse le champ de vision vers celui de la vitesse et amortit la secousse.</summary>
        public void UpdateMotion(float dt, float speedKmh)
        {
            if (dt <= 0f) return;
            var k = 1f - (float)Math.Exp(-Math.Max(0f, _config.fovSharpness) * dt);
            FovDeg += (TargetFov(speedKmh) - FovDeg) * k;
            _shakeTime += dt;
            var decay = _config.shakeSeconds > 0f ? dt / _config.shakeSeconds : 1f;
            ShakeAmplitude = Math.Max(0f, ShakeAmplitude - Math.Max(ShakeAmplitude, 0.05f) * decay * 3f);
        }

        /// <summary>Secoue la caméra (la plus forte secousse l'emporte).</summary>
        public void Shake(float amplitude) => ShakeAmplitude = Math.Max(ShakeAmplitude, Math.Max(0f, amplitude));
        public void PotholeShake() => Shake(_config.potholeShake);
        public void CrashShake() => Shake(_config.crashShake);

        /// <summary>Décalage de la secousse (m, droite et haut), qui oscille vite et s'éteint avec l'amplitude.</summary>
        public void ShakeOffset(out float right, out float up)
        {
            right = ShakeAmplitude * (float)Math.Sin(_shakeTime * 71.0);
            up = ShakeAmplitude * (float)Math.Sin(_shakeTime * 53.0 + 1.3);
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
