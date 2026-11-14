using TaxiKmer.Core.Quality;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Réglage de qualité en jeu (tâche 1.7) : applique le niveau choisi (asset URP du même rang via les Quality Settings,
    /// images par seconde visées, distance d'affichage, brouillard), le garde d'une partie à l'autre (PlayerPrefs),
    /// et affiche « Qualité : Moyen · 30 i/s » sur le bouton de qualité des commandes (touche G au clavier, lue par DriverControls).
    /// Les niveaux et leurs valeurs viennent de <see cref="QualityConfig"/> (Resources/Data/quality.json).
    /// </summary>
    public sealed class QualityView : MonoBehaviour
    {
        public const string Resource = "Data/quality";
        public const string PrefKey = "taxikmer.qualite";

        public DriverControls controls;

        private QualityConfig _config;
        private FrameRateMeter _meter;
        private int _level;
        private Camera _camera;

        public int Level => _level;
        public QualityLevel Current => _config.Level(_level);
        public float FramesPerSecond => _meter.FramesPerSecond;

        public static QualityConfig LoadConfig()
        {
            var text = Resources.Load<TextAsset>(Resource);
            if (text == null)
            {
                Debug.LogWarning($"[TaxiKmer] Resources/{Resource} introuvable : qualité par défaut.");
                return new QualityConfig();
            }
            return JsonUtility.FromJson<QualityConfig>(text.text);
        }

        private void Awake()
        {
            _config = LoadConfig();
            _meter = new FrameRateMeter(_config.frameRateWindowSeconds);
            Apply(_config.Resolve(PlayerPrefs.GetInt(PrefKey, -1)), save: false);
        }

        private void Update()
        {
            _meter.Tick(Time.unscaledDeltaTime);
            if (controls != null && controls.QualityTriggered)
                Apply(_config.Next(_level), save: true);

            // La caméra peut naître après nous (rechargement de scène) : on réapplique la distance si elle a changé.
            var cam = Camera.main;
            if (cam != _camera) ApplyCamera();

            if (controls != null) controls.QualityLabel = Label();
        }

        /// <summary>Passe au niveau <paramref name="index"/> (ramené dans les bornes).</summary>
        public void Apply(int index, bool save)
        {
            _level = _config.Resolve(index);
            var level = Current;

            // Les Quality Settings portent l'asset URP (ombres, résolution de rendu, MSAA, HDR) ; on cherche le niveau
            // par son nom pour ne pas dépendre de l'ordre, sinon par son rang.
            var unityIndex = System.Array.IndexOf(QualitySettings.names, level.name);
            if (unityIndex < 0) unityIndex = Mathf.Clamp(_level, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != unityIndex) QualitySettings.SetQualityLevel(unityIndex, true);

            QualitySettings.vSyncCount = 0; // sinon targetFrameRate est ignoré
            Application.targetFrameRate = level.targetFrameRate;
            RenderSettings.fogStartDistance = level.fogStart;
            RenderSettings.fogEndDistance = level.fogEnd;
            ApplyCamera();
            _meter.Reset();

            if (!save) return;
            PlayerPrefs.SetInt(PrefKey, _level);
            PlayerPrefs.Save();
        }

        private void ApplyCamera()
        {
            _camera = Camera.main;
            if (_camera != null) _camera.farClipPlane = Current.drawDistance;
        }

        private string Label()
        {
            var text = $"Qualité : {Current.name}";
            if (!_config.showFrameRate) return text;
            var fps = _meter.FramesPerSecond;
            return fps > 0f ? $"{text} · {Mathf.RoundToInt(fps)} i/s" : text;
        }
    }
}
