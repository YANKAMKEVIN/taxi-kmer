using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Cycle jour / nuit (idée n° 3) : tourne et teinte la lumière principale, l'ambiance et le brouillard selon l'heure
    /// du jeu, allume les têtes de lampadaires et les phares du taxi à la tombée de la nuit. Toute la règle est dans
    /// <see cref="DayLight"/> (daylight.json). Les phares ne projettent une vraie lumière qu'au niveau Haut (coût sur
    /// téléphone). Créé par <see cref="StreetView"/> ; non exécuté dans Unity (session cloud).
    /// </summary>
    public sealed class DayLightView
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private readonly DayLight _light;
        private readonly Light _sun;
        private readonly Renderer[] _lampHeads;
        private readonly Renderer[] _headlights;
        private readonly Light _beam;
        private readonly Material _lampOn, _lampOff;
        private bool? _lampsShown;

        public DayLightView(DayLightConfig config, Transform lampHeads, GameObject taxi)
        {
            _light = new DayLight(config);
            _sun = RenderSettings.sun;
            if (_sun == null)
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _sun = l; break; }
            _lampHeads = lampHeads != null ? lampHeads.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (_lampHeads.Length > 0)
            {
                _lampOff = _lampHeads[0].sharedMaterial;
                _lampOn = new Material(_lampOff) { name = "Lampadaire allumé" };
                _lampOn.EnableKeyword("_EMISSION");
                _lampOn.SetColor(EmissionColor, new Color(1f, 0.82f, 0.5f) * 2.5f);
            }
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in taxi.GetComponentsInChildren<Renderer>())
                if (r.name == "Phare") list.Add(r);
            _headlights = list.ToArray();
            foreach (var r in _headlights) r.material.EnableKeyword("_EMISSION"); // instance propre au taxi
            _beam = new GameObject("Faisceau des phares").AddComponent<Light>();
            _beam.transform.SetParent(taxi.transform, false);
            _beam.transform.localPosition = new Vector3(0f, 0.8f, TaxiFactory.Length / 2f + 0.1f);
            _beam.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            _beam.type = LightType.Spot;
            _beam.range = 40f;
            _beam.spotAngle = 60f;
            _beam.intensity = 2f;
            _beam.shadows = LightShadows.None;
            _beam.enabled = false;
        }

        /// <param name="minutes">Heure du jeu en minutes depuis minuit.</param>
        /// <param name="highQuality">Niveau Haut : les phares éclairent vraiment la route.</param>
        public void Update(float minutes, bool highQuality)
        {
            if (!_light.Enabled) return;
            var s = _light.At(minutes);
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(s.ElevationDegrees, s.HeadingDegrees, 0f);
                _sun.intensity = s.Intensity;
                _sun.color = ToColor(s.Sun);
            }
            var ambient = ToColor(s.Ambient);
            RenderSettings.ambientSkyColor = ambient;
            RenderSettings.ambientEquatorColor = ambient * 0.8f;
            RenderSettings.ambientGroundColor = ambient * 0.45f;
            RenderSettings.fogColor = ToColor(s.Fog);
            var cam = Camera.main;
            if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = RenderSettings.fogColor;

            if (_lampsShown != s.LampsOn && _lampOn != null)
                foreach (var r in _lampHeads) r.sharedMaterial = s.LampsOn ? _lampOn : _lampOff;
            if (_lampsShown != s.LampsOn)
                foreach (var r in _headlights)
                    r.material.SetColor(EmissionColor, s.LampsOn ? new Color(1f, 0.95f, 0.8f) * 2f : Color.black);
            _lampsShown = s.LampsOn;
            _beam.enabled = s.LampsOn && highQuality;
        }

        private static Color ToColor(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.white;
    }
}
