using System;

namespace TaxiKmer.Core.Road
{
    /// <summary>
    /// Ce qui se voit des dégâts du taxi (idée n° 17) : le joueur comprend sans lire le HUD qu'il doit passer au garage.
    /// Seuils dans road.json (bumperHangDamage, brokenHeadlightDamage, smokeDamage) ; dégâts de 0 (neuf) à 100.
    /// </summary>
    public struct DamageLook
    {
        /// <summary>Le pare-chocs avant pend (degrés, 0 : en place ; jusqu'à <see cref="MaxBumperDegrees"/> à 100).</summary>
        public float BumperHangDegrees;
        public bool HeadlightBroken;
        /// <summary>Fumée sous le capot (0 : aucune, 1 : épaisse).</summary>
        public float Smoke;

        public const float MaxBumperDegrees = 25f;

        public bool Any => BumperHangDegrees > 0f || HeadlightBroken || Smoke > 0f;

        public static DamageLook From(RoadConfig config, float damage)
        {
            config = config ?? new RoadConfig();
            var look = new DamageLook();
            if (damage >= config.bumperHangDamage)
                look.BumperHangDegrees = 8f + (MaxBumperDegrees - 8f) * Ramp(damage, config.bumperHangDamage, 100f);
            look.HeadlightBroken = damage >= config.brokenHeadlightDamage;
            if (damage >= config.smokeDamage) look.Smoke = 0.3f + 0.7f * Ramp(damage, config.smokeDamage, 100f);
            return look;
        }

        private static float Ramp(float v, float from, float to) => to <= from ? 1f : Math.Max(0f, Math.Min(1f, (v - from) / (to - from)));
    }
}
