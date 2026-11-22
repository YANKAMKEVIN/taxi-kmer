using System.Collections.Generic;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.Ux;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Ambiance sonore de la rue (jalon 5) : rumeur de la ville et du marché en boucle, klaxons au loin, appel des clients
    /// quand le taxi passe devant eux. Toute la règle est dans <see cref="AmbienceMix"/> ; les sons sont provisoires
    /// (<see cref="SoundSynth"/>). Créé par <see cref="StreetView"/> si ambience.json a enabled = true. Non exécuté dans
    /// Unity (session cloud).
    /// </summary>
    public sealed class AmbienceView
    {
        public AmbienceMix Mix { get; }

        private readonly AudioSource _murmur;
        private readonly AudioSource _market;
        private readonly AudioSource _effects;
        private readonly AudioClip _horn;
        private readonly AudioClip _call;
        private readonly List<KeyValuePair<string, float>> _waiting = new List<KeyValuePair<string, float>>();

        public AmbienceView(GameObject taxi, AmbienceConfig config, float[] marketAlongs, int seed)
        {
            Mix = new AmbienceMix(config, marketAlongs, new System.Random(seed));
            _murmur = Loop(taxi, "Rumeur de la ville provisoire", SoundSynth.StreetMurmur());
            _market = Loop(taxi, "Rumeur du marché provisoire", SoundSynth.MarketChatter());
            _effects = taxi.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.spatialBlend = 0f;
            _horn = Clip("Klaxon lointain provisoire", SoundSynth.DistantHorn());
            _call = Clip("Appel de client provisoire", SoundSynth.ClientCall());
        }

        /// <param name="audible">Faux pendant la pause, le menu ou le bilan : tout se tait.</param>
        public void Update(float dt, Street street, float along, Vector3 taxiPosition, IReadOnlyList<TrafficVehicle> vehicles,
            bool marketDay, bool talking, bool audible)
        {
            Mix.CountNear(vehicles, taxiPosition.x, taxiPosition.z, out var moving, out var jammed);
            _waiting.Clear();
            foreach (var c in street.Waiting)
                if (c.Calling) _waiting.Add(new KeyValuePair<string, float>(c.Client.Id, c.Along));
            var events = Mix.Update(dt, along, moving, jammed, _waiting, marketDay, talking, audible && !street.DayFinished);
            _murmur.volume = Mix.MurmurVolume;
            _market.volume = Mix.MarketVolume;
            if (events.Horn)
            {
                _effects.pitch = Random.Range(0.85f, 1.15f); // pas toujours la même voiture
                _effects.PlayOneShot(_horn, Random.Range(0.4f, 0.8f));
            }
            foreach (var _ in events.Calls)
            {
                _effects.pitch = Random.Range(0.9f, 1.2f);
                _effects.PlayOneShot(_call, 0.7f);
            }
        }

        private static AudioSource Loop(GameObject host, string name, float[] data)
        {
            var source = host.AddComponent<AudioSource>();
            source.clip = Clip(name, data);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SoundSynth.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
