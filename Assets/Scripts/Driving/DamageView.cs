using TaxiKmer.Core.Road;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Dégâts visibles sur le taxi (idée n° 17) : pare-chocs avant qui pend, phare gauche éteint et noirci, fumée sous
    /// le capot. Règles dans <see cref="DamageLook"/> ; s'appuie sur les blocs nommés du taxi provisoire
    /// (<see cref="TaxiFactory"/> : « Pare-chocs avant », « Phare »). Pièces détachables à faire sur le Mac avec le vrai
    /// modèle. Non exécuté dans Unity.
    /// </summary>
    public sealed class DamageView
    {
        private readonly RoadConfig _config;
        private readonly Transform _bumper;
        private readonly Quaternion _bumperRest;
        private readonly Vector3 _bumperPos;
        private readonly Renderer _leftLight;
        private readonly ParticleSystem _smoke;
        private float _shownDamage = -1f;

        public DamageView(RoadConfig config, GameObject taxi)
        {
            _config = config;
            foreach (Transform child in taxi.transform)
            {
                if (child.name == "Pare-chocs avant") _bumper = child;
                if (child.name == "Phare" && child.localPosition.x < 0f) _leftLight = child.GetComponent<Renderer>();
            }
            if (_bumper != null) { _bumperRest = _bumper.localRotation; _bumperPos = _bumper.localPosition; }
            var go = new GameObject("Fumée du capot");
            go.transform.SetParent(taxi.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.05f, TaxiFactory.Length / 2f - 0.6f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // vers le haut
            _smoke = go.AddComponent<ParticleSystem>();
            var main = _smoke.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 1.2f;
            main.startSize = 0.6f;
            main.startColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = _smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.3f;
            var emission = _smoke.emission;
            emission.rateOverTime = 0f;
            go.GetComponent<ParticleSystemRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Update(float damage)
        {
            if (Mathf.Abs(damage - _shownDamage) < 0.5f) return; // rien de nouveau
            _shownDamage = damage;
            var look = DamageLook.From(_config, damage);
            if (_bumper != null)
            {
                // Pend côté gauche : tourne autour de son attache droite et descend un peu.
                _bumper.localRotation = _bumperRest * Quaternion.Euler(0f, 0f, look.BumperHangDegrees);
                _bumper.localPosition = _bumperPos + Vector3.down * (look.BumperHangDegrees / DamageLook.MaxBumperDegrees * 0.15f);
            }
            if (_leftLight != null)
            {
                var m = _leftLight.material; // instance propre au taxi
                m.color = look.HeadlightBroken ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
                if (look.HeadlightBroken) m.SetColor("_EmissionColor", Color.black);
            }
            var emission = _smoke.emission;
            emission.rateOverTime = look.Smoke * 12f;
            if (look.Smoke > 0f && !_smoke.isPlaying) _smoke.Play();
            if (look.Smoke <= 0f && _smoke.isPlaying) _smoke.Stop();
        }
    }
}
