using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>Réglages gardés dans les préférences du téléphone (PlayerPrefs).</summary>
    public sealed class PrefsStore : ISettingsStore
    {
        public static readonly PrefsStore Instance = new PrefsStore();

        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    }
}
