using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Caméra libre du mode photo (idée n° 27) : on tourne en glissant, on avance, recule, monte et descend ; elle
    /// reste dans une bulle autour du taxi (pas de survol de toute la ville) et au-dessus du sol. Pure, testable.
    /// </summary>
    public sealed class PhotoCamera
    {
        public const float MaxDistance = 80f;
        public const float MinHeight = 0.5f;
        public const float MaxHeight = 60f;
        public const float SpeedMs = 8f;
        public const float DegreesPerScreen = 180f;

        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public float YawDeg { get; private set; }
        public float PitchDeg { get; private set; }
        private float _cx, _cz;

        /// <summary>Part de là où était la caméra de conduite ; la bulle est centrée sur le taxi.</summary>
        public PhotoCamera(float x, float y, float z, float yawDeg, float pitchDeg, float taxiX, float taxiZ)
        {
            X = x; Y = y; Z = z; YawDeg = yawDeg; PitchDeg = pitchDeg;
            _cx = taxiX; _cz = taxiZ;
            Clamp();
        }

        /// <summary>Glissement du doigt (fractions de la largeur d'écran) : tourne.</summary>
        public void Look(float dxScreens, float dyScreens)
        {
            YawDeg = (YawDeg + dxScreens * DegreesPerScreen) % 360f;
            PitchDeg = Math.Max(-80f, Math.Min(80f, PitchDeg - dyScreens * DegreesPerScreen));
        }

        /// <param name="forward">-1 à 1 : recule / avance dans la direction du regard (à plat).</param>
        /// <param name="right">-1 à 1 : glisse à gauche / à droite.</param>
        /// <param name="up">-1 à 1 : descend / monte.</param>
        public void Move(float forward, float right, float up, float dt)
        {
            var yaw = YawDeg * Math.PI / 180.0;
            float fx = (float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);
            X += (fx * forward + fz * right) * SpeedMs * dt;
            Z += (fz * forward - fx * right) * SpeedMs * dt;
            Y += up * SpeedMs * dt;
            Clamp();
        }

        private void Clamp()
        {
            Y = Math.Max(MinHeight, Math.Min(MaxHeight, Y));
            float dx = X - _cx, dz = Z - _cz;
            var d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= MaxDistance) return;
            X = _cx + dx / d * MaxDistance;
            Z = _cz + dz / d * MaxDistance;
        }

        /// <summary>Nom du fichier d'une photo (« taxi-kmer-jour-12-0715.png »).</summary>
        public static string FileName(int day, string clock, int index) =>
            $"taxi-kmer-jour-{day}-{(clock ?? "").Replace(":", "")}{(index > 0 ? "-" + index : "")}.png";
    }
}
