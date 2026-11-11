using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Klaxon du taxi. Son provisoire synthétisé (deux tons, comme un avertisseur de voiture) en attendant un vrai
    /// enregistrement. Sonne tant que le bouton est maintenu, au moins <see cref="minDuration"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class TaxiHorn : MonoBehaviour
    {
        public float minDuration = 0.25f;
        public float volume = 0.8f;

        private AudioSource _source;
        private float _until;

        /// <summary>Nombre de coups de klaxon depuis le lancement (servira à accepter les clients, tâche 1.5).</summary>
        public int Honks { get; private set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = MakeClip();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = volume;
        }

        public void SetPressed(bool pressed)
        {
            if (pressed)
            {
                if (!_source.isPlaying)
                {
                    Honks++;
                    _source.Play();
                }
                _until = Mathf.Max(_until, Time.time + minDuration);
            }
            else if (_source.isPlaying && Time.time >= _until)
            {
                _source.Stop();
            }
        }

        /// <summary>Une seconde bouclable : 400 Hz + 500 Hz, légèrement saturés.</summary>
        private static AudioClip MakeClip()
        {
            const int rate = 44100;
            var data = new float[rate];
            for (var i = 0; i < rate; i++)
            {
                var t = i / (float)rate;
                var s = Mathf.Sin(2f * Mathf.PI * 400f * t) + 0.8f * Mathf.Sin(2f * Mathf.PI * 500f * t);
                data[i] = 0.45f * (float)System.Math.Tanh(2.5f * s);
            }
            var clip = AudioClip.Create("Klaxon provisoire", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
