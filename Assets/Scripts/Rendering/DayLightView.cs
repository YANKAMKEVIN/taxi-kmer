using System;
using TaxiKmer.Core.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TaxiKmer.Rendering
{
    /// <summary>
    /// Lumière et étalonnage de Yaoundé selon l'heure du jeu (direction artistique « réaliste stylisé poussé ») : soleil
    /// (direction, couleur, intensité, puis lune la nuit), ciel procédural, lumière ambiante, couleur du brouillard, et un
    /// volume de post-traitement global (tonalité, contraste, saturation, balance des blancs, vignette ; halo lumineux en
    /// qualité Haute seulement). Toute la règle est dans <see cref="DayLight"/> et daylight.json. Rien en qualité Basse
    /// sauf la lumière elle-même (le post-traitement est coupé).
    /// </summary>
    public sealed class DayLightView : MonoBehaviour
    {
        public const string Resource = "Data/daylight";

        /// <summary>Minute du jeu depuis minuit ; par défaut l'heure posée par la scène de conduite (<see cref="SceneClock"/>).</summary>
        public Func<float> MinuteSource = () => SceneClock.Minute;
        /// <summary>Le soleil de la scène (trouvé par son nom « Soleil » sinon).</summary>
        public Light sun;

        /// <summary>Dernier état appliqué (pour les tests et les captures).</summary>
        public DayLightState State { get; private set; }

        private DayLightConfig _config;
        private Volume _volume;
        private ColorAdjustments _color;
        private Bloom _bloom;
        private Material _sky;
        private string _qualityName = "";

        /// <summary>Chaque scène qui a un « Soleil » reçoit la lumière du jour (Monde, et le jour suivant qui la recharge).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            var go = GameObject.Find("Soleil");
            if (go != null && go.GetComponent<DayLightView>() == null) go.AddComponent<DayLightView>().sun = go.GetComponent<Light>();
        }

        public static DayLightConfig LoadConfig()
        {
            var asset = Resources.Load<TextAsset>(Resource);
            return asset != null ? JsonUtility.FromJson<DayLightConfig>(asset.text) : new DayLightConfig();
        }

        private void Awake()
        {
            _config = LoadConfig();
            if (sun == null)
            {
                var go = GameObject.Find("Soleil");
                if (go != null) sun = go.GetComponent<Light>();
            }
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (RenderSettings.skybox != null)
            {
                _sky = new Material(RenderSettings.skybox) { name = "Ciel de Yaoundé" };
                RenderSettings.skybox = _sky;
            }
            BuildVolume();
        }

        private void OnDestroy()
        {
            if (_volume != null && _volume.profile != null) Destroy(_volume.profile);
            if (_sky != null) Destroy(_sky);
        }

        private void BuildVolume()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Étalonnage de Yaoundé";
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            _color = profile.Add<ColorAdjustments>(true);
            _color.contrast.Override(14f);
            _color.saturation.Override(16f);
            _color.postExposure.Override(0f);
            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(6f); // un peu chaud : poussière rouge, soleil tropical
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1.1f);
            _bloom.intensity.Override(0.25f);
            _bloom.scatter.Override(0.6f);

            var go = new GameObject("Étalonnage");
            go.transform.SetParent(transform.parent, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.profile = profile;
        }

        private void LateUpdate()
        {
            var minute = MinuteSource != null ? MinuteSource() : 12 * 60f;
            var s = DayLight.At(_config, minute);
            State = s;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(s.Elevation, 1f), s.Azimuth, 0f);
                sun.color = ToColor(s.SunColor);
                sun.intensity = s.SunIntensity;
                sun.shadowStrength = s.Night ? 0.35f : 0.85f;
            }
            RenderSettings.ambientSkyColor = ToColor(s.Sky);
            RenderSettings.ambientEquatorColor = ToColor(s.Equator);
            RenderSettings.ambientGroundColor = ToColor(s.Ground);
            RenderSettings.fogColor = ToColor(s.Fog);
            if (_sky != null)
            {
                if (_sky.HasProperty("_Exposure")) _sky.SetFloat("_Exposure", Mathf.Lerp(0.04f, 1.25f, s.SkyBrightness));
                if (_sky.HasProperty("_AtmosphereThickness")) _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.6f, 1.05f, s.SkyBrightness));
            }
            _color.postExposure.value = s.Exposure;

            // Post-traitement selon la qualité : rien en Bas, sans halo en Moyen, complet en Haut.
            var quality = QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "";
            if (quality == _qualityName) return;
            _qualityName = quality;
            var low = quality == "Bas";
            _volume.enabled = !low;
            _bloom.active = quality == "Haut";
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = !low;
            }
        }

        private static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B);
    }
}
