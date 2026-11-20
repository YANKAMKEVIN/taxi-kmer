namespace TaxiKmer.Core.Ux
{
    public enum HapticEvent
    {
        Honk,
        Board,
        Payment,
        Refused,
    }

    /// <summary>
    /// Vibrations des moments clés, au format de VibrationEffect.createWaveform d'Android : durées en ms qui alternent
    /// pause et vibration (la première est une pause), et amplitude de chaque segment (0 à 255).
    /// Brève au klaxon et à la montée, double au paiement, plus longue et plus forte quand un client ne rentre pas.
    /// </summary>
    public static class HapticPatterns
    {
        public const int MaxAmplitude = 255;

        public static void For(HapticEvent e, out long[] timings, out int[] amplitudes)
        {
            switch (e)
            {
                case HapticEvent.Honk:
                    timings = new long[] { 0, 25 };
                    amplitudes = new[] { 0, 110 };
                    break;
                case HapticEvent.Board:
                    timings = new long[] { 0, 40 };
                    amplitudes = new[] { 0, 150 };
                    break;
                case HapticEvent.Payment:
                    timings = new long[] { 0, 40, 70, 60 };
                    amplitudes = new[] { 0, 170, 0, 230 };
                    break;
                default:
                    timings = new long[] { 0, 90 };
                    amplitudes = new[] { 0, MaxAmplitude };
                    break;
            }
        }

        /// <summary>Durée totale du motif (ms) : sert au repli « vibrer n ms » quand l'amplitude n'est pas réglable.</summary>
        public static long TotalMilliseconds(HapticEvent e)
        {
            For(e, out var timings, out _);
            long total = 0;
            foreach (var t in timings) total += t;
            return total;
        }
    }
}
