using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Sons provisoires du taxi, synthétisés par <see cref="SoundSynth"/> (Core) comme le klaxon : moteur dont la hauteur
    /// suit la vitesse, crissement au freinage fort, portière à la montée et à la descente, pièces au paiement.
    /// Ajouté au taxi par StreetView ; le volume général se règle dans la pause (AudioListener).
    /// </summary>
    public sealed class TaxiSounds : MonoBehaviour
    {
        public float engineVolume = 0.5f;
        public float effectsVolume = 0.9f;

        private TaxiCar _taxi;
        private AudioSource _engine;
        private AudioSource _effects;
        private AudioClip _squeal, _door, _coins;
        private bool _wasBraking;

        /// <summary>Coupe le moteur (bilan de fin de journée).</summary>
        public bool Muted { get; set; }

        private void Awake()
        {
            _taxi = GetComponent<TaxiCar>();
            _engine = gameObject.AddComponent<AudioSource>();
            _engine.clip = Clip("Moteur provisoire", SoundSynth.EngineLoop());
            _engine.loop = true;
            _engine.playOnAwake = false;
            _engine.spatialBlend = 0f;
            _engine.volume = 0f;
            _engine.Play();

            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.spatialBlend = 0f;
            _squeal = Clip("Crissement provisoire", SoundSynth.BrakeSqueal());
            _door = Clip("Portière provisoire", SoundSynth.DoorSlam());
            _coins = Clip("Pièces provisoires", SoundSynth.Coins());
        }

        private void Update()
        {
            if (_taxi == null) return;
            var cmd = _taxi.Command;
            var max = _taxi.Config != null ? _taxi.Config.pedals.maxForwardKmh : 80f;
            _engine.pitch = SoundSynth.EnginePitch(_taxi.SpeedKmh, max);
            _engine.volume = Muted ? 0f : engineVolume * SoundSynth.EngineVolume(cmd.Throttle) * UiKit.EffectsVolume; // idée n° 31

            var braking = SoundSynth.HardBraking(_taxi.SpeedKmh, cmd.Brake);
            if (braking && !_wasBraking && !Muted) _effects.PlayOneShot(_squeal, effectsVolume * 0.7f * UiKit.EffectsVolume);
            _wasBraking = braking;
        }

        public void PlayDoor() => _effects.PlayOneShot(_door, effectsVolume * UiKit.EffectsVolume);

        public void PlayCoins() => _effects.PlayOneShot(_coins, effectsVolume * UiKit.EffectsVolume);

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SoundSynth.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
