using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Vibrations des moments clés (<see cref="HapticPatterns"/>, Core). Sur Android, appelle le service Vibrator :
    /// <c>VibrationEffect.createWaveform</c> (durées et amplitudes, Android 8 et plus, le minimum du jeu), sinon
    /// <c>vibrate(durée)</c> ; repli sur Handheld.Vibrate, dont la présence ajoute aussi la permission VIBRATE au build.
    /// Sans effet hors téléphone. Désactivable dans les réglages (<see cref="Enabled"/>).
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        private const float MinInterval = 0.06f;
        private static float _last = -10f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static AndroidJavaClass _effectClass;
        private static bool _tried;
        private static bool _effectsUsable;
#endif

        public static void Play(HapticEvent e)
        {
            if (!Enabled) return;
            var now = Time.unscaledTime;
            if (now - _last < MinInterval) return;
            _last = now;
            HapticPatterns.For(e, out var timings, out var amplitudes);
            Vibrate(timings, amplitudes, HapticPatterns.TotalMilliseconds(e));
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool EnsureVibrator()
        {
            if (_tried) return _vibrator != null;
            _tried = true;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                _effectsUsable = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TaxiKmer] Vibreur indisponible : " + e.Message);
                _effectsUsable = false;
            }
            return _vibrator != null;
        }
#endif

        private static void Vibrate(long[] timings, int[] amplitudes, long totalMs)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (EnsureVibrator())
            {
                if (_effectsUsable)
                {
                    try
                    {
                        using (var effect = _effectClass.CallStatic<AndroidJavaObject>("createWaveform", new object[] { timings, amplitudes, -1 }))
                            _vibrator.Call("vibrate", effect);
                        return;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[TaxiKmer] Vibrations graduées abandonnées : " + e.Message);
                        _effectsUsable = false;
                    }
                }
                try
                {
                    _vibrator.Call("vibrate", totalMs);
                    return;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[TaxiKmer] Vibreur indisponible : " + e.Message);
                    _vibrator = null;
                }
            }
            Handheld.Vibrate();
#elif UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
