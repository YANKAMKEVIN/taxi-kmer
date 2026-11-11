using TaxiKmer.Core.Driving;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>Lecture des réglages de conduite (Assets/Resources/Data/driving.json), avec repli sur les valeurs par défaut.</summary>
    public static class DriveData
    {
        public const string Resource = "Data/driving";
        private static DriveConfig _cached;

        public static DriveConfig Load()
        {
            if (_cached != null) return _cached;
            var text = Resources.Load<TextAsset>(Resource);
            if (text == null)
            {
                Debug.LogWarning($"[TaxiKmer] Resources/{Resource} introuvable : réglages de conduite par défaut.");
                return _cached = new DriveConfig();
            }
            return _cached = JsonUtility.FromJson<DriveConfig>(text.text);
        }
    }
}
