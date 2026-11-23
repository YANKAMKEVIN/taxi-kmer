using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;
using TaxiKmer.World;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>Ajouts de la série d'idées du 23/11 : accessibilité, pause, objectifs, vendeurs, mini-carte, événements de ville. Partie de <see cref="StreetView"/> (idée n° 33 : fichier découpé).</summary>
    public sealed partial class StreetView
    {
        /// <summary>Flashs d'information : pluie, contrôles volants, heures de pointe, jour de marché (radio.json → flashes).</summary>
        /// <summary>Taille du texte et couleurs lisibles (idée n° 29), partagées par les vues.</summary>
        private void ApplyAccessibility()
        {
            UiKit.TextScale = _settings.TextScale;
            UiKit.ColorBlind = _settings.ColorBlind;
            controls.ApplyLayoutOptions(_settings.LeftHanded, _settings.ButtonScale, _settings.ButtonOpacity); // idée n° 30
            UiKit.RadioVolume = _settings.RadioVolume; // idée n° 31
            UiKit.VoiceVolume = _settings.VoiceVolume;
            UiKit.EffectsVolume = _settings.EffectsVolume;
            UiKit.StreetVolume = _settings.StreetVolume;
        }

        /// <summary>Ligne sous le titre de la pause : papiers (jalon 4) et objectifs du jour (idée n° 25).</summary>
        private string PauseNote()
        {
            var papers = _police != null ? _police.Police.Papers.Summary(_police.Police.Day) : "";
            var goals = _goals != null ? _goals.Summary() : "";
            return papers.Length > 0 && goals.Length > 0 ? papers + "\n" + goals : papers + goals;
        }

        /// <summary>Objectifs du jour (idée n° 25) : suivis à chaque image depuis le grand livre de la journée.</summary>
        private void UpdateGoals()
        {
            var ledger = Street.Shift.Ledger;
            var police = 0;
            foreach (var e in ledger.Expenses) if (e.Label == Street.PoliceExpenseLabel) police += e.Amount;
            _goals.PolicePaid(police);
            _goals.Revenue(ledger.Revenue);
            _goals.Passengers(Street.Shift.Cabin.PassengerCount);
            var done = _goals.TakeJustDone();
            if (done != null) Toast(UiText.Fill(_texts.goalDone, "objectif", done.Label), 5f);
        }

        /// <summary>Vendeur ambulant (idée n° 24) : il vient à la fenêtre du chauffeur au feu ; un passager peut acheter et en parler.</summary>
        private void UpdateVendor(float dt, TaxiOnRoute state)
        {
            var e = _vendors.Update(dt, state.Along, state.Direction, taxi.SpeedKmh, _lightAlongs, Street.Shift.Cabin.PassengerCount);
            if (e.Arrived)
            {
                Toast(StreetVendors.Message(e), 5f);
                if (e.Bought)
                    _talk?.Say(Core.Talk.TalkTriggers.Vendor, TalkView.AnyPassenger(Street),
                        new Dictionary<string, string> { [Core.Talk.TalkValues.Article] = e.Goods.label });
                if (_vendor == null)
                {
                    _vendor = new GameObject("Vendeur ambulant");
                    _vendor.transform.SetParent(transform, false);
                    Part(_vendor.transform, PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.85f, 0.45f), Shirts[3]);
                    Part(_vendor.transform, PrimitiveType.Sphere, new Vector3(0f, 1.82f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
                    Part(_vendor.transform, PrimitiveType.Cylinder, new Vector3(0f, 2.05f, 0f), new Vector3(0.6f, 0.08f, 0.6f), new Color(0.75f, 0.55f, 0.25f)); // plateau sur la tête
                }
            }
            var here = _vendors.VendorAt >= 0;
            if (_vendor == null) return;
            _vendor.SetActive(here);
            if (!here) return;
            // À la fenêtre du chauffeur (côté gauche du taxi).
            var t = taxi.transform;
            _vendor.transform.SetPositionAndRotation(t.position - t.right * 1.4f + t.forward * 0.3f, Quaternion.LookRotation(t.right, Vector3.up));
        }

        /// <summary>Repères de la mini-carte : destinations des passagers, postes de police visibles, stations-service.</summary>
        private void UpdateMiniMap()
        {
            _mapMarkers.Clear();
            var seen = new HashSet<string>();
            foreach (var c in Street.Shift.Cabin.Passengers)
            {
                if (!seen.Add(c.DestinationStopId)) continue;
                var index = System.Array.FindIndex(_stopList, st => st.id == c.DestinationStopId);
                if (index >= 0) AddMapMarker(_stopList[index].distanceAlongRouteMeters, StopPalette.For(index));
            }
            if (_police != null)
                foreach (var post in _police.Police.Schedule.Posts)
                    if (Core.Police.PoliceMarkers.Visible(post, Street.Shift.Clock.GameMinutes)) AddMapMarker(post.Along, new Color(0.2f, 0.4f, 1f));
            if (_fuel.stations != null)
                foreach (var station in _fuel.stations) AddMapMarker(station.distanceAlongRouteMeters, new Color(0.2f, 0.85f, 0.35f));
            var forward = taxi.transform.forward;
            _miniMap.Update(taxi.transform.position, forward, _mapMarkers, !Street.DayFinished && !_menu.Frozen);
        }

        private void AddMapMarker(float along, Color color)
        {
            WorldGeometry.SampleAlong(_xz, _cum, along, out var x, out var z, out _, out _);
            _mapMarkers.Add((x, z, color));
        }

        /// <summary>
        /// Poste de contrôle (tâche 4.5, primitives provisoires) à droite de la route dans le sens vers Étoudi : fixe =
        /// abri bleu, barrière rouge et blanche, plots ; volant = plots et pick-up de police. Un policier attend au bord
        /// de la chaussée (il s'avance quand il arrête le taxi). Règle d'affichage : <see cref="Core.Police.PoliceMarkers"/>.
        /// </summary>
        /// <summary>
        /// Événement de ville (idée n° 14) : clients en plus ou en moins, répliques dédiées, barrières de police en travers
        /// de la route fermée (avec collider : le taxi ne passe pas) pendant le cortège, message de fin.
        /// </summary>
        private void UpdateCityEvent(int minute)
        {
            if (!_cityEvent.Any) return;
            var active = _cityEvent.ActiveAt(minute);
            Street.SpawnRateScale *= _cityEvent.ClientsFactor(minute);
            if (_talk != null) _talk.Director.CityEvent = _cityEvent.ActiveId(minute);
            if (_cityEventWasActive && !active && _cityEvent.Event.endText.Length > 0) Toast(_cityEvent.Event.endText, 6f);
            _cityEventWasActive = active;
            if (!_cityEvent.HasClosure) return;
            if (_closure == null)
            {
                _closure = new GameObject("Route fermée : " + _cityEvent.Event.name);
                _closure.transform.SetParent(transform, false);
                foreach (var along in new[] { _cityEvent.Event.closedFromAlong, _cityEvent.Event.closedToAlong })
                {
                    WorldGeometry.SampleAlong(_xz, _cum, along, out var x, out var z, out var dx, out var dz);
                    var barrier = new GameObject("Barrière");
                    barrier.transform.SetParent(_closure.transform, false);
                    barrier.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up));
                    for (var i = -4; i <= 4; i++)
                        Part(barrier.transform, PrimitiveType.Cube, new Vector3(i * 1.6f, 0.6f, 0f), new Vector3(1.5f, 0.25f, 0.2f),
                            i % 2 == 0 ? new Color(0.8f, 0.12f, 0.1f) : new Color(0.92f, 0.92f, 0.9f));
                    var wall = barrier.AddComponent<BoxCollider>();
                    wall.center = new Vector3(0f, 0.8f, 0f);
                    wall.size = new Vector3(16f, 1.6f, 0.4f);
                }
            }
            _closure.SetActive(_cityEvent.ClosedAt(_cityEvent.Event.closedFromAlong, minute));
        }
    }
}
