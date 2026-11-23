using System.Collections.Generic;
using TaxiKmer.Core.Road;
using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Pluie visible (idée n° 12) : gouttes autour de la caméra (coupées au niveau Bas), chaussée qui brille quand elle
    /// est mouillée, essuie-glaces sur le pare-brise du taxi, bruit de pluie sur la tôle. Règles dans
    /// <see cref="RainLook"/> (rain.json) ; l'averse vient de la météo du jour (road.json). Non exécuté dans Unity.
    /// </summary>
    public sealed class RainView
    {
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public RainLook Look { get; }
        private readonly ParticleSystem _drops;
        private readonly List<Material> _roads = new List<Material>();
        private readonly Transform[] _wipers = new Transform[2];
        private readonly AudioSource _roof;

        public RainView(RainLookConfig config, Transform world, GameObject taxi)
        {
            Look = new RainLook(config);
            var go = new GameObject("Gouttes de pluie");
            _drops = go.AddComponent<ParticleSystem>();
            var main = _drops.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 18f;
            main.startSize = 0.04f;
            main.startColor = new Color(0.8f, 0.85f, 0.95f, 0.5f);
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = _drops.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 1f, 30f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // les gouttes tombent
            var emission = _drops.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Chaussées : une copie des matériaux (ne pas modifier les fichiers du projet en jouant dans l'éditeur).
            if (world != null)
                foreach (var name in new[] { "Routes", "Grands axes" })
                {
                    var group = world.Find(name);
                    if (group == null) continue;
                    var renderers = group.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length == 0) continue;
                    var copy = new Material(renderers[0].sharedMaterial) { name = renderers[0].sharedMaterial.name + " (pluie)" };
                    foreach (var r in renderers) r.sharedMaterial = copy;
                    _roads.Add(copy);
                }

            // Essuie-glaces : deux balais noirs au bas du pare-brise (taxi provisoire de TaxiFactory).
            for (var i = 0; i < 2; i++)
            {
                var pivot = new GameObject("Essuie-glace").transform;
                pivot.SetParent(taxi.transform, false);
                pivot.localPosition = new Vector3(i == 0 ? -0.45f : 0.2f, 1.04f, 0.87f);
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(blade.GetComponent<Collider>());
                blade.name = "Balai";
                blade.transform.SetParent(pivot, false);
                blade.transform.localPosition = new Vector3(0.28f, 0f, 0f);
                blade.transform.localScale = new Vector3(0.56f, 0.025f, 0.02f);
                blade.GetComponent<MeshRenderer>().material.color = new Color(0.05f, 0.05f, 0.05f);
                _wipers[i] = pivot;
            }

            _roof = taxi.AddComponent<AudioSource>();
            var data = SoundSynth.RainOnRoof();
            var clip = AudioClip.Create("Pluie sur la tôle provisoire", data.Length, 1, SoundSynth.Rate, false);
            clip.SetData(data, 0);
            _roof.clip = clip;
            _roof.loop = true;
            _roof.playOnAwake = false;
            _roof.spatialBlend = 0f;
            _roof.volume = 0f;
        }

        public void Update(float dt, bool raining, int qualityLevel, bool audible)
        {
            Look.Update(dt, raining);
            var cam = Camera.main;
            if (cam != null) _drops.transform.position = cam.transform.position + Vector3.up * 12f + cam.transform.forward * 8f;
            var emission = _drops.emission;
            emission.rateOverTime = Look.DropsPerSecond(qualityLevel);
            if (Look.DropsPerSecond(qualityLevel) > 0 && !_drops.isPlaying) _drops.Play();
            foreach (var m in _roads) m.SetFloat(Smoothness, Look.RoadSmoothness);
            var angle = Look.WiperPosition * 95f;
            if (_wipers[0] != null) _wipers[0].localRotation = Quaternion.Euler(0f, 0f, angle);
            if (_wipers[1] != null) _wipers[1].localRotation = Quaternion.Euler(0f, 0f, angle);
            _roof.volume = audible ? Look.RoofVolume * UiKit.EffectsVolume : 0f;
            if (_roof.volume > 0f && !_roof.isPlaying) _roof.Play();
            else if (_roof.volume <= 0f && _roof.isPlaying) _roof.Stop();
        }
    }
}
